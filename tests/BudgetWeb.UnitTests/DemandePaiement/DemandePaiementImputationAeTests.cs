using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementImputationAeTests
{
    [Fact]
    public async Task GrilleAe_Liste_Toutes_Les_Rubriques_Actives_Meme_Sans_Prevision()
    {
        var (svc, repo, idDemande) = await PrepareAsync();
        repo.RubriquesDc =
        [
            Rubrique(200, "01202", "Frais de mission"),
            Rubrique(201, "00110", "Combustibles"),
            Rubrique(202, "9999", "Sans prévision"),
        ];
        repo.Previsions[200] = DemandePaiementTestData.PrevisionAe(id: 200, montantAnnuel: 20_000m);

        var grille = await svc.GetGrilleImputationAeAsync(idDemande, "Travaux réseau MT", mois: 9);

        Assert.Equal(3, grille.Lignes.Count);
        var sansPrev = grille.Lignes.Single(l => l.IdRubriqueBudgetaire == 202);
        Assert.False(sansPrev.PrevisionExiste);
        Assert.Equal(0m, sansPrev.BudgetAnnuel);
    }

    [Fact]
    public async Task EnregistrerAe_Multi_Lignes_Convertit_Avec_Taux()
    {
        var (svc, repo, idDemande) = await PrepareAsync();
        repo.RubriquesDc = [Rubrique(200, "01202"), Rubrique(201, "00110")];
        var demande = repo.Demandes.Single(d => d.IdDemandePaiement == idDemande);
        demande.Devise = "CDF";
        demande.MontantBrut = 229_000m;
        demande.TauxConversion = 2290m;
        demande.MontantUsd = 100m;

        var grille = await svc.EnregistrerImputationsAeAsync(
            idDemande,
            new EnregistrerImputationsAeRequest(
                "Transport Maniema",
                null,
                9,
                [
                    new LigneImputationAeRequest(200, 114_500m),
                    new LigneImputationAeRequest(201, 114_500m),
                ]));

        Assert.Equal(2, demande.Imputations.Count);
        Assert.All(demande.Imputations, i => Assert.True(i.Mois == 9));
        Assert.All(demande.Imputations, i => Assert.Equal("Transport Maniema", i.LibelleItemAE));
        Assert.Equal(100m, demande.Imputations.Sum(i => i.MontantUsd));
        Assert.Equal(0m, grille.EcartBrut);
    }

    [Fact]
    public async Task EnregistrerAe_Remplace_Uniquement_Item_Et_Mois()
    {
        var (svc, repo, idDemande) = await PrepareAsync();
        repo.RubriquesDc = [Rubrique(200, "01202"), Rubrique(201, "00110")];

        await svc.EnregistrerImputationsAeAsync(
            idDemande,
            new EnregistrerImputationsAeRequest(
                "Transport Maniema",
                null,
                1,
                [new LigneImputationAeRequest(200, 40m)]));
        await svc.EnregistrerImputationsAeAsync(
            idDemande,
            new EnregistrerImputationsAeRequest(
                "Transport Maniema",
                null,
                3,
                [new LigneImputationAeRequest(201, 30m)]));
        await svc.EnregistrerImputationsAeAsync(
            idDemande,
            new EnregistrerImputationsAeRequest(
                "Mission Katanga",
                null,
                3,
                [new LigneImputationAeRequest(200, 30m)]));
        await svc.EnregistrerImputationsAeAsync(
            idDemande,
            new EnregistrerImputationsAeRequest(
                "Transport Maniema",
                null,
                3,
                [new LigneImputationAeRequest(200, 25m)]));

        var imputations = repo.Demandes.Single(d => d.IdDemandePaiement == idDemande).Imputations;
        Assert.Equal(3, imputations.Count);
        Assert.Contains(imputations, i => i.LibelleItemAE == "Transport Maniema" && i.Mois == 1 && i.FK_RubriqueBudgetaire == 200);
        Assert.Contains(imputations, i => i.LibelleItemAE == "Transport Maniema" && i.Mois == 3 && i.FK_RubriqueBudgetaire == 200);
        Assert.Contains(imputations, i => i.LibelleItemAE == "Mission Katanga" && i.Mois == 3);
        Assert.DoesNotContain(imputations, i => i.LibelleItemAE == "Transport Maniema" && i.Mois == 3 && i.FK_RubriqueBudgetaire == 201);
    }

    [Fact]
    public async Task GrilleAe_Credit_Disponible_Negatif_Visible()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 8_000m);
        repo.RubriquesDc = [Rubrique(200, "01202")];
        repo.Previsions[200] = DemandePaiementTestData.PrevisionAe(montantAnnuel: 1_000m);

        await svc.EnregistrerImputationsAeAsync(
            idDemande,
            new EnregistrerImputationsAeRequest(
                "Travaux réseau MT",
                null,
                null,
                [new LigneImputationAeRequest(200, 8_000m)]));

        var grille = await svc.GetGrilleImputationAeAsync(idDemande, "Travaux réseau MT");
        var ligne = grille.Lignes.Single(l => l.IdRubriqueBudgetaire == 200);
        Assert.True(ligne.CreditDisponibleAnnuel < 0m);
    }

    [Fact]
    public async Task Controle_Annuel_Independent_Du_Mois()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 100m);
        repo.RubriquesDc = [Rubrique(200, "01202")];
        repo.Previsions[200] = DemandePaiementTestData.PrevisionAe(montantAnnuel: 50_000m);

        await svc.EnregistrerImputationsAeAsync(
            idDemande,
            new EnregistrerImputationsAeRequest(
                "Travaux réseau MT",
                null,
                1,
                [new LigneImputationAeRequest(200, 40m)]));
        await svc.EnregistrerImputationsAeAsync(
            idDemande,
            new EnregistrerImputationsAeRequest(
                "Travaux réseau MT",
                null,
                6,
                [new LigneImputationAeRequest(200, 60m)]));

        var grille = await svc.GetGrilleImputationAeAsync(idDemande, "Travaux réseau MT", mois: 6);
        var ligne = grille.Lignes.Single(l => l.IdRubriqueBudgetaire == 200);
        Assert.Equal(60m, ligne.EngagementEnCoursUsd);
        Assert.Equal(100m, ligne.EngagementEnCoursAnnuelUsd);
        Assert.Equal(0m, grille.EcartUsd);
    }

    [Fact]
    public async Task Viser_Refuse_Si_Somme_Usd_Differente()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 100m);
        repo.RubriquesDc = [Rubrique(200, "01202")];
        repo.Previsions[200] = DemandePaiementTestData.PrevisionAe(montantAnnuel: 50_000m);

        await svc.EnregistrerImputationsAeAsync(
            idDemande,
            new EnregistrerImputationsAeRequest(
                "Travaux réseau MT",
                null,
                null,
                [new LigneImputationAeRequest(200, 40m)]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ViserAsync(idDemande));
        Assert.Contains("somme des imputations", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(BudgetWeb.Application.Services.DemandePaiementService Svc, FakeDemandePaiementRepo Repo, long IdDemande)>
        PrepareAsync(decimal montantUsd = 100m)
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[200] = DemandePaiementTestData.PrevisionAe(montantAnnuel: 50_000m);
        repo.RubriquesDc = [Rubrique(200, "01202")];

        var svc = DemandePaiementTestData.CreateService(repo);
        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                montantBrut: montantUsd,
                typeBudgetSollicite: TypeBudgetCode.ActionsExploitation,
                itemSollicite: "Travaux réseau MT"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);
        await svc.ReceptionnerAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, svc, created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(svc, created.IdDemandePaiement);
        await svc.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await svc.OrienterAsync(created.IdDemandePaiement, new OrienterDemandePaiementRequest());
        return (svc, repo, created.IdDemandePaiement);
    }

    private static RubriqueBudgetaire Rubrique(long id, string code, string libelle = "Rubrique")
        => new()
        {
            IdRB = id,
            CodeRB = code,
            Libelle = libelle,
            Actif = true,
        };
}
