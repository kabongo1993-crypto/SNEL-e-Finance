using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementAssignationTests
{
    [Fact]
    public async Task N1_Rejet_Assigne_Demandeur_Et_Reset_Validations()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1 = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        await n1.RejeterValidationEntiteAsync(
            created.IdDemandePaiement,
            new RetourDemandePaiementRequest("Correction", null));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.ACorriger, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.X1, tracked.FK_UtilisateurAssigne);
        Assert.All(tracked.ValidationsEntite, v =>
            Assert.Equal(StatutValidationEntite.EnAttente, v.Statut));
    }

    [Fact]
    public async Task Assignation_Nulle_Conserve_Pool_Soumise()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Null(tracked.FK_UtilisateurAssigne);

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        Assert.NotNull(await y1.GetByIdAsync(id));
        Assert.NotNull(await y2.GetByIdAsync(id));
    }

    [Fact]
    public async Task Apres_ACorriger_EnvoyerEnValidation_Remet_Pool_Sans_Assigne()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1 = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        await n1.RejeterValidationEntiteAsync(
            created.IdDemandePaiement,
            new RetourDemandePaiementRequest("Manque de montant", null));

        Assert.Equal(
            DemandePaiementRoutageTestHelpers.X1,
            DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement).FK_UtilisateurAssigne);

        await x1.RemettreEnBrouillonAsync(created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, tracked.Statut);
        Assert.Null(tracked.FK_UtilisateurAssigne);

        var physique = await x1.DeclarerValidationPhysiqueN1Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest(
                "Signataire Test",
                "Responsable",
                DateOnly.FromDateTime(DateTime.Today),
                null));
        Assert.Equal(StatutDemandePaiement.EnValidationN2, physique.Statut);
    }

    [Fact]
    public async Task Createur_Assigne_Peut_Declarer_Validation_Physique_N1()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        // État historique : assigne resté sur le créateur en N1 après retour/correction.
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement);
        tracked.FK_UtilisateurAssigne = DemandePaiementRoutageTestHelpers.X1;

        var physique = await x1.DeclarerValidationPhysiqueN1Async(
            created.IdDemandePaiement,
            new DeclarationValidationPhysiqueRequest(
                "Signataire Test",
                "Responsable",
                DateOnly.FromDateTime(DateTime.Today),
                null));
        Assert.Equal(StatutDemandePaiement.EnValidationN2, physique.Statut);
    }

    [Fact]
    public async Task N1_Voit_Demande_Apres_Correction_Meme_Si_Assigne_Createur()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1 = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1.RejeterValidationEntiteAsync(
            created.IdDemandePaiement,
            new RetourDemandePaiementRequest("Manque de montant", null));
        await x1.RemettreEnBrouillonAsync(created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        // Simule l'ancien bug : assigne resté sur le créateur.
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement);
        tracked.FK_UtilisateurAssigne = DemandePaiementRoutageTestHelpers.X1;

        Assert.NotNull(await n1.GetByIdAsync(created.IdDemandePaiement));
        var list = await n1.ListAsync(new DemandePaiementQuery(Statut: StatutDemandePaiement.EnValidationN1));
        Assert.Contains(list, d => d.IdDemandePaiement == created.IdDemandePaiement);
    }
}
