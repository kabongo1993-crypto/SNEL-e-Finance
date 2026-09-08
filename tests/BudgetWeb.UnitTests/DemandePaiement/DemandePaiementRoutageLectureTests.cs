using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Lot 3.6.2 — exposition lecture seule historique routage DPM.</summary>
public class DemandePaiementRoutageLectureTests
{
    [Fact]
    public async Task SansRoutage_Retourne_ListeVide()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var rows = await charge.GetRoutageAsync(id);

        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task UnRoutage_Expose_Action_Source_Cible_Date_Actif()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.Y1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.Y1,
            Nom = "KABONGO",
            Prenom = "Christian",
            NomUtilisateur = "ckabongo",
            Actif = true,
        };

        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        await charge.ReceptionnerAsync(id);

        var rows = await charge.GetRoutageAsync(id);
        var row = Assert.Single(rows!);

        Assert.Equal(DemandePaiementRoutageAction.Receptionner, row.Action);
        var routage = repo.Routages.Single(r => r.FK_DemandePaiement == id);
        Assert.Equal(routage.FK_UtilisateurSource, row.IdUtilisateurSource);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, row.IdUtilisateurCible);
        Assert.Equal("KABONGO", row.NomUtilisateurCible);
        Assert.Equal("Christian", row.PrenomUtilisateurCible);
        Assert.True(row.EstActif);
        Assert.True(row.DateRoutage > DateTime.MinValue);
    }

    [Fact]
    public async Task PlusieursRoutages_OrdreChronologique_AncienVersRecent()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        await charge.ReceptionnerAsync(id);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, id);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, id);
        await charge.TraiterChargeAsync(
            id,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await charge.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1));

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var rows = await admin.GetRoutageAsync(id);

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.Equal(DemandePaiementRoutageAction.Receptionner, rows[0].Action);
        Assert.Equal(DemandePaiementRoutageAction.Orienter, rows[1].Action);
        Assert.True(rows[0].DateRoutage <= rows[1].DateRoutage);
        Assert.True(rows[0].IdRoutage < rows[1].IdRoutage);
    }

    [Fact]
    public async Task Utilisateurs_Source_Et_Cible_IdentiteExposee()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.Y1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.Y1,
            Nom = "Charge",
            Prenom = "Un",
            NomUtilisateur = "y1",
            Actif = true,
        };
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.Z1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.Z1,
            Nom = "Junior",
            Prenom = "Un",
            NomUtilisateur = "z1",
            Actif = true,
        };

        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);

        var id = repo.Demandes.Single().IdDemandePaiement;
        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var rows = await admin.GetRoutageAsync(id);
        var orienter = Assert.Single(rows!, r => r.Action == DemandePaiementRoutageAction.Orienter);

        Assert.Equal("Charge", orienter.NomUtilisateurSource);
        Assert.Equal("Un", orienter.PrenomUtilisateurSource);
        Assert.Equal("Junior", orienter.NomUtilisateurCible);
        Assert.Equal("Un", orienter.PrenomUtilisateurCible);
    }

    [Fact]
    public async Task CibleSansUtilisateurResolu_ExposeIdNullEtNomsNull()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        repo.Routages.Add(new DemandePaiementRoutage
        {
            IdRoutage = 99,
            FK_DemandePaiement = id,
            FK_UtilisateurSource = DemandePaiementRoutageTestHelpers.Y1,
            FK_UtilisateurCible = 0,
            StatutSource = StatutDemandePaiement.Soumise,
            StatutCible = StatutDemandePaiement.EnTraitementDpm,
            Action = DemandePaiementRoutageAction.Receptionner,
            DateRoutage = DateTime.Now,
            EstActif = false,
        });

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var rows = await admin.GetRoutageAsync(id);
        var row = Assert.Single(rows!);

        Assert.Null(row.IdUtilisateurCible);
        Assert.Null(row.NomUtilisateurCible);
        Assert.Null(row.PrenomUtilisateurCible);
    }

    [Fact]
    public async Task Isolation_Y2_NePeutPasLireRoutage_DpmAssignee_Y1()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        tracked.FK_UtilisateurAssigne = DemandePaiementRoutageTestHelpers.Y1;

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        Assert.NotNull(await y1.GetRoutageAsync(id));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2).GetRoutageAsync(id));
    }

    [Fact]
    public async Task Lecture_MultipleRoutages_ChargeUtilisateursEnBatch()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        await charge.ReceptionnerAsync(id);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, id);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, id);
        await charge.TraiterChargeAsync(
            id,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await charge.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1));

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        Assert.Equal(0, repo.RoutagesLectureUtilisateurBatchCallCount);
        _ = await admin.GetRoutageAsync(id);

        Assert.Equal(1, repo.RoutagesLectureUtilisateurBatchCallCount);
    }

    [Fact]
    public async Task DemandeInexistante_Retourne_Null()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);

        var rows = await charge.GetRoutageAsync(999_999);

        Assert.Null(rows);
    }
}
