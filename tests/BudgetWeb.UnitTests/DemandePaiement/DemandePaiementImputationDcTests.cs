using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementImputationDcTests
{
    [Fact]
    public async Task GrilleDc_Liste_Toutes_Les_Rubriques_Actives_Meme_Sans_Prevision()
    {
        var (svc, repo, idDemande) = await PrepareAsync();
        repo.RubriquesDc =
        [
            Rubrique(100, "6041", "Fournitures"),
            Rubrique(101, "6052", "Carburant"),
            Rubrique(102, "9999", "Sans prévision"),
        ];
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(id: 100, montantAnnuel: 12_000m);

        var grille = await svc.GetGrilleImputationDcAsync(idDemande, mois: 3);

        Assert.Equal(3, grille.Lignes.Count);
        Assert.Equal("6041", grille.Lignes[0].CodeRubrique);
        var sansPrev = grille.Lignes.Single(l => l.IdRubriqueBudgetaire == 102);
        Assert.False(sansPrev.PrevisionExiste);
        Assert.Equal(0m, sansPrev.BudgetAnnuel);
        Assert.Equal(0m, sansPrev.BudgetMensuel);
    }

    [Fact]
    public async Task EnregistrerDc_Multi_Lignes_Convertit_Avec_Taux_En_Tete()
    {
        var (svc, repo, idDemande) = await PrepareAsync();
        repo.RubriquesDc = [Rubrique(100, "6041"), Rubrique(101, "6052")];
        var demande = repo.Demandes.Single(d => d.IdDemandePaiement == idDemande);
        demande.Devise = "EUR";
        demande.MontantBrut = 84m;
        demande.TauxConversion = 0.84m;
        demande.MontantUsd = 100m;

        var grille = await svc.EnregistrerImputationsDcAsync(
            idDemande,
            new EnregistrerImputationsDcRequest(
                3,
                [
                    new LigneImputationDcRequest(100, 42m),
                    new LigneImputationDcRequest(101, 42m),
                ]));

        Assert.Equal(2, demande.Imputations.Count);
        Assert.All(demande.Imputations, i => Assert.Equal(0.84m, i.TauxConversion));
        Assert.All(demande.Imputations, i => Assert.Equal("EUR", i.Devise));
        Assert.Equal(100m, demande.Imputations.Sum(i => i.MontantUsd));
        Assert.Equal(0m, grille.EcartBrut);
        Assert.True(Math.Abs(grille.EcartUsd) < DemandePaiementMontants.ToleranceUsd);
        Assert.Equal(100, demande.Imputations.Single(i => i.FK_RubriqueBudgetaire == 100).FK_BudgetLigne);
        Assert.Null(demande.Imputations.Single(i => i.FK_RubriqueBudgetaire == 101).FK_BudgetLigne);
    }

    [Fact]
    public async Task EnregistrerDc_Autorise_Ecart_Non_Nul()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 100m);
        repo.RubriquesDc = [Rubrique(100, "6041")];

        var grille = await svc.EnregistrerImputationsDcAsync(
            idDemande,
            new EnregistrerImputationsDcRequest(3, [new LigneImputationDcRequest(100, 40m)]));

        Assert.Equal(60m, grille.EcartBrut);
        Assert.Single(repo.Demandes.Single(d => d.IdDemandePaiement == idDemande).Imputations);
    }

    [Fact]
    public async Task EnregistrerDc_Remplace_Uniquement_Le_Mois_Cible()
    {
        var (svc, repo, idDemande) = await PrepareAsync();
        repo.RubriquesDc = [Rubrique(100, "6041"), Rubrique(101, "6052")];

        await svc.EnregistrerImputationsDcAsync(
            idDemande,
            new EnregistrerImputationsDcRequest(2, [new LigneImputationDcRequest(100, 30m)]));
        await svc.EnregistrerImputationsDcAsync(
            idDemande,
            new EnregistrerImputationsDcRequest(3, [new LigneImputationDcRequest(101, 70m)]));
        await svc.EnregistrerImputationsDcAsync(
            idDemande,
            new EnregistrerImputationsDcRequest(3, [new LigneImputationDcRequest(100, 70m)]));

        var imputations = repo.Demandes.Single(d => d.IdDemandePaiement == idDemande).Imputations;
        Assert.Equal(2, imputations.Count);
        Assert.Contains(imputations, i => i.Mois == 2 && i.FK_RubriqueBudgetaire == 100);
        Assert.Contains(imputations, i => i.Mois == 3 && i.FK_RubriqueBudgetaire == 100);
        Assert.DoesNotContain(imputations, i => i.Mois == 3 && i.FK_RubriqueBudgetaire == 101);
    }

    [Fact]
    public async Task Viser_Refuse_Si_Somme_Usd_Differente()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 100m);
        repo.RubriquesDc = [Rubrique(100, "6041")];

        await svc.EnregistrerImputationsDcAsync(
            idDemande,
            new EnregistrerImputationsDcRequest(3, [new LigneImputationDcRequest(100, 40m)]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ViserAsync(idDemande));
        Assert.Contains("somme des imputations", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GrilleDc_Montre_Credit_Disponible_Negatif()
    {
        var (svc, repo, idDemande) = await PrepareAsync(montantUsd: 8_000m);
        repo.RubriquesDc = [Rubrique(100, "6041")];
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 1_200m);

        await svc.EnregistrerImputationsDcAsync(
            idDemande,
            new EnregistrerImputationsDcRequest(3, [new LigneImputationDcRequest(100, 8_000m)]));

        var grille = await svc.GetGrilleImputationDcAsync(idDemande, 3);
        var ligne = grille.Lignes.Single(l => l.IdRubriqueBudgetaire == 100);
        Assert.True(ligne.CreditDisponibleMensuel < 0m);
        Assert.True(ligne.CreditDisponibleAnnuel < 0m);
    }

    private static async Task<(BudgetWeb.Application.Services.DemandePaiementService Svc, FakeDemandePaiementRepo Repo, long IdDemande)>
        PrepareAsync(decimal montantUsd = 100m)
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        repo.RubriquesDc = [Rubrique(100, "6041")];

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
