using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Durcissement GarantirAcces sur mutations — Lot 3.6.5.</summary>
public class DemandePaiementAccesMutationHardeningTests
{
    [Fact]
    public async Task Z1_Peut_Imputer_Dpm_Assignee()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z1 = DemandePaiementRoutageTestHelpers.Junior(repo);
        var imp = await z1.AddImputationAsync(id, DemandePaiementTestData.ImputationDc());
        Assert.True(imp.IdImputation > 0);
    }

    [Fact]
    public async Task Z2_Ne_Peut_Pas_Imputer_Dpm_Assignee_A_Z1()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z2 = DemandePaiementRoutageTestHelpers.Junior(repo, 302);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            z2.AddImputationAsync(id, DemandePaiementTestData.ImputationDc()));
    }

    [Fact]
    public async Task Y1_Peut_Supprimer_Son_Brouillon_Modifier()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var id = created.IdDemandePaiement;

        Assert.True(await x1.DeleteBrouillonAsync(id));
        Assert.Empty(repo.Demandes);
    }

    [Fact]
    public async Task X2_Ne_Peut_Pas_Supprimer_Brouillon_De_X1()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        var x2 = DemandePaiementRoutageTestHelpers.Service(
            repo,
            20,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            x2.DeleteBrouillonAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task Y2_Ne_Peut_Pas_Traiter_Document_Sur_Dpm_Assignee_Y1()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y2.EtablirBilletConversionAsync(
                id,
                new EtablirBilletConversionRequest(null, null, null)));
    }

    [Fact]
    public async Task Demandeur_Avec_Soumettre_Peut_Soumettre_ValideeEntite()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await admin.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await admin.ValiderN2ElectroniqueAsync(created.IdDemandePaiement);

        var soumise = await x1.SoumettreAsync(created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.Soumise, soumise.Statut);
    }

    [Fact]
    public async Task Imputer_Sans_Permission_Imputer_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var lecteur = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.Z1,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsControlerBudget]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            lecteur.AddImputationAsync(id, DemandePaiementTestData.ImputationDc()));
    }
}
