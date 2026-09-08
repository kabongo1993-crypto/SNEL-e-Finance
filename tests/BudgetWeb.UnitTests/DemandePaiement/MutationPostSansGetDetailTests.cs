using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Vérifie que les POST de mutation Vague 1 ne rechargent plus la DPM via un second GetDetailAsync.
/// </summary>
public class MutationPostSansGetDetailTests
{
    [Fact]
    public async Task EnvoyerEnValidation_Reussit_SansGetDetailAsync_Initial()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        repo.ResetPdfPipelineCounters();
        var envoyee = await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetMutationHeaderAsyncCallCount);
        Assert.Equal(0, repo.GetDetailDtoApresMutationAsyncCallCount);
        Assert.Equal(1, repo.GetDetailDtoApresEnvoyerAsyncCallCount);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, envoyee.Statut);
        Assert.Equal(2, envoyee.ValidationsEntite!.Count);
        Assert.Contains("ENVOYER_EN_VALIDATION_N1", repo.Audits.Select(a => a.Operation));
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN1_Reussit_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        repo.ResetPdfPipelineCounters();
        var n1 = await svc.DeclarerValidationPhysiqueN1Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Jean Kabongo", "Chef service", new DateOnly(2026, 3, 1), null));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetMutationHeaderAsyncCallCount);
        Assert.Equal(1, repo.GetEmpreinteReadAsyncCallCount);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, n1.Statut);
        var v = n1.ValidationsEntite!.First(x => x.Niveau == ValidationEntiteNiveau.N1);
        Assert.Equal(ModeValidationEntite.Physique, v.ModeValidation);
        Assert.Equal("Jean Kabongo", v.NomSignatairePhysique);
        Assert.Contains("DECLARER_VALIDATION_PHYSIQUE_N1", repo.Audits.Select(a => a.Operation));
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN2_Reussit_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await svc.DeclarerValidationPhysiqueN1Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Resp N1", null, new DateOnly(2026, 3, 1), null));
        await DemandePaiementTestData.AddDocumentSigneSampleAsync(svc, created.IdDemandePaiement);

        repo.ResetPdfPipelineCounters();
        var n2 = await svc.DeclarerValidationPhysiqueN2Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Resp N2", null, new DateOnly(2026, 3, 2), null));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetMutationHeaderAsyncCallCount);
        Assert.Equal(1, repo.GetValidationsEntiteN2AsyncCallCount);
        Assert.Equal(1, repo.GetEmpreinteReadAsyncCallCount);
        Assert.Equal(1, repo.EnrichDemandeMapDetailShellAsyncCallCount);
        Assert.Equal(StatutDemandePaiement.ValideeEntite, n2.Statut);
        var v = n2.ValidationsEntite!.First(x => x.Niveau == ValidationEntiteNiveau.N2);
        Assert.Equal(ModeValidationEntite.Physique, v.ModeValidation);
        Assert.Contains("DECLARER_VALIDATION_PHYSIQUE_N2", repo.Audits.Select(a => a.Operation));
    }

    [Fact]
    public async Task Soumettre_Reussit_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableServiceDemandeur)
            .ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableEntiteInitiatrice)
            .ValiderN2ElectroniqueAsync(created.IdDemandePaiement);

        repo.ResetPdfPipelineCounters();
        var soumise = await svc.SoumettreAsync(created.IdDemandePaiement);

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetMutationHeaderAsyncCallCount);
        Assert.Equal(1, repo.GetValidationsEntiteSoumettreAsyncCallCount);
        Assert.Equal(1, repo.GetEmpreinteReadAsyncCallCount);
        Assert.Equal(1, repo.EnrichDemandeMapDetailShellAsyncCallCount);
        Assert.Equal(1, repo.EnrichStatutsInstrumentsMapDetailAsyncCallCount);
        Assert.Equal(StatutDemandePaiement.Soumise, soumise.Statut);
        Assert.NotNull(soumise.DateSoumission);
        Assert.Contains("SOUMETTRE", repo.Audits.Select(a => a.Operation));
    }

    [Fact]
    public async Task CreateBrouillon_Reussit_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);

        repo.ResetPdfPipelineCounters();
        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(montantBrut: 12_500m));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(StatutDemandePaiement.Brouillon, created.Statut);
        Assert.True(created.IdDemandePaiement > 0);
        Assert.Equal(12_500m, created.MontantBrut);
        Assert.Contains("CREER", repo.Audits.Select(a => a.Operation));
    }

    [Fact]
    public async Task UpdateBrouillon_Reussit_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        repo.ResetPdfPipelineCounters();
        var updated = await svc.UpdateBrouillonAsync(
            created.IdDemandePaiement,
            DemandePaiementTestData.SampleUpdateRequest(montantBrut: 9_999m));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal("Frais de mission révisés", updated.Objet);
        Assert.Equal(9_999m, updated.MontantBrut);
        Assert.Contains("MODIFIER", repo.Audits.Select(a => a.Operation));
    }
}
