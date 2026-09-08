using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Vague 2b-4 - DeclarerValidationPhysique N2 via lectures ciblees (sans GetDetailAsync initial).</summary>
public class DeclarerPhysiqueN2MutationReadTests
{
    private static readonly DeclarationValidationPhysiqueRequest N1PhysiqueRequest =
        new("Resp N1", null, new DateOnly(2026, 3, 1), null);

    private static readonly DeclarationValidationPhysiqueRequest N2PhysiqueRequest =
        new("Resp N2", null, new DateOnly(2026, 3, 2), null);

    private static async Task<long> PrepareEnValidationN2Async(
        FakeDemandePaiementRepo repo,
        bool n1Electronique = false,
        bool addDocumentSigne = false)
    {
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        if (n1Electronique)
        {
            await DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableServiceDemandeur)
                .ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        }
        else
        {
            await svc.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, N1PhysiqueRequest);
        }

        if (addDocumentSigne)
            await DemandePaiementTestData.AddDocumentSigneSampleAsync(svc, created.IdDemandePaiement);

        return created.IdDemandePaiement;
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN2_NeDependPasDeGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo { ForbidGetDetailAsync = true };
        var id = await PrepareEnValidationN2Async(repo, addDocumentSigne: true);
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);

        repo.ResetPdfPipelineCounters();
        var result = await svc.DeclarerValidationPhysiqueN2Async(id, N2PhysiqueRequest);

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetMutationHeaderAsyncCallCount);
        Assert.Equal(1, repo.GetValidationsEntiteN2AsyncCallCount);
        Assert.Equal(1, repo.GetEmpreinteReadAsyncCallCount);
        Assert.Equal(1, repo.EnrichDemandeMapDetailShellAsyncCallCount);
        Assert.Equal(StatutDemandePaiement.ValideeEntite, result.Statut);
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN2_StatutEtValidationCorrects()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await PrepareEnValidationN2Async(repo, addDocumentSigne: true);
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);

        var result = await svc.DeclarerValidationPhysiqueN2Async(id, N2PhysiqueRequest);

        Assert.Equal(StatutDemandePaiement.ValideeEntite, result.Statut);
        var v = result.ValidationsEntite!.First(x => x.Niveau == ValidationEntiteNiveau.N2);
        Assert.Equal(ModeValidationEntite.Physique, v.ModeValidation);
        Assert.Equal("Resp N2", v.NomSignatairePhysique);
        var tracked = await repo.GetTrackedAsync(id);
        Assert.NotNull(tracked!.ValidationsEntite.First(x => x.Niveau == ValidationEntiteNiveau.N2).EmpreinteDonnees);
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN2_AuditEnregistre()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await PrepareEnValidationN2Async(repo, addDocumentSigne: true);
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);

        await svc.DeclarerValidationPhysiqueN2Async(id, N2PhysiqueRequest);

        Assert.Contains("DECLARER_VALIDATION_PHYSIQUE_N2", repo.Audits.Select(a => a.Operation));
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN2_DtoComplet_ContientCollectionsEtCircuit()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(montantBrut: 250m));
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await svc.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, N1PhysiqueRequest);
        await DemandePaiementTestData.AddDocumentSigneSampleAsync(svc, created.IdDemandePaiement);

        var before = await svc.GetByIdAsync(created.IdDemandePaiement);
        Assert.NotNull(before);

        var result = await svc.DeclarerValidationPhysiqueN2Async(created.IdDemandePaiement, N2PhysiqueRequest);

        Assert.Equal(StatutDemandePaiement.ValideeEntite, result.Statut);
        Assert.Equal(before.Reference, result.Reference);
        Assert.Equal(before.Objet, result.Objet);
        Assert.Equal(before.MontantBrut, result.MontantBrut);
        Assert.Equal(before.CodeUB, result.CodeUB);
        Assert.Equal(before.LibelleCasDossier, result.LibelleCasDossier);
        Assert.Equal(2, result.ValidationsEntite!.Count);
        Assert.Contains(result.ValidationsEntite, v => v.Niveau == 1 && v.Statut == "VALIDEE");
        Assert.Contains(result.ValidationsEntite, v => v.Niveau == 2 && v.Statut == "VALIDEE");
        Assert.Equal(before.Beneficiaires.Count, result.Beneficiaires.Count);
        Assert.Equal(before.Imputations.Count, result.Imputations.Count);
        Assert.Equal(before.Pieces.Count, result.Pieces.Count);
        Assert.False(result.ModePaiementVerrouille);
    }

    [Fact]
    public async Task CasA_N1Electronique_N2Physique_DocumentSignePresent_Succes()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await PrepareEnValidationN2Async(repo, n1Electronique: true, addDocumentSigne: true);
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);

        var result = await svc.DeclarerValidationPhysiqueN2Async(id, N2PhysiqueRequest);

        Assert.Equal(StatutDemandePaiement.ValideeEntite, result.Statut);
    }

    [Fact]
    public async Task CasB_N1Physique_N2Physique_DocumentSigneAbsent_Rejet()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await PrepareEnValidationN2Async(repo, addDocumentSigne: false);
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => svc.DeclarerValidationPhysiqueN2Async(id, N2PhysiqueRequest));

        Assert.Contains("DOCUMENT_DPM_SIGNE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CasC_N1NonValidee_Rejet()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeclarerValidationPhysiqueN2Async(created.IdDemandePaiement, N2PhysiqueRequest));
    }

    [Fact]
    public async Task CasD_N2PasEnAttente_Rejet()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await PrepareEnValidationN2Async(repo, n1Electronique: true, addDocumentSigne: true);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableEntiteInitiatrice)
            .ValiderN2ElectroniqueAsync(id);

        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        await Assert.ThrowsAnyAsync<Exception>(
            () => svc.DeclarerValidationPhysiqueN2Async(id, N2PhysiqueRequest));
    }

    [Fact]
    public async Task DeclarerValidationPhysiqueN2_EmpreinteEnregistree_IdentiqueAuDetailQuery()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await svc.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, N1PhysiqueRequest);
        await DemandePaiementTestData.AddDocumentSigneSampleAsync(svc, created.IdDemandePaiement);
        var id = created.IdDemandePaiement;

        var detail = await repo.GetDetailAsync(id)
            ?? throw new InvalidOperationException("Demande introuvable.");
        var attendue = DemandePaiementEmpreinte.Calculer(detail);

        await svc.DeclarerValidationPhysiqueN2Async(id, N2PhysiqueRequest);

        var tracked = await repo.GetTrackedAsync(id);
        var empreinteEnregistree = tracked!.ValidationsEntite.First(x => x.Niveau == ValidationEntiteNiveau.N2).EmpreinteDonnees;
        Assert.Equal(attendue, empreinteEnregistree, ignoreCase: true);
    }
}

