using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class SoumettreMutationReadTests
{
    private static readonly DeclarationValidationPhysiqueRequest PhysiqueRequest =
        new("Jean Kabongo", "Chef service", new DateOnly(2026, 3, 1), null);

    private static async Task<long> PrepareValideeEntiteElectroniqueAsync(FakeDemandePaiementRepo repo)
    {
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableServiceDemandeur)
            .ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableEntiteInitiatrice)
            .ValiderN2ElectroniqueAsync(created.IdDemandePaiement);
        return created.IdDemandePaiement;
    }

    [Fact]
    public async Task Soumettre_NeDependPasDeGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await PrepareValideeEntiteElectroniqueAsync(repo);

        repo.ResetPdfPipelineCounters();
        repo.ForbidGetDetailAsync = true;
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var result = await svc.SoumettreAsync(id);

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetMutationHeaderAsyncCallCount);
        Assert.Equal(1, repo.GetValidationsEntiteSoumettreAsyncCallCount);
        Assert.Equal(1, repo.GetEmpreinteReadAsyncCallCount);
        Assert.Equal(StatutDemandePaiement.Soumise, result.Statut);
        Assert.Contains("SOUMETTRE", repo.Audits.Select(a => a.Operation));
    }

    [Fact]
    public async Task Soumettre_EmpreinteObsolete_RejeteCommeAvant()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await PrepareValideeEntiteElectroniqueAsync(repo);
        var row = repo.GetDemandeEntityForTest(id)!;
        row.Objet = "Objet modifie apres validation";

        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SoumettreAsync(id));
        Assert.Contains("depuis la validation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Soumettre_PieceObligatoireAbsente_MessageIdentique()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableServiceDemandeur)
            .ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableEntiteInitiatrice)
            .ValiderN2ElectroniqueAsync(created.IdDemandePaiement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SoumettreAsync(created.IdDemandePaiement));
        Assert.Contains("Facture fournisseur", ex.Message, StringComparison.Ordinal);
        Assert.Contains("manquante", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Soumettre_N1NonValidee_Rejete()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SoumettreAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task Soumettre_N2NonValidee_Rejete()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableServiceDemandeur)
            .ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SoumettreAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task Soumettre_ValidationPhysiqueSansDocumentSigne_Rejete()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await PrepareValideeEntiteElectroniqueAsync(repo);
        var row = repo.GetDemandeEntityForTest(id)!;
        foreach (var v in row.ValidationsEntite)
        {
            v.ModeValidation = ModeValidationEntite.Physique;
            v.Statut = StatutValidationEntite.Validee;
        }
        var pieces = row.PiecesJointes.ToList();
        pieces.RemoveAll(p =>
            string.Equals(p.CodeTypePiece, TypePieceJointeDpm.DocumentDpmSigne, StringComparison.OrdinalIgnoreCase));
        row.PiecesJointes = pieces;

        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SoumettreAsync(id));
        Assert.Contains("DOCUMENT_DPM_SIGNE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Soumettre_ValidationPhysiqueAvecDocumentSigne_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await svc.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, PhysiqueRequest);
        await DemandePaiementTestData.AddDocumentSigneSampleAsync(svc, created.IdDemandePaiement);
        await svc.DeclarerValidationPhysiqueN2Async(created.IdDemandePaiement, PhysiqueRequest);

        repo.ResetPdfPipelineCounters();
        var result = await svc.SoumettreAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.Soumise, result.Statut);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
    }

    [Fact]
    public async Task Soumettre_DtoComplet_ApresMutation()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await PrepareValideeEntiteElectroniqueAsync(repo);
        var before = await DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull).GetByIdAsync(id);
        Assert.NotNull(before);

        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var result = await svc.SoumettreAsync(id);

        Assert.Equal(before!.Reference, result.Reference);
        Assert.Equal(before.Objet, result.Objet);
        Assert.Equal(before.MontantBrut, result.MontantBrut);
        Assert.Equal(before.CodeUB, result.CodeUB);
        Assert.Equal(before.LibelleCasDossier, result.LibelleCasDossier);
        Assert.Equal(before.Beneficiaires.Count, result.Beneficiaires.Count);
        Assert.Equal(2, result.ValidationsEntite!.Count);
        Assert.All(result.ValidationsEntite, v => Assert.Equal(StatutValidationEntite.Validee, v.Statut));
        Assert.False(result.ModePaiementVerrouille);
    }
}

public class SoumettreEmpreinteParityTests
{
    [Fact]
    public async Task CasA_SansBeneficiaire_EmpreinteIdentique()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await AssertEmpreinteParityAsync(repo, created.IdDemandePaiement);
    }

    [Fact]
    public async Task CasE_DocumentSigneExclu_EmpreinteIdentique()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddDocumentSigneSampleAsync(svc, created.IdDemandePaiement);
        await AssertEmpreinteParityAsync(repo, created.IdDemandePaiement);
    }

    [Fact]
    public async Task CasF_PiecesMetierModifiees_EmpreinteIdentiqueAuDetailQuery()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await AssertEmpreinteParityAsync(repo, created.IdDemandePaiement);
    }

    private static async Task AssertEmpreinteParityAsync(FakeDemandePaiementRepo repo, long idDemande)
    {
        var viaDetail = await repo.GetDetailAsync(idDemande)
            ?? throw new InvalidOperationException("Detail introuvable.");
        var viaEmpreinteRead = await repo.GetEmpreinteReadAsync(idDemande)
            ?? throw new InvalidOperationException("Empreinte read introuvable.");

        Assert.Equal(
            DemandePaiementEmpreinte.Calculer(viaDetail),
            DemandePaiementEmpreinte.Calculer(viaEmpreinteRead));
    }
}