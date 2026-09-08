using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using BudgetWeb.Infrastructure.Documents;
using System.Text;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class ValidationEntiteCircuitTests
{
    [Fact]
    public async Task Initiation_Cree_Brouillon()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        Assert.Equal(StatutDemandePaiement.Brouillon, created.Statut);
    }

    [Fact]
    public async Task Impression_Apres_Initiation()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var pdf = await svc.GenererDocumentPdfAsync(created.IdDemandePaiement);
        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Contains("IMPRIMER", repo.Audits.Select(a => a.Operation));
    }

    [Fact]
    public async Task Impression_Avec_Renderer_Reel_Produit_Pdf()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.ServiceDemandeur,
            renderer: new QuestPdfDemandePaiementRenderer());
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var pdf = await svc.GenererDocumentPdfAsync(created.IdDemandePaiement);
        Assert.True(pdf.Length > 8_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task Soumettre_Sans_N1_Interdit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SoumettreAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task Soumettre_Sans_N2_Interdit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var resp1 = DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableServiceDemandeur);
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await resp1.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.SoumettreAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task N2_Avant_N1_Interdit()
    {
        var repo = new FakeDemandePaiementRepo();
        var agent = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var resp2 = DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableEntiteInitiatrice);
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            resp2.ValiderN2ElectroniqueAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task N1_Electronique_Enregistre_Validateur()
    {
        var repo = new FakeDemandePaiementRepo();
        var agent = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var resp1 = DemandePaiementTestData.CreateService(repo, AppPermissions.ResponsableServiceDemandeur);
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        var n1 = await resp1.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        var v = n1.ValidationsEntite!.First(x => x.Niveau == ValidationEntiteNiveau.N1);
        Assert.Equal(ModeValidationEntite.Electronique, v.ModeValidation);
        Assert.Equal(StatutValidationEntite.Validee, v.Statut);
        Assert.NotNull(v.IdUtilisateurValidateur);
        Assert.Null(v.IdUtilisateurDeclarant);
    }

    [Fact]
    public async Task N2_Electronique_Puis_Soumise()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        var soumise = await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);
        Assert.Equal(StatutDemandePaiement.Soumise, soumise.Statut);
    }

    [Fact]
    public async Task Validation_Physique_Distincte_Signataire_Et_Declarant()
    {
        var repo = new FakeDemandePaiementRepo();
        var agent = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        var n1 = await agent.DeclarerValidationPhysiqueN1Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Jean Kabongo", "Chef service", new DateOnly(2026, 3, 1), null));
        var v = n1.ValidationsEntite!.First(x => x.Niveau == ValidationEntiteNiveau.N1);
        Assert.Equal(ModeValidationEntite.Physique, v.ModeValidation);
        Assert.Equal("Jean Kabongo", v.NomSignatairePhysique);
        Assert.NotNull(v.IdUtilisateurDeclarant);
        Assert.Null(v.IdUtilisateurValidateur);
    }

    [Fact]
    public async Task Validation_Physique_Sans_Signataire_Interdite()
    {
        var repo = new FakeDemandePaiementRepo();
        var agent = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            agent.DeclarerValidationPhysiqueN1Async(
                created.IdDemandePaiement,
                new DeclarationValidationPhysiqueRequest(" ", null, new DateOnly(2026, 3, 1), null)));
    }

    [Fact]
    public async Task Validation_Physique_Exige_Document_Signe_Avant_Validee_Entite()
    {
        var repo = new FakeDemandePaiementRepo();
        var agent = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await agent.DeclarerValidationPhysiqueN1Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Resp N1", null, new DateOnly(2026, 3, 1), null));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeclarerValidationPhysiqueN2Async(
                created.IdDemandePaiement,
                new DeclarationValidationPhysiqueRequest("Resp N2", null, new DateOnly(2026, 3, 2), null)));
    }

    [Fact]
    public async Task Validation_Physique_Avec_Document_Signe_Ok()
    {
        var repo = new FakeDemandePaiementRepo();
        var agent = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await agent.DeclarerValidationPhysiqueN1Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Resp N1", null, new DateOnly(2026, 3, 1), null));
        await DemandePaiementTestData.AddDocumentSigneSampleAsync(agent, created.IdDemandePaiement);
        var fin = await agent.DeclarerValidationPhysiqueN2Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Resp N2", null, new DateOnly(2026, 3, 2), null));
        Assert.Equal(StatutDemandePaiement.ValideeEntite, fin.Statut);
    }

    [Fact]
    public async Task Audit_Trace_Operations_Entite()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);
        var ops = repo.Audits.Select(a => a.Operation).ToList();
        Assert.Contains("ENVOYER_EN_VALIDATION_N1", ops);
        Assert.Contains("VALIDER_N1", ops);
        Assert.Contains("VALIDER_N2", ops);
        Assert.Contains("SOUMETTRE", ops);
    }
}
