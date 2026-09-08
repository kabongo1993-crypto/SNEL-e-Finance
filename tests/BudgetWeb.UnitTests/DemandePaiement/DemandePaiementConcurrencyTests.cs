using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementConcurrencyTests
{
    private static DemandePaiementService CreateAgentService(FakeDemandePaiementRepo repo)
        => DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

    [Fact]
    public async Task EnvoyerEnValidation_Second_Appel_Retourne_Conflit_409()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = CreateAgentService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);

        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        await Assert.ThrowsAsync<DemandePaiementConcurrencyException>(() =>
            svc.EnvoyerEnValidationAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task UpdateBrouillon_Apres_EnvoyerValidation_Retourne_Conflit_409()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = CreateAgentService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);

        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateBrouillonAsync(
                created.IdDemandePaiement,
                DemandePaiementTestData.SampleUpdateRequest()));

        var detail = await svc.GetByIdAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, detail!.Statut);
    }

    [Fact]
    public async Task Soumettre_Second_Appel_Retourne_Conflit_409()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc();
        var svc = CreateAgentService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);

        await Assert.ThrowsAsync<DemandePaiementConcurrencyException>(() =>
            svc.SoumettreAsync(created.IdDemandePaiement));

        Assert.Equal(
            StatutDemandePaiement.Soumise,
            repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task Viser_Second_Appel_Retourne_Conflit_409()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement);
        MettreEnControle(repo, created.IdDemandePaiement, idTypeBudget: 1);
        await svc.AddImputationAsync(created.IdDemandePaiement, DemandePaiementTestData.ImputationDc());

        await svc.ViserAsync(created.IdDemandePaiement);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ViserAsync(created.IdDemandePaiement));

        Assert.Equal(
            StatutDemandePaiement.ViseeBudgetairement,
            repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task Deux_Onglets_Envoyer_Puis_Enregistrer_Statut_Coherent()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = CreateAgentService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);

        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateBrouillonAsync(
                created.IdDemandePaiement,
                DemandePaiementTestData.SampleUpdateRequest()));

        Assert.Equal(
            StatutDemandePaiement.EnValidationN1,
            repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task Soumettre_Sans_Validation_Entite_Reste_Erreur_Metier()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = CreateAgentService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SoumettreAsync(created.IdDemandePaiement));

        Assert.IsNotType<DemandePaiementConcurrencyException>(ex);
        Assert.Contains("validée par l'entité", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void MettreEnControle(FakeDemandePaiementRepo repo, long idDemande, long idTypeBudget)
    {
        var tracked = repo.Demandes.Single(d => d.IdDemandePaiement == idDemande);
        tracked.Statut = StatutDemandePaiement.EnControleBudgetaire;
        tracked.FK_TypeBudget = idTypeBudget;
        tracked.FK_VersionBudgetaire = 4;
        tracked.MontantUsd = tracked.MontantBrut;
        tracked.TauxConversion = 1m;
    }
}
