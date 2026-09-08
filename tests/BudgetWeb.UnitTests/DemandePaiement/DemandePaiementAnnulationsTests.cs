using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementAnnulationsTests
{
    private const long DemandeurId = DemandePaiementRoutageTestHelpers.X1;
    private const long N1Id = DemandePaiementRoutageTestHelpers.N1;
    private const long N2Id = DemandePaiementRoutageTestHelpers.N2;

    [Fact]
    public async Task AnnulerSoumission_AvantReception_Retourne_ValideeEntite()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo, DemandeurId);
        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var annulee = await admin.AnnulerSoumissionAsync(id);

        Assert.Equal(StatutDemandePaiement.ValideeEntite, annulee.Statut);
        Assert.Null(annulee.DateSoumission);
        var tracked = repo.Demandes.Single(d => d.IdDemandePaiement == id);
        Assert.Null(tracked.FK_UtilisateurSoumission);
        Assert.Null(tracked.DateSoumission);
        Assert.Contains("ANNULER_SOUMISSION", repo.Audits.Select(a => a.Operation));
    }

    [Fact]
    public async Task AnnulerSoumission_ApresReception_Interdit()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo, DemandeurId);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        await charge.ReceptionnerAsync(id);

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        await Assert.ThrowsAsync<DemandePaiementConcurrencyException>(() =>
            admin.AnnulerSoumissionAsync(id));
    }

    [Fact]
    public async Task AnnulerSoumission_AutreUtilisateur_Interdit()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo, DemandeurId);
        var autre = DemandePaiementRoutageTestHelpers.Demandeur(repo, 999);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            autre.AnnulerSoumissionAsync(id));
    }

    [Fact]
    public async Task AnnulerValidationN2_Electronique_Retourne_EnValidationN1()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var demandeur = DemandePaiementRoutageTestHelpers.Demandeur(repo, DemandeurId);
        var n1 = DemandePaiementRoutageTestHelpers.Service(repo, N1Id, AppPermissions.ResponsableServiceDemandeur);
        var n2 = DemandePaiementRoutageTestHelpers.Service(repo, N2Id, AppPermissions.ResponsableEntiteInitiatrice);

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await demandeur.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await n2.ValiderN2ElectroniqueAsync(created.IdDemandePaiement);

        var annulee = await n2.AnnulerValidationN2Async(created.IdDemandePaiement);

        Assert.Equal(StatutDemandePaiement.EnValidationN1, annulee.Statut);
        var v1 = annulee.ValidationsEntite!.First(x => x.Niveau == ValidationEntiteNiveau.N1);
        var v2 = annulee.ValidationsEntite!.First(x => x.Niveau == ValidationEntiteNiveau.N2);
        Assert.Equal(StatutValidationEntite.Validee, v1.Statut);
        Assert.Equal(StatutValidationEntite.EnAttente, v2.Statut);
    }

    [Fact]
    public async Task AnnulerValidationN1_Electronique_ApresN2_Retourne_Brouillon()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var demandeur = DemandePaiementRoutageTestHelpers.Demandeur(repo, DemandeurId);
        var n1 = DemandePaiementRoutageTestHelpers.Service(repo, N1Id, AppPermissions.ResponsableServiceDemandeur);
        var n2 = DemandePaiementRoutageTestHelpers.Service(repo, N2Id, AppPermissions.ResponsableEntiteInitiatrice);

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(demandeur, created.IdDemandePaiement);
        await demandeur.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await n2.ValiderN2ElectroniqueAsync(created.IdDemandePaiement);
        await n2.AnnulerValidationN2Async(created.IdDemandePaiement);

        var annulee = await n1.AnnulerValidationN1Async(created.IdDemandePaiement);

        Assert.Equal(StatutDemandePaiement.Brouillon, annulee.Statut);
        Assert.All(annulee.ValidationsEntite!, v =>
            Assert.Equal(StatutValidationEntite.EnAttente, v.Statut));
    }

    [Fact]
    public async Task AnnulerValidationN2_Physique_Cascade_Vers_Brouillon()
    {
        var repo = new FakeDemandePaiementRepo();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var demandeur = DemandePaiementRoutageTestHelpers.Demandeur(repo, DemandeurId);
        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await demandeur.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await demandeur.DeclarerValidationPhysiqueN1Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Resp N1", null, new DateOnly(2026, 3, 1), null));
        await DemandePaiementTestData.AddDocumentSigneSampleAsync(demandeur, created.IdDemandePaiement);
        await demandeur.DeclarerValidationPhysiqueN2Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest("Resp N2", null, new DateOnly(2026, 3, 2), null));

        var annulee = await demandeur.AnnulerValidationN2Async(created.IdDemandePaiement);

        Assert.Equal(StatutDemandePaiement.Brouillon, annulee.Statut);
        Assert.All(annulee.ValidationsEntite!, v =>
            Assert.Equal(StatutValidationEntite.EnAttente, v.Statut));
    }

    [Fact]
    public async Task AnnulerValidationN1_EnValidationN2_Retourne_EnValidationN1()
    {
        var repo = new FakeDemandePaiementRepo();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var demandeur = DemandePaiementRoutageTestHelpers.Demandeur(repo, DemandeurId);
        var n1 = DemandePaiementRoutageTestHelpers.Service(repo, N1Id, AppPermissions.ResponsableServiceDemandeur);

        var created = await demandeur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await demandeur.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        var annulee = await n1.AnnulerValidationN1Async(created.IdDemandePaiement);

        Assert.Equal(StatutDemandePaiement.EnValidationN1, annulee.Statut);
        var v1 = annulee.ValidationsEntite!.First(x => x.Niveau == ValidationEntiteNiveau.N1);
        Assert.Equal(StatutValidationEntite.EnAttente, v1.Statut);
    }
}