public class DeclarerPhysiqueN2EmpreinteParityTests
{
    [Fact]
    public async Task CasA_SansBeneficiaire_EmpreinteIdentique()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await AssertEmpreinteParityAsync(repo, created.IdDemandePaiement);
    }

    [Fact]
    public async Task CasE_DocumentSigneExclu_EmpreinteIdentique()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await svc.DeclarerValidationPhysiqueN1Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Resp N1", null, new DateOnly(2026, 3, 1), null));
        await DemandePaiementTestData.AddDocumentSigneSampleAsync(svc, created.IdDemandePaiement);
        await AssertEmpreinteParityAsync(repo, created.IdDemandePaiement);
    }

    [Fact]
    public async Task CasF_PiecesMetierModifiees_EmpreinteIdentiqueAuDetailQuery()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await AssertEmpreinteParityAsync(repo, created.IdDemandePaiement);
    }

    private static async Task AssertEmpreinteParityAsync(FakeDemandePaiementRepo repo, long idDemande)
    {
        var viaDetail = await repo.GetDetailAsync(idDemande)
            ?? throw new InvalidOperationException("Demande introuvable.");
        var viaEmpreinteRead = await repo.GetEmpreinteReadAsync(idDemande)
            ?? throw new InvalidOperationException("Empreinte read introuvable.");

        Assert.Equal(
            DemandePaiementEmpreinte.Calculer(viaDetail),
            DemandePaiementEmpreinte.Calculer(viaEmpreinteRead));
    }
}
