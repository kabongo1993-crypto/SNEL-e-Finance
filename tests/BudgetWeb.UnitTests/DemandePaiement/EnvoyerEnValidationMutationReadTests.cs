using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Vague 2b — EnvoyerEnValidation via lectures ciblées (sans DetailQuery initial).</summary>
public class EnvoyerEnValidationMutationReadTests
{
    [Fact]
    public async Task EnvoyerEnValidation_NeDependPasDeGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo { ForbidGetDetailDtoApresMutationAsync = true };
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        repo.ResetPdfPipelineCounters();
        var result = await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetMutationHeaderAsyncCallCount);
        Assert.Equal(0, repo.GetDetailDtoApresMutationAsyncCallCount);
        Assert.Equal(1, repo.GetDetailDtoApresEnvoyerAsyncCallCount);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, result.Statut);
        Assert.Equal(2, result.ValidationsEntite!.Count);
    }

    [Fact]
    public async Task EnvoyerEnValidation_GetDetailDtoApresMutationAsync_InterditPendantWorkflow()
    {
        var repo = new FakeDemandePaiementRepo { ForbidGetDetailDtoApresMutationAsync = true };
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        var result = await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, result.Statut);
        Assert.Equal(0, repo.GetDetailDtoApresMutationAsyncCallCount);
    }

    [Fact]
    public async Task EnvoyerEnValidation_GetDetailAsync_InterditPendantWorkflow()
    {
        var repo = new FakeDemandePaiementRepo { ForbidGetDetailAsync = true };
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        var result = await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, result.Statut);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
    }

    [Fact]
    public async Task EnvoyerEnValidation_StatutIncorrect_Rejete()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        await Assert.ThrowsAsync<DemandePaiementConcurrencyException>(
            () => svc.EnvoyerEnValidationAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task EnvoyerEnValidation_DemandeurNonAutorise_Rejete()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        var intrus = DemandePaiementTestData.CreateService(repo, [AppPermissions.PaiementsLire]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => intrus.EnvoyerEnValidationAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task EnvoyerEnValidation_EnteteInvalide_Rejete()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var demande = repo.Demandes.First(d => d.IdDemandePaiement == created.IdDemandePaiement);
        demande.Objet = "   ";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.EnvoyerEnValidationAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task EnvoyerEnValidation_AuditEnregistre()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var audit = Assert.Single(repo.Audits.Where(a => a.Operation == "ENVOYER_EN_VALIDATION_N1"));
        Assert.Equal(created.IdDemandePaiement, audit.IdEntite);
    }

    [Fact]
    public async Task EnvoyerEnValidation_DtoComplet_ContientCollectionsEtCircuit()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(montantBrut: 250m));

        var before = await svc.GetByIdAsync(created.IdDemandePaiement);
        Assert.NotNull(before);

        var result = await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        Assert.Equal(StatutDemandePaiement.EnValidationN1, result.Statut);
        Assert.Equal("EN ATTENTE N1", result.CircuitEntiteStatut);
        Assert.Equal(before.Reference, result.Reference);
        Assert.Equal(before.Objet, result.Objet);
        Assert.Equal(before.MontantBrut, result.MontantBrut);
        Assert.Equal(before.CodeUB, result.CodeUB);
        Assert.Equal(before.LibelleCasDossier, result.LibelleCasDossier);
        Assert.Equal(2, result.ValidationsEntite!.Count);
        Assert.Contains(result.ValidationsEntite, v => v.Niveau == 1 && v.Statut == "EN_ATTENTE");
        Assert.Contains(result.ValidationsEntite, v => v.Niveau == 2 && v.Statut == "EN_ATTENTE");
        Assert.Equal(before.Beneficiaires.Count, result.Beneficiaires.Count);
        Assert.Equal(before.Imputations.Count, result.Imputations.Count);
        Assert.Equal(before.Pieces.Count, result.Pieces.Count);
        Assert.False(result.ModePaiementVerrouille);
    }
}