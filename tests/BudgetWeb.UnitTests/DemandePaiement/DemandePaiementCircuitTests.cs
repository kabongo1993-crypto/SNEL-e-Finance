using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Circuit DPM Département → Chargé → Junior et Chargé → Junior.</summary>
public class DemandePaiementCircuitTests
{
    [Fact]
    public async Task Circuit_Departement_Vers_Junior_Dc()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);

        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsEcrire, AppPermissions.PaiementsSoumettre]);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);
        var junior = DemandePaiementTestData.CreateService(repo, AppPermissions.JuniorDc);

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(10_000m));
        Assert.Equal(StatutDemandePaiement.Brouillon, created.Statut);
        Assert.Equal(1, created.IdDemandeur);
        Assert.Single(repo.Demandes);

        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        var soumise = await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, demandeur);
        Assert.Equal(StatutDemandePaiement.Soumise, soumise.Statut);

        var fileCharge = await charge.ListAsync(new DemandePaiementQuery(Statut: StatutDemandePaiement.Soumise));
        Assert.Contains(fileCharge, d => d.IdDemandePaiement == created.IdDemandePaiement);

        var fileJuniorAvant = await junior.ListAsync(new DemandePaiementQuery());
        Assert.DoesNotContain(fileJuniorAvant, d => d.IdDemandePaiement == created.IdDemandePaiement);

        await charge.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);
        var traite = await charge.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, traite.Statut);
        Assert.Equal(10_000m, traite.MontantBrut);
        Assert.Equal("USD", traite.Devise);
        Assert.Equal("CDF", traite.DevisePaiement);
        Assert.Equal(10_000m * FakeTauxChangeService.TauxUsdCdf, traite.MontantPaiement);
        Assert.NotNull(traite.MontantUsd);

        await charge.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());

        var fileJunior = await junior.ListAsync(new DemandePaiementQuery());
        Assert.Contains(fileJunior, d => d.IdDemandePaiement == created.IdDemandePaiement);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            demandeur.AddImputationAsync(created.IdDemandePaiement, DemandePaiementTestData.ImputationDc()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            charge.AddImputationAsync(created.IdDemandePaiement, DemandePaiementTestData.ImputationDc()));

        var imp = await junior.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(montantUsd: 10_000m, idBudgetLigne: 100));
        Assert.Equal(TypeBudgetCode.DepensesCourantes, imp.CodeTypeBudget);
        Assert.Single(repo.Demandes);
    }

    [Fact]
    public async Task Circuit_Charge_Direct_Sans_Soumise()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                modePaiementSollicite: ModePaiementDpm.Banque,
                typeBudgetSollicite: TypeBudgetCode.ActionsExploitation,
                itemSollicite: "025"));
        Assert.Equal(1, created.IdDemandeur);
        Assert.Equal(1, repo.Demandes.Single().FK_UtilisateurCreation);

        await DemandePaiementTestData.AddSamplePieceAsync(charge, created.IdDemandePaiement);
        var enTraitement = await charge.EntrerTraitementAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, enTraitement.Statut);
        Assert.Null(enTraitement.DateSoumission);
        Assert.DoesNotContain(
            repo.Audits,
            a => a.Operation == "SOUMETTRE" && a.IdEntite == created.IdDemandePaiement);

        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge,
            created.IdDemandePaiement,
            TypeInstrumentPaiement.MinuteCheque);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);
        await charge.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("MINUTE_CHEQUE", "USD", null, null));
        await charge.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());

        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, repo.Demandes.Single().Statut);
        Assert.Equal(2, repo.Demandes.Single().FK_TypeBudget);
        Assert.Single(repo.Demandes);
    }

    [Fact]
    public async Task Charge_Caisse_Banque_Et_Orientations()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        foreach (var (mode, instrument, codeType, idType, item) in new[]
                 {
                     ("CAISSE", "PIECE_CAISSE", TypeBudgetCode.DepensesCourantes, 1L, (string?)null),
                     ("CAISSE", "BON_PROVISOIRE", TypeBudgetCode.ActionsExploitation, 2L, "025"),
                     ("BANQUE", "MINUTE_CHEQUE", TypeBudgetCode.BudgetInvestissement, 3L, "018"),
                 })
        {
            var created = await charge.CreateBrouillonAsync(
                DemandePaiementTestData.SampleCreateRequest(
                    typeBudgetSollicite: codeType,
                    itemSollicite: item,
                    modePaiementSollicite: mode));
            await DemandePaiementTestData.AddSamplePieceAsync(charge, created.IdDemandePaiement);
            await charge.EntrerTraitementAsync(created.IdDemandePaiement);
            await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge,
                created.IdDemandePaiement,
                instrument);
            await DemandePaiementTestData.EtablirBilletConversionStandardAsync(
                charge,
                created.IdDemandePaiement);

            var traite = await charge.TraiterChargeAsync(
                created.IdDemandePaiement,
                new TraitementChargeDpmRequest(instrument, mode == "CAISSE" ? "CDF" : "USD", null, null));
            Assert.Equal(mode, traite.ModePaiementSollicite);
            Assert.Equal(instrument, traite.TypeInstrumentPaiement);
            await charge.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());
            Assert.Equal(idType, repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).FK_TypeBudget);
        }
    }

    [Fact]
    public async Task Charge_Peut_Modifier_Mode_Et_Type_Budget_Sollicites()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsEcrire, AppPermissions.PaiementsSoumettre]);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await demandeur.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                modePaiementSollicite: ModePaiementDpm.Caisse,
                typeBudgetSollicite: TypeBudgetCode.DepensesCourantes,
                itemSollicite: null));
        Assert.Equal(ModePaiementDpm.Caisse, created.ModePaiementSollicite);
        Assert.Equal(TypeBudgetCode.DepensesCourantes, created.TypeBudgetSollicite);

        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, demandeur);
        await charge.ReceptionnerAsync(created.IdDemandePaiement);

        await charge.RetenirSollicitationChargeAsync(
            created.IdDemandePaiement,
            new RetenirSollicitationChargeRequest(
                ModePaiementSollicite: ModePaiementDpm.Banque,
                TypeBudgetSollicite: TypeBudgetCode.ActionsExploitation,
                ItemSollicite: "025"));

        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge,
            created.IdDemandePaiement,
            TypeInstrumentPaiement.MinuteCheque);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);

        var traite = await charge.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest(
                TypeInstrumentPaiement.MinuteCheque,
                "USD",
                null,
                null));

        Assert.Equal(ModePaiementDpm.Banque, traite.ModePaiementSollicite);
        Assert.Equal(TypeBudgetCode.ActionsExploitation, traite.TypeBudgetSollicite);
        Assert.Equal("025", traite.ItemSollicite);
        Assert.Equal(TypeInstrumentPaiement.MinuteCheque, traite.TypeInstrumentPaiement);

        await charge.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());
        Assert.Equal(2, repo.Demandes.Single().FK_TypeBudget);
    }

    [Fact]
    public async Task Juniors_Imputent_Leur_Filiere_Uniquement()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var admin = DemandePaiementTestData.CreateService(repo);
        var juniorAe = DemandePaiementTestData.CreateService(repo, AppPermissions.JuniorAe);

        var created = await admin.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(admin, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, admin);
        await admin.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, admin, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(admin, created.IdDemandePaiement);
        await admin.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await admin.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            juniorAe.AddImputationAsync(created.IdDemandePaiement, DemandePaiementTestData.ImputationDc()));

        var juniorDc = DemandePaiementTestData.CreateService(repo, AppPermissions.JuniorDc);
        var imp = await juniorDc.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc());
        Assert.Equal("DC", imp.CodeTypeBudget);
    }

    [Fact]
    public async Task Retour_Et_Visa_Sans_Tresorerie()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);
        await svc.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, svc, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(svc, created.IdDemandePaiement);
        await svc.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));

        var retour = await svc.RetournerAsync(
            created.IdDemandePaiement,
            new RetourDemandePaiementRequest("Correction", "Pièce manquante", "EN_TRAITEMENT_DPM"));
        Assert.Equal(StatutDemandePaiement.ACorriger, retour.Statut);
        Assert.Contains("EN_TRAITEMENT_DPM", retour.CommentaireRetour ?? string.Empty);

        await svc.RemettreEnBrouillonAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);
        await svc.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, svc, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(svc, created.IdDemandePaiement);
        await svc.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await svc.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());

        var junior = DemandePaiementTestData.CreateService(repo, AppPermissions.JuniorDc);
        await junior.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(montantUsd: 100m, idBudgetLigne: 100));

        var visee = await svc.ViserAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.ViseeBudgetairement, visee.Statut);
        Assert.DoesNotContain(repo.Audits, a => a.Operation.Contains("TRESOR", StringComparison.OrdinalIgnoreCase));
        Assert.Single(repo.Demandes);
    }

    [Fact]
    public async Task Soumettre_Ne_Graft_Pas_Detail_AsNoTracking_Sur_Tracked()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);

        var soumise = await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);
        Assert.Equal(StatutDemandePaiement.Soumise, soumise.Statut);
        Assert.Same(
            repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement),
            repo.Demandes.Single());
    }
}
