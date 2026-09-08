using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class FicheImputationBudgetaireTests
{
    [Fact]
    public void CalculerTotaux_Travail_Ne_Contient_Pas_Les_Colonnes_Budgetaires()
    {
        var lignes = new[]
        {
            new FicheImputationLigneDto(1, "A00300", "1", "00100", null, null, 7_600m, null, null, null, 7_600m, null),
            new FicheImputationLigneDto(2, "A00300", "2", "00122", null, null, 4_400m, null, null, null, 4_400m, null),
        };

        var totaux = FicheImputationBudgetaireBuilder.CalculerTotaux(
            FicheImputationMode.Travail,
            lignes,
            12_000m);

        Assert.Null(totaux.TotalBudgetMensuel);
        Assert.Null(totaux.TotalCreditEngageMensuel);
        Assert.Equal(12_000m, totaux.TotalEngagementEnCoursMensuel);
        Assert.Null(totaux.TotalCreditDisponibleMensuel);
        Assert.Equal(12_000m, totaux.TotalEngagementEnCoursAnnuel);
    }

    [Fact]
    public async Task GetFicheImputation_Travail_Expose_Uniquement_Engagements()
    {
        var (repo, idDemande) = await PrepareDemandeEnControleAsync(montantUsd: 7_600m);
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.JuniorDc);

        var fiche = await svc.GetFicheImputationAsync(idDemande, FicheImputationMode.Travail);

        Assert.Equal(FicheImputationMode.Travail, fiche.Mode);
        Assert.Null(fiche.DateEngagement);
        Assert.Single(fiche.Lignes);
        Assert.Equal(7_600m, fiche.Lignes[0].EngagementEnCoursMensuel);
        Assert.Equal(7_600m, fiche.Lignes[0].EngagementEnCoursAnnuel);
        Assert.Null(fiche.Lignes[0].BudgetMensuel);
        Assert.Null(fiche.Lignes[0].CreditDisponibleAnnuel);
    }

    [Fact]
    public async Task GetFicheImputation_Definitive_Reutilise_Snapshots()
    {
        var (repo, idDemande) = await PrepareDemandeEnControleAsync(
            montantUsd: 7_600m,
            prevision: DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m));
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ChefDivision));

        await svc.ViserAsync(idDemande);

        var fiche = await svc.GetFicheImputationAsync(idDemande, FicheImputationMode.Definitive);

        Assert.Equal(FicheImputationMode.Definitive, fiche.Mode);
        Assert.NotNull(fiche.DateEngagement);
        Assert.Single(fiche.Lignes);
        Assert.Equal(120_000m, fiche.Lignes[0].BudgetAnnuel);
        Assert.NotNull(fiche.Lignes[0].CreditDisponibleAnnuel);
        Assert.Equal(7_600m, fiche.Lignes[0].EngagementEnCoursMensuel);

        var snapshot = repo.Demandes
            .SelectMany(d => d.Imputations)
            .SelectMany(i => i.Snapshots)
            .Single();

        Assert.Equal(snapshot.BudgetAnnuel, fiche.Lignes[0].BudgetAnnuel);
        Assert.Equal(snapshot.CreditDisponibleAnnuelAvantVisa, fiche.Lignes[0].CreditDisponibleAnnuel);
    }

    [Fact]
    public async Task GenererFicheImputationPdf_Retourne_Pdf()
    {
        var (repo, idDemande) = await PrepareDemandeEnControleAsync();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.JuniorDc);

        var pdf = await svc.GenererFicheImputationPdfAsync(idDemande, FicheImputationMode.Travail);

        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal(0x50, pdf[1]);
    }

    [Fact]
    public async Task GetFicheImputation_Definitive_Avant_Visa_Echoue()
    {
        var (repo, idDemande) = await PrepareDemandeEnControleAsync();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.JuniorDc);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GetFicheImputationAsync(idDemande, FicheImputationMode.Definitive));

        Assert.Contains("visa", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(FakeDemandePaiementRepo Repo, long IdDemande)> PrepareDemandeEnControleAsync(
        decimal montantUsd = 7_600m,
        PrevisionBudgetaire? prevision = null,
        FakeDemandePaiementRepo? repo = null)
    {
        repo ??= new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        prevision ??= DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        repo.Previsions[prevision.IdPrevision] = prevision;

        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

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

        return (repo, created.IdDemandePaiement);
    }
}
