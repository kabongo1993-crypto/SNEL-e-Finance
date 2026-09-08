using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Vague 2b-3 — DeclarerValidationPhysique N1 via lectures ciblées.</summary>
public class DeclarerPhysiqueN1MutationReadTests
{
    private static readonly DeclarationValidationPhysiqueRequest PhysiqueRequest =
        new("Jean Kabongo", "Chef service", new DateOnly(2026, 3, 1), null);

    [Fact]
    public async Task DeclarerValidationPhysiqueN1_NeDependPasDeGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo { ForbidGetDetailAsync = true };
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        repo.ResetPdfPipelineCounters();
        var result = await svc.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, PhysiqueRequest);

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetMutationHeaderAsyncCallCount);
        Assert.Equal(1, repo.GetValidationsEntiteMinimalAsyncCallCount);
        Assert.Equal(1, repo.GetEmpreinteReadAsyncCallCount);
        Assert.Equal(1, repo.EnrichDemandeMapDetailShellAsyncCallCount);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, result.Statut);
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN1_GetDetailAsync_InterditPendantWorkflow()
    {
        var repo = new FakeDemandePaiementRepo { ForbidGetDetailAsync = true };
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var result = await svc.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, PhysiqueRequest);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, result.Statut);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN1_StatutEtValidationCorrects()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var result = await svc.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, PhysiqueRequest);

        Assert.Equal(StatutDemandePaiement.EnValidationN2, result.Statut);
        Assert.Equal("EN ATTENTE N2", result.CircuitEntiteStatut);
        var v = result.ValidationsEntite!.First(x => x.Niveau == ValidationEntiteNiveau.N1);
        Assert.Equal(ModeValidationEntite.Physique, v.ModeValidation);
        Assert.Equal("Jean Kabongo", v.NomSignatairePhysique);
        Assert.Equal("Chef service", v.FonctionSignatairePhysique);
        var tracked = await repo.GetTrackedAsync(created.IdDemandePaiement);
        Assert.NotNull(tracked!.ValidationsEntite.First(x => x.Niveau == ValidationEntiteNiveau.N1).EmpreinteDonnees);
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN1_AuditEnregistre()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        await svc.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, PhysiqueRequest);

        var audit = Assert.Single(repo.Audits.Where(a => a.Operation == "DECLARER_VALIDATION_PHYSIQUE_N1"));
        Assert.Equal(created.IdDemandePaiement, audit.IdEntite);
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN1_DtoComplet_ContientCollectionsEtCircuit()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(montantBrut: 250m));
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var before = await svc.GetByIdAsync(created.IdDemandePaiement);
        Assert.NotNull(before);

        var result = await svc.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, PhysiqueRequest);

        Assert.Equal(StatutDemandePaiement.EnValidationN2, result.Statut);
        Assert.Equal("EN ATTENTE N2", result.CircuitEntiteStatut);
        Assert.Equal(before.Reference, result.Reference);
        Assert.Equal(before.Objet, result.Objet);
        Assert.Equal(before.MontantBrut, result.MontantBrut);
        Assert.Equal(before.CodeUB, result.CodeUB);
        Assert.Equal(before.LibelleCasDossier, result.LibelleCasDossier);
        Assert.Equal(2, result.ValidationsEntite!.Count);
        Assert.Contains(result.ValidationsEntite, v => v.Niveau == 1 && v.Statut == "VALIDEE");
        Assert.Contains(result.ValidationsEntite, v => v.Niveau == 2 && v.Statut == "EN_ATTENTE");
        Assert.Equal(before.Beneficiaires.Count, result.Beneficiaires.Count);
        Assert.Equal(before.Imputations.Count, result.Imputations.Count);
        Assert.Equal(before.Pieces.Count, result.Pieces.Count);
        Assert.False(result.ModePaiementVerrouille);
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN1_DemandeurNonAutorise_Rejete()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var intrus = DemandePaiementTestData.CreateService(repo, [AppPermissions.PaiementsLire]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => intrus.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, PhysiqueRequest));
    }

}
