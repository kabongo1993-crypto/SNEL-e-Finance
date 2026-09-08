using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementDeleteBrouillonTests
{
    [Fact]
    public async Task DeleteBrouillon_Supprime_Demande_Et_Enfants()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var storage = new InMemoryFileStorage();
        var svc = DemandePaiementTestData.CreateService(repo, fileStorage: storage);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);

        var piecePath = repo.Demandes[0].PiecesJointes.First().CheminRelatif;
        Assert.Single(repo.Demandes);
        Assert.True(await storage.ExistsAsync(piecePath));

        var deleted = await svc.DeleteBrouillonAsync(created.IdDemandePaiement);

        Assert.True(deleted);
        Assert.Empty(repo.Demandes);
        Assert.False(await storage.ExistsAsync(piecePath));
    }

    [Fact]
    public async Task DeleteBrouillon_Refuse_Statut_Soumise()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);

        var ex = await Assert.ThrowsAsync<DemandePaiementConcurrencyException>(() =>
            svc.DeleteBrouillonAsync(created.IdDemandePaiement));

        Assert.NotNull(ex.Message);
        Assert.Single(repo.Demandes);
    }

    [Fact]
    public async Task DeleteBrouillon_Refuse_Statut_A_Corriger()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        repo.Demandes[0].Statut = StatutDemandePaiement.ACorriger;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteBrouillonAsync(created.IdDemandePaiement));

        Assert.Contains("brouillon", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteBrouillon_Refuse_Sans_Permission_Ecrire()
    {
        var repo = new FakeDemandePaiementRepo();
        var adminSvc = DemandePaiementTestData.CreateService(repo);
        var created = await adminSvc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        var readOnlySvc = DemandePaiementTestData.CreateService(repo, [AppPermissions.PaiementsLire]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            readOnlySvc.DeleteBrouillonAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task DeleteBrouillon_Refuse_Hors_Perimetre()
    {
        var repo = new FakeDemandePaiementRepo();
        var adminSvc = DemandePaiementTestData.CreateService(repo);
        var created = await adminSvc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        var intrus = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 99,
                Permissions = [AppPermissions.PaiementsEcrire],
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            intrus.DeleteBrouillonAsync(created.IdDemandePaiement));
    }

    [Fact]
    public async Task DeleteBrouillon_Demande_Inexistante_Retourne_False()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var deleted = await svc.DeleteBrouillonAsync(999);

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteBrouillon_Concurrence_Statut_Modifie()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        repo.OnBeforeGetTracked = _ =>
        {
            repo.Demandes[0].Statut = StatutDemandePaiement.Soumise;
        };

        await Assert.ThrowsAsync<DemandePaiementConcurrencyException>(() =>
            svc.DeleteBrouillonAsync(created.IdDemandePaiement));

        Assert.Single(repo.Demandes);
    }

    [Fact]
    public async Task DeleteBrouillon_Rollback_Si_Persistance_Echoue()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        repo.FailSaveChanges = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteBrouillonAsync(created.IdDemandePaiement));

        Assert.Single(repo.Demandes);
    }
}
