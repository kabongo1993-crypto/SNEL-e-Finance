using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Autorisation unifiée DPM — Lot 3.2.</summary>
public class DemandePaiementAccesDemandeurTests
{
    private const long X1 = 10;
    private const long X2 = 20;
    private const long Y1 = 101;
    private const long Y2 = 102;

    [Fact]
    public async Task X1_Voit_Sa_Propre_Dpm()
    {
        var repo = SeedDemandeurRepo(creatorId: X1);
        var svc = ServiceDemandeur(repo, X1);
        var d = repo.Demandes.Single();

        var detail = await svc.GetByIdAsync(d.IdDemandePaiement);
        Assert.NotNull(detail);
    }

    [Fact]
    public async Task X1_Ne_Voit_Pas_Dpm_De_X2_Meme_Ub_Et_Perimetre()
    {
        var repo = SeedDemandeurRepo(creatorId: X1);
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.Brouillon, creatorId: X2));
        var svc = ServiceDemandeur(repo, X1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetByIdAsync(2));
    }

    [Fact]
    public async Task Y1_Voit_Dpm_Assignee_A_Y1()
    {
        var repo = SeedChargeRepo(assigneeId: Y1);
        var svc = ServiceCharge(repo, Y1);

        var detail = await svc.GetByIdAsync(1);
        Assert.NotNull(detail);
    }

    [Fact]
    public async Task Y2_Ne_Voit_Pas_Dpm_Assignee_A_Y1()
    {
        var repo = SeedChargeRepo(assigneeId: Y1);
        var svc = ServiceCharge(repo, Y2);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetByIdAsync(1));
    }

    [Fact]
    public async Task Admin_Voit_Dpm_Assignee_A_Y1()
    {
        var repo = SeedChargeRepo(assigneeId: Y1);
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);

        var detail = await svc.GetByIdAsync(1);
        Assert.NotNull(detail);
    }

    [Fact]
    public async Task Createur_X_Conserve_Lecture_En_Traitement()
    {
        var repo = SeedChargeRepo(assigneeId: Y1, statut: StatutDemandePaiement.EnTraitementDpm, creatorId: X1);
        var svc = ServiceDemandeur(repo, X1);

        var detail = await svc.GetByIdAsync(1);
        Assert.NotNull(detail);
    }

    [Fact]
    public async Task Createur_X_Ne_Peut_Pas_Retourner_Sans_Role_Metier()
    {
        var repo = SeedChargeRepo(
            assigneeId: Y1,
            statut: StatutDemandePaiement.EnTraitementDpm,
            creatorId: X1);
        var svc = ServiceDemandeur(repo, X1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.RetournerAsync(1, new RetourDemandePaiementRequest("Motif", null)));
    }

    [Fact]
    public async Task Y2_Ne_Peut_Pas_Retourner_Dpm_Assignee_A_Y1()
    {
        var repo = SeedChargeRepo(
            assigneeId: Y1,
            statut: StatutDemandePaiement.EnTraitementDpm);
        repo.SeedPieceObligatoire();
        var svc = ServiceCharge(repo, Y2);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.RetournerAsync(1, new RetourDemandePaiementRequest("Motif", null)));
    }

    [Fact]
    public async Task Y2_Ne_Peut_Pas_Prendre_En_Controle_Dpm_Inaccessible()
    {
        var repo = SeedJuniorRepo(assigneeId: Y1);
        var svc = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = Y2,
                Permissions = AppPermissions.JuniorDc,
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.PrendreEnControleAsync(1));
    }

    [Fact]
    public async Task Junior_Hors_Filiere_Ne_Peut_Pas_Controler()
    {
        var repo = SeedJuniorRepo(assigneeId: null, codeType: TypeBudgetCode.ActionsExploitation);
        var svc = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = Y1,
                Permissions = AppPermissions.JuniorDc,
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ControlerBudgetaireAsync(1));
    }

    [Fact]
    public async Task Junior_Hors_Filiere_Ne_Peut_Pas_Viser()
    {
        var repo = SeedJuniorRepo(assigneeId: null, codeType: TypeBudgetCode.BudgetInvestissement);
        var svc = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = Y1,
                Permissions = [.. AppPermissions.JuniorDc, AppPermissions.PaiementsViserBudget],
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ViserAsync(1));
    }

    [Fact]
    public async Task Liste_Et_Detail_Coherents_Pour_Isolation_Demandeur()
    {
        var repo = SeedDemandeurRepo(creatorId: X1);
        repo.Demandes.Add(MakeDemande(2, StatutDemandePaiement.Brouillon, creatorId: X2));
        var svc = ServiceDemandeur(repo, X1);

        var list = await svc.ListAsync(new DemandePaiementQuery(Scope: DemandePaiementListScope.MesDemandes));
        Assert.Single(list);
        Assert.Equal(1, list[0].IdDemandePaiement);

        var detailOk = await svc.GetByIdAsync(1);
        Assert.NotNull(detailOk);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetByIdAsync(2));
    }

    private static FakeDemandePaiementRepo SeedDemandeurRepo(long creatorId)
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbAutorisees.Add(10);
        repo.Demandes.Add(MakeDemande(1, StatutDemandePaiement.Brouillon, creatorId: creatorId));
        return repo;
    }

    private static FakeDemandePaiementRepo SeedChargeRepo(
        long assigneeId,
        string statut = StatutDemandePaiement.Soumise,
        long creatorId = X1)
    {
        var repo = new FakeDemandePaiementRepo();
        repo.UbDepartements[10] = 1;
        repo.UbAutorisees.Add(10);
        repo.UbProxyPrevision.Add(10);
        repo.Demandes.Add(MakeDemande(
            1,
            statut,
            creatorId: creatorId,
            assigneeId: assigneeId));
        return repo;
    }

    private static FakeDemandePaiementRepo SeedJuniorRepo(
        long? assigneeId,
        string codeType = TypeBudgetCode.DepensesCourantes)
    {
        var repo = new FakeDemandePaiementRepo();
        repo.UbDepartements[10] = 1;
        repo.UbAutorisees.Add(10);
        var d = MakeDemande(1, StatutDemandePaiement.EnControleBudgetaire, creatorId: X1, assigneeId: assigneeId);
        d.FK_TypeBudget = codeType switch
        {
            TypeBudgetCode.DepensesCourantes => 1,
            TypeBudgetCode.ActionsExploitation => 2,
            _ => 3,
        };
        d.TypeBudget = new Domain.Entities.TypeBudget { CodeType = codeType, IdTypeBudget = d.FK_TypeBudget.Value };
        repo.Demandes.Add(d);
        return repo;
    }

    private static DemandePaiementEntity MakeDemande(
        long id,
        string statut,
        long creatorId,
        long? assigneeId = null)
        => new()
        {
            IdDemandePaiement = id,
            Reference = $"DP-TEST-{id:D3}",
            Statut = statut,
            FK_UniteBudgetaire = 10,
            FK_UtilisateurCreation = creatorId,
            FK_UtilisateurAssigne = assigneeId,
            FK_Demandeur = 1,
            FK_ExerciceBudgetaire = 1,
            FK_CasDossier = 1,
            FK_TypeBudget = 1,
            Objet = "Test",
            MontantBrut = 100m,
            Devise = "USD",
            DateEmission = new DateOnly(2026, 1, 1),
            DateCreation = DateTime.Now,
            TypeBudget = new Domain.Entities.TypeBudget { IdTypeBudget = 1, CodeType = TypeBudgetCode.DepensesCourantes },
        };

    private static DemandePaiementService ServiceDemandeur(FakeDemandePaiementRepo repo, long userId)
        => DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = userId,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });

    private static DemandePaiementService ServiceCharge(FakeDemandePaiementRepo repo, long userId)
        => DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = userId,
                Permissions = AppPermissions.ChargeDpm,
            });
}
