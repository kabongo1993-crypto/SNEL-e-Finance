using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Micro-lot 3.5-A — exposition lecture seule assignation sur DTO list/detail.</summary>
public class DemandePaiementAssignationDtoTests
{
    [Fact]
    public async Task Detail_Assignee_AUtilisateurCourant_RetourneIdEtIdentite()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        tracked.FK_UtilisateurAssigne = DemandePaiementRoutageTestHelpers.Y1;
        tracked.UtilisateurAssigne = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.Y1,
            Nom = "KABONGO",
            Prenom = "Christian",
            NomUtilisateur = "ckabongo",
            Actif = true,
        };

        var svc = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        var detail = await svc.GetByIdAsync(id);

        Assert.NotNull(detail);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, detail!.IdUtilisateurAssigne);
        Assert.Equal("KABONGO", detail.NomUtilisateurAssigne);
        Assert.Equal("Christian", detail.PrenomUtilisateurAssigne);
    }

    [Fact]
    public async Task Detail_Assignee_AAutreUtilisateur_RetourneIdentiteComplete()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        tracked.FK_UtilisateurAssigne = DemandePaiementRoutageTestHelpers.Y1;
        tracked.UtilisateurAssigne = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.Y1,
            Nom = "Dupont",
            Prenom = "Jean",
            NomUtilisateur = "jdupont",
            Actif = true,
        };

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var detail = await admin.GetByIdAsync(id);

        Assert.NotNull(detail);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, detail!.IdUtilisateurAssigne);
        Assert.Equal("Dupont", detail.NomUtilisateurAssigne);
        Assert.Equal("Jean", detail.PrenomUtilisateurAssigne);
    }

    [Fact]
    public async Task Detail_SansAssignation_RetourneTroisNull()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var svc = DemandePaiementRoutageTestHelpers.Charge(repo);
        var detail = await svc.GetByIdAsync(id);

        Assert.NotNull(detail);
        Assert.Null(detail!.IdUtilisateurAssigne);
        Assert.Null(detail.NomUtilisateurAssigne);
        Assert.Null(detail.PrenomUtilisateurAssigne);
    }

    [Fact]
    public async Task Liste_Et_Detail_Coherents_SurAssignation()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        tracked.FK_UtilisateurAssigne = DemandePaiementRoutageTestHelpers.Y1;
        tracked.UtilisateurAssigne = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.Y1,
            Nom = "Martin",
            Prenom = "Paul",
            NomUtilisateur = "pmartin",
            Actif = true,
        };

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        var list = await y1.ListAsync(new DemandePaiementQuery(Scope: DemandePaiementListScope.ChargeDpm));
        var row = Assert.Single(list, r => r.IdDemandePaiement == id);
        var detail = await y1.GetByIdAsync(id);

        Assert.NotNull(detail);
        Assert.Equal(row.IdUtilisateurAssigne, detail!.IdUtilisateurAssigne);
        Assert.Equal(row.NomUtilisateurAssigne, detail.NomUtilisateurAssigne);
        Assert.Equal(row.PrenomUtilisateurAssigne, detail.PrenomUtilisateurAssigne);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, row.IdUtilisateurAssigne);
    }

    [Fact]
    public async Task Assignation_Y1_NestPasConsidereeCommeCourantePourY2()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        tracked.FK_UtilisateurAssigne = DemandePaiementRoutageTestHelpers.Y1;
        tracked.UtilisateurAssigne = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.Y1,
            Nom = "Charge",
            Prenom = "Un",
            NomUtilisateur = "y1",
            Actif = true,
        };

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        var detail = await y1.GetByIdAsync(id);

        Assert.NotNull(detail);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, detail!.IdUtilisateurAssigne);
        Assert.NotEqual(DemandePaiementRoutageTestHelpers.Y2, detail.IdUtilisateurAssigne);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2).GetByIdAsync(id));
    }

    [Fact]
    public async Task Scope_Existant_Continue_Fonctionner_AvecAssignation()
    {
        var repo = new FakeDemandePaiementRepo();
        await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var list = await charge.ListAsync(
            new DemandePaiementQuery(Scope: DemandePaiementListScope.ChargeDpm));

        Assert.NotEmpty(list);
        Assert.All(list, r => Assert.True(r.IdUtilisateurAssigne is null or > 0));
    }

    [Fact]
    public async Task Autorisation_Backend_Inchangee_DpmAssignee_AutreUtilisateur()
    {
        var repo = new FakeDemandePaiementRepo();
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(
            repo,
            DemandePaiementRoutageTestHelpers.Y1,
            DemandePaiementRoutageTestHelpers.Z1);

        var id = repo.Demandes.Single(d =>
            StatutDemandePaiement.Normaliser(d.Statut) == StatutDemandePaiement.EnControleBudgetaire).IdDemandePaiement;

        var z2 = DemandePaiementRoutageTestHelpers.Junior(repo, 302);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            z2.RetournerAsync(id, new RetourDemandePaiementRequest("Test", null)));
    }
}
