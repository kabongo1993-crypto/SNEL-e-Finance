using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementServiceTests
{
    [Fact]
    public async Task CreateBrouillon_Cree_Statut_Brouillon()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        Assert.Equal(StatutDemandePaiement.Brouillon, created.Statut);
        Assert.StartsWith("DP-2026-", created.Reference);
        Assert.Equal(100m, created.MontantBrut);
        Assert.Equal("USD", created.Devise);
        Assert.Null(created.MontantUsd);
        Assert.Null(created.TauxConversion);
        Assert.Equal("CAISSE", created.ModePaiementSollicite);
        Assert.Equal("DC", created.TypeBudgetSollicite);
        Assert.Null(created.ItemSollicite);
        Assert.Null(created.IdTypeBudget);
        Assert.Equal(1, created.IdDemandeur);
        Assert.Equal(10, created.IdUB);
        Assert.Equal("Direction Générale", created.LibelleDepartement);
        Assert.Single(repo.Demandes);
    }

    [Fact]
    public async Task CreateBrouillon_Refuse_UbDifferenteDuDemandeur()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(idUB: 99)));

        Assert.Contains("demandeur", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBrouillon_Refuse_DemandeurInactif()
    {
        var demandeurs = new FakeDemandeurRepo();
        demandeurs.Items[0].Actif = false;
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, demandeurs: demandeurs);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest()));
    }

    [Fact]
    public async Task Demandeur_Create_RattacheUneSeuleUb()
    {
        var demandeurs = new FakeDemandeurRepo();
        var svc = DemandePaiementTestData.CreateDemandeurService(demandeurs);

        var created = await svc.CreateAsync(new CreateDemandeurRequest("NEW/CODE", "Nouveau", 10));

        Assert.Equal("NEW/CODE", created.Code);
        Assert.Equal(10, created.IdUB);
        Assert.Equal("Direction Générale", created.LibelleDepartement);
    }

    [Fact]
    public async Task Demandeur_CodeUnique()
    {
        var demandeurs = new FakeDemandeurRepo();
        var svc = DemandePaiementTestData.CreateDemandeurService(demandeurs);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateDemandeurRequest("DDK/DKC/DG", "Dup", 10)));
    }

    [Fact]
    public async Task UpdateBrouillon_Modifie_Entete()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var updated = await svc.UpdateBrouillonAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.SampleUpdateRequest());

        Assert.Equal(StatutDemandePaiement.Brouillon, updated.Statut);
        Assert.Equal("Frais de mission révisés", updated.Objet);
        Assert.Equal(150m, updated.MontantBrut);
        Assert.Equal("Lubumbashi", updated.LieuEmission);
        Assert.Equal("CAISSE", updated.ModePaiementSollicite);
        Assert.Null(updated.MontantUsd);
    }

    [Fact]
    public async Task Soumettre_Avec_Pieces_Completes_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);

        var soumise = await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);

        Assert.Equal(StatutDemandePaiement.Soumise, soumise.Statut);
        Assert.NotNull(soumise.DateSoumission);
        Assert.Null(soumise.IdTypeBudget);
        Assert.Null(soumise.MontantUsd);
    }

    [Fact]
    public async Task Soumettre_Sans_Piece_Obligatoire_Echoue_Avec_Message_Explicite()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(
                repo,
                AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur))
            .ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(
                repo,
                AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice))
            .ValiderN2ElectroniqueAsync(created.IdDemandePaiement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SoumettreAsync(created.IdDemandePaiement));

        Assert.Contains("Facture fournisseur", ex.Message, StringComparison.Ordinal);
        Assert.Contains("manquante", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddImputation_Accepte_Dc_Ae_Et_Bi()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var dc = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(100m));
        await MettreEnControleAsync(repo, dc.IdDemandePaiement, idTypeBudget: 1);
        var impDc = await svc.AddImputationAsync(dc.IdDemandePaiement, DemandePaiementTestData.ImputationDc());
        Assert.Equal(TypeBudgetCode.DepensesCourantes, impDc.CodeTypeBudget);

        var ae = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(500m));
        await MettreEnControleAsync(repo, ae.IdDemandePaiement, idTypeBudget: 2);
        var impAe = await svc.AddImputationAsync(ae.IdDemandePaiement, DemandePaiementTestData.ImputationAe());
        Assert.Equal(TypeBudgetCode.ActionsExploitation, impAe.CodeTypeBudget);

        var bi = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(1_000m));
        await MettreEnControleAsync(repo, bi.IdDemandePaiement, idTypeBudget: 3);
        var impBi = await svc.AddImputationAsync(bi.IdDemandePaiement, DemandePaiementTestData.ImputationBi());
        Assert.Equal(TypeBudgetCode.BudgetInvestissement, impBi.CodeTypeBudget);
    }

    [Fact]
    public async Task AddImputation_Sans_FkBudgetLigne_Est_Valide()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await MettreEnControleAsync(repo, created.IdDemandePaiement, idTypeBudget: 1);
        var imp = await svc.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(idBudgetLigne: null));

        Assert.Null(imp.IdBudgetLigne);
    }

    [Fact]
    public async Task ControlerBudgetaire_Credit_Dc_Insuffisant_Est_Ok_Avec_Depassement()
    {
        var (svc, repo, idDemande) = await PrepareDemandeEnControleAsync(
            montantUsd: 10_000m,
            prevision: DemandePaiementTestData.PrevisionDc(montantAnnuel: 5_000m));

        var controle = await svc.ControlerBudgetaireAsync(idDemande);

        Assert.True(controle.EstValide);
        Assert.True(controle.Imputations[0].EstValide);
        Assert.True(controle.Imputations[0].DepassementAnnuel);
        Assert.True(controle.Imputations[0].CreditDisponibleAnnuel < 0m);
        Assert.Equal(0, repo.TotalSnapshotCount());
    }

    [Fact]
    public async Task Retourner_Passe_A_Corriger()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        await charge.ReceptionnerAsync(id);

        var retournee = await charge.RetournerAsync(
            id,
            new RetourDemandePaiementRequest("Pièces incomplètes", "Merci de joindre la facture signée"));

        Assert.Equal(StatutDemandePaiement.ACorriger, retournee.Statut);
        Assert.Equal("Pièces incomplètes", retournee.MotifRetour);
    }

    [Fact]
    public async Task RemettreEnBrouillon_Apres_Correction_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        await charge.ReceptionnerAsync(id);

        await charge.RetournerAsync(id, new RetourDemandePaiementRequest("Correction requise", null));
        var demandeur = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var brouillon = await demandeur.RemettreEnBrouillonAsync(id);

        Assert.Equal(StatutDemandePaiement.Brouillon, brouillon.Statut);
        Assert.Null(brouillon.MotifRetour);
    }

    [Fact]
    public async Task Viser_Valide_Cree_Snapshots()
    {
        var (svc, repo, idDemande) = await PrepareDemandeEnControleAsync(
            prevision: DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m));

        var visee = await svc.ViserAsync(idDemande);

        Assert.Equal(StatutDemandePaiement.ViseeBudgetairement, visee.Statut);
        Assert.NotNull(visee.DateVisa);
        Assert.Equal(1, repo.TotalSnapshotCount());
        Assert.All(
            repo.Demandes.SelectMany(d => d.Imputations),
            i => Assert.Single(i.Snapshots));
    }

    [Fact]
    public async Task Viser_Sans_Pieces_Echoue()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await MettreEnControleAsync(repo, created.IdDemandePaiement, idTypeBudget: 1);
        await svc.AddImputationAsync(created.IdDemandePaiement, DemandePaiementTestData.ImputationDc());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ViserAsync(created.IdDemandePaiement));

        Assert.Contains("Facture fournisseur", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, repo.TotalSnapshotCount());
    }

    [Fact]
    public async Task Viser_Credit_Dc_Insuffisant_Reussit()
    {
        var (svc, repo, idDemande) = await PrepareDemandeEnControleAsync(
            montantUsd: 10_000m,
            prevision: DemandePaiementTestData.PrevisionDc(montantAnnuel: 5_000m));

        var visee = await svc.ViserAsync(idDemande);

        Assert.Equal(StatutDemandePaiement.ViseeBudgetairement, visee.Statut);
        Assert.Equal(1, repo.TotalSnapshotCount());
    }

    [Fact]
    public async Task SumEngage_Retourne_Zero_Avant_Visa()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc();

        var engage = await repo.SumEngageDcAnnuelAsync(1, 10, 100, excludeDemandeId: null);
        Assert.Equal(0m, engage);

        var (svc, _, idDemande) = await PrepareDemandeEnControleAsync(repo: repo);
        engage = await repo.SumEngageDcAnnuelAsync(1, 10, 100, excludeDemandeId: idDemande);
        Assert.Equal(0m, engage);
    }

    [Fact]
    public async Task Snapshot_Count_Correspond_Au_Nombre_Imputations_Apres_Visa()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(montantBrut: 600m));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);
        await svc.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, svc, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(svc, created.IdDemandePaiement);
        await svc.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await svc.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());
        await svc.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(montantUsd: 100m));
        await svc.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(montantUsd: 500m) with { Ordre = 2, Mois = 4 });

        await svc.ViserAsync(created.IdDemandePaiement);

        var nbImputations = repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).Imputations.Count;
        Assert.Equal(2, nbImputations);
        Assert.Equal(nbImputations, repo.TotalSnapshotCount());
    }

    [Fact]
    public async Task Viser_Echec_Persistence_Ne_Laisse_Pas_Snapshots()
    {
        var (svc, repo, idDemande) = await PrepareDemandeEnControleAsync();

        repo.FailSaveChanges = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ViserAsync(idDemande));

        var demande = repo.Demandes.Single(d => d.IdDemandePaiement == idDemande);
        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, demande.Statut);
        Assert.Equal(0, repo.TotalSnapshotCount());
    }

    [Fact]
    public async Task CreateBrouillon_Refuse_Sans_Permission_Ecrire()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest()));
    }

    private static async Task<(DemandePaiementService Svc, FakeDemandePaiementRepo Repo, long IdDemande)>
        PrepareDemandeEnControleAsync(
            decimal montantUsd = 100m,
            PrevisionBudgetaire? prevision = null,
            FakeDemandePaiementRepo? repo = null)
    {
        repo ??= new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        prevision ??= DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        repo.Previsions[prevision.IdPrevision] = prevision;

        var svc = DemandePaiementTestData.CreateService(repo);
        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(montantBrut: montantUsd));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);
        await svc.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, svc, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(svc, created.IdDemandePaiement);
        await svc.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await svc.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());
        await svc.AddImputationAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.ImputationDc(
                montantUsd: montantUsd,
                idBudgetLigne: prevision.IdPrevision));

        return (svc, repo, created.IdDemandePaiement);
    }

    private static Task MettreEnControleAsync(
        FakeDemandePaiementRepo repo,
        long idDemande,
        long idTypeBudget)
    {
        var tracked = repo.Demandes.Single(d => d.IdDemandePaiement == idDemande);
        tracked.Statut = StatutDemandePaiement.EnControleBudgetaire;
        tracked.FK_TypeBudget = idTypeBudget;
        tracked.FK_VersionBudgetaire = 4;
        tracked.MontantUsd = tracked.MontantBrut;
        tracked.TauxConversion = 1m;
        tracked.ModePaiementSollicite = ModePaiementSollicite.Caisse;
        return Task.CompletedTask;
    }
}
