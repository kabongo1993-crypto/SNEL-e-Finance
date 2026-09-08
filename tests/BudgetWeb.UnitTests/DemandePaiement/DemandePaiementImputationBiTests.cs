using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DetailBI;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementImputationBiTests
{
    private const string DetailElectrique = "Installation électrique";
    private const string DetailReseau = "Installation réseau";
    private const string DetailMobilier = "Mobilier bureau";

    [Fact]
    public async Task GrilleBi_Liste_Details_Item_Et_Rubrique_Sans_Prevision()
    {
        var (svc, repo, idDemande) = await PrepareAsync();
        repo.Previsions[301] = DemandePaiementTestData.PrevisionBi(
            id: 301,
            detailBI: DetailElectrique,
            montantAnnuel: 20_000m);

        var demande = repo.Demandes.Single(d => d.IdDemandePaiement == idDemande);
        demande.Imputations.Add(DemandePaiementTestData.ImputationBiEntity(
            idDemande,
            detailBi: DetailReseau,
            mois: 9));

        var grille = await svc.GetGrilleImputationBiAsync(idDemande, idItemBI: 5, mois: 9);

        Assert.True(grille.Lignes.Count >= 2);
        var sansPrev = grille.Lignes.Single(l => l.DetailBI == DetailReseau);
        Assert.False(sansPrev.PrevisionExiste);
        Assert.Equal(0m, sansPrev.BudgetAnnuel);
    }

    [Fact]
    public async Task EnregistrerBi_Multi_Details_Et_Mois()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 100m);
        repo.Previsions[301] = DemandePaiementTestData.PrevisionBi(id: 301, detailBI: DetailElectrique);
        repo.Previsions[302] = DemandePaiementTestData.PrevisionBi(
            id: 302,
            detailBI: DetailReseau);

        await svc.EnregistrerImputationsBiAsync(
            idDemande,
            new EnregistrerImputationsBiRequest(5, 1, [new LigneImputationBiRequest(DetailElectrique, 40m)]));
        await svc.EnregistrerImputationsBiAsync(
            idDemande,
            new EnregistrerImputationsBiRequest(5, 3, [new LigneImputationBiRequest(DetailReseau, 30m)]));
        await svc.EnregistrerImputationsBiAsync(
            idDemande,
            new EnregistrerImputationsBiRequest(6, 3, [new LigneImputationBiRequest(DetailMobilier, 30m)]));

        var imputations = repo.Demandes.Single(d => d.IdDemandePaiement == idDemande).Imputations;
        Assert.Equal(3, imputations.Count);
        Assert.Equal(100m, imputations.Sum(i => i.MontantUsd));
    }

    [Fact]
    public async Task GrilleBi_Credit_Disponible_Negatif_Visible()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 8_000m);
        repo.Previsions[301] = DemandePaiementTestData.PrevisionBi(
            id: 301,
            detailBI: DetailElectrique,
            montantAnnuel: 1_000m);

        await svc.EnregistrerImputationsBiAsync(
            idDemande,
            new EnregistrerImputationsBiRequest(5, null, [new LigneImputationBiRequest(DetailElectrique, 8_000m)]));

        var grille = await svc.GetGrilleImputationBiAsync(idDemande, 5);
        var ligne = grille.Lignes.Single(l => l.DetailBI == DetailElectrique);
        Assert.True(ligne.CreditDisponibleAnnuel < 0m);
    }

    [Fact]
    public async Task Controle_Annuel_Bi_Independent_Du_Mois()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 100m);
        repo.Previsions[301] = DemandePaiementTestData.PrevisionBi(id: 301, detailBI: DetailElectrique);

        await svc.EnregistrerImputationsBiAsync(
            idDemande,
            new EnregistrerImputationsBiRequest(5, 1, [new LigneImputationBiRequest(DetailElectrique, 40m)]));
        await svc.EnregistrerImputationsBiAsync(
            idDemande,
            new EnregistrerImputationsBiRequest(5, 6, [new LigneImputationBiRequest(DetailElectrique, 60m)]));

        var grille = await svc.GetGrilleImputationBiAsync(idDemande, 5, mois: 6);
        var ligne = grille.Lignes.Single(l => l.DetailBI == DetailElectrique);
        Assert.Equal(60m, ligne.EngagementEnCoursUsd);
        Assert.Equal(100m, ligne.EngagementEnCoursAnnuelUsd);
        Assert.Equal(0m, grille.EcartUsd);
    }

    [Fact]
    public void DetailBi_Libelle_Refuse_Doublon()
    {
        Assert.True(DetailBILibelle.SontEquivalent("Climatisation", "climatisation"));
        Assert.False(DetailBILibelle.SontEquivalent("Climatisation", "Ventilation"));
    }

    [Fact]
    public async Task Viser_Refuse_Si_Somme_Usd_Differente_Bi()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 100m);
        repo.Previsions[301] = DemandePaiementTestData.PrevisionBi(id: 301, detailBI: DetailElectrique);

        await svc.EnregistrerImputationsBiAsync(
            idDemande,
            new EnregistrerImputationsBiRequest(5, null, [new LigneImputationBiRequest(DetailElectrique, 40m)]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ViserAsync(idDemande));
        Assert.Contains("somme des imputations", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(DemandePaiementService Svc, FakeDemandePaiementRepo Repo, long IdDemande)>
        PrepareAsync(decimal montantUsd = 100m)
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[301] = DemandePaiementTestData.PrevisionBi();

        var svc = DemandePaiementTestData.CreateService(repo);
        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                montantBrut: montantUsd,
                typeBudgetSollicite: TypeBudgetCode.BudgetInvestissement,
                itemSollicite: "Installations techniques"));
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
}
