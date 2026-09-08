using BudgetWeb.Application.DpmConsultation;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Lot 3.6.3 — projection lecture seule des destinataires de retour depuis le routage.</summary>
public class DemandePaiementRetourDestinataireTests
{
    [Fact]
    public async Task AucunRoutageRetour_Retourne_ListeVide()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var retours = await charge.GetRetoursDestinatairesAsync(id);

        Assert.NotNull(retours);
        Assert.Empty(retours);
    }

    [Fact]
    public async Task N2_Vers_N1_Destinataire_ValidateurN1()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.N1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.N1,
            Nom = "Validateur",
            Prenom = "N1",
            NomUtilisateur = "n1",
            Actif = true,
        };

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var n2Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N2,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1Svc.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        var id = created.IdDemandePaiement;
        await n2Svc.RejeterValidationEntiteAsync(id, new RetourDemandePaiementRequest("Correction", null));

        var retour = Assert.Single(await n1Svc.GetRetoursDestinatairesAsync(id)!);

        Assert.Equal(DemandePaiementRetourType.N2VersN1, retour.TypeRetour);
        Assert.Equal(DemandePaiementRoutageTestHelpers.N1, retour.IdUtilisateurDestinataire);
        Assert.Equal("Validateur", retour.NomUtilisateurDestinataire);
        Assert.Equal("N1", retour.PrenomUtilisateurDestinataire);
        AssertCoherentAvecRoutage(repo, id, retour);
    }

    [Fact]
    public async Task N1_Vers_X_Destinataire_Demandeur()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.X1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.X1,
            Nom = "Demandeur",
            Prenom = "X",
            NomUtilisateur = "x1",
            Actif = true,
        };

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement).FK_UtilisateurCreation =
            DemandePaiementRoutageTestHelpers.X1;
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var id = created.IdDemandePaiement;
        await n1Svc.RejeterValidationEntiteAsync(id, new RetourDemandePaiementRequest("Correction", null));

        var retour = Assert.Single(await x1.GetRetoursDestinatairesAsync(id)!);

        Assert.Equal(DemandePaiementRetourType.N1VersDemandeur, retour.TypeRetour);
        Assert.Equal(DemandePaiementRoutageTestHelpers.X1, retour.IdUtilisateurDestinataire);
        Assert.Equal("Demandeur", retour.NomUtilisateurDestinataire);
        AssertCoherentAvecRoutage(repo, id, retour);
    }

    [Fact]
    public async Task Y_Vers_X_Destinataire_Demandeur()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.X1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.X1,
            Nom = "Kabongo",
            Prenom = "Jean",
            NomUtilisateur = "x1",
            Actif = true,
        };

        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);
        await y1.RetournerAsync(id, new RetourDemandePaiementRequest("Correction demandeur", null));

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var retour = Assert.Single(await x1.GetRetoursDestinatairesAsync(id)!);

        Assert.Equal(DemandePaiementRetourType.ChargeVersDemandeur, retour.TypeRetour);
        Assert.Equal(DemandePaiementRoutageAction.RetourDemandeur, retour.ActionRoutage);
        Assert.Equal(DemandePaiementRoutageTestHelpers.X1, retour.IdUtilisateurDestinataire);
        Assert.Equal("Kabongo", retour.NomUtilisateurDestinataire);
        AssertCoherentAvecRoutage(repo, id, retour);
    }

    [Fact]
    public async Task Z_Vers_Y1_Destinataire_ChargeDp()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.Y1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.Y1,
            Nom = "Martin",
            Prenom = "Paul",
            NomUtilisateur = "y1",
            Actif = true,
        };

        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.RetournerAsync(id, new RetourDemandePaiementRequest("Correction imputation", null));

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        var retour = Assert.Single(await y1.GetRetoursDestinatairesAsync(id)!);

        Assert.Equal(DemandePaiementRetourType.JuniorVersCharge, retour.TypeRetour);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, retour.IdUtilisateurDestinataire);
        Assert.Equal("Martin", retour.NomUtilisateurDestinataire);
        AssertCoherentAvecRoutage(repo, id, retour);
    }

    [Fact]
    public async Task V_Vers_Z_Destinataire_Controleur()
    {
        var repo = new FakeDemandePaiementRepo();
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

        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.AddImputationAsync(id, DemandePaiementTestData.ImputationDc(montantUsd: 5_000m, idBudgetLigne: 100));

        var v = DemandePaiementRoutageTestHelpers.Viseur(repo);
        await v.RetournerAsync(id, new RetourDemandePaiementRequest("Rejet visa", null));

        var retour = Assert.Single(await z.GetRetoursDestinatairesAsync(id)!);

        Assert.Equal(DemandePaiementRetourType.VisaVersControle, retour.TypeRetour);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, retour.IdUtilisateurDestinataire);
        Assert.Equal("Junior", retour.NomUtilisateurDestinataire);
        AssertCoherentAvecRoutage(repo, id, retour);
    }

    [Fact]
    public async Task PlusieursRetours_Retournent_ChaqueDestinataire()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.N1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.N1,
            Nom = "N1",
            Prenom = "Validateur",
            NomUtilisateur = "n1",
            Actif = true,
        };
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.X1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.X1,
            Nom = "Demandeur",
            Prenom = "X",
            NomUtilisateur = "x1",
            Actif = true,
        };

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var n2Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N2,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement).FK_UtilisateurCreation =
            DemandePaiementRoutageTestHelpers.X1;
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1Svc.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        var id = created.IdDemandePaiement;
        await n2Svc.RejeterValidationEntiteAsync(id, new RetourDemandePaiementRequest("Retour N2", null));
        await n1Svc.RejeterValidationEntiteAsync(id, new RetourDemandePaiementRequest("Retour N1", null));

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var retours = await admin.GetRetoursDestinatairesAsync(id);

        Assert.NotNull(retours);
        Assert.Equal(2, retours!.Count);
        Assert.Equal(DemandePaiementRetourType.N2VersN1, retours[0].TypeRetour);
        Assert.Equal(DemandePaiementRetourType.N1VersDemandeur, retours[1].TypeRetour);
        Assert.Equal(DemandePaiementRoutageTestHelpers.N1, retours[0].IdUtilisateurDestinataire);
        Assert.Equal(DemandePaiementRoutageTestHelpers.X1, retours[1].IdUtilisateurDestinataire);
    }

    [Fact]
    public async Task UtilisateurIntrouvable_ConserveId_IdentiteNull()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        const long idInconnu = 9_999;
        repo.Routages.Add(new DemandePaiementRoutage
        {
            IdRoutage = 50,
            FK_DemandePaiement = id,
            FK_UtilisateurSource = DemandePaiementRoutageTestHelpers.Y1,
            FK_UtilisateurCible = idInconnu,
            StatutSource = StatutDemandePaiement.EnTraitementDpm,
            StatutCible = StatutDemandePaiement.ACorriger,
            Action = DemandePaiementRoutageAction.RetourDemandeur,
            DateRoutage = DateTime.Now,
            EstActif = false,
            Motif = "Test",
        });

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var retour = Assert.Single(await admin.GetRetoursDestinatairesAsync(id)!);

        Assert.Equal(idInconnu, retour.IdUtilisateurDestinataire);
        Assert.Null(retour.NomUtilisateurDestinataire);
        Assert.Null(retour.PrenomUtilisateurDestinataire);
    }

    [Fact]
    public async Task Isolation_Y2_NePeutPasLireDestinataires_DpmAssignee_Y1()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        tracked.FK_UtilisateurAssigne = DemandePaiementRoutageTestHelpers.Y1;

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        Assert.NotNull(await y1.GetRetoursDestinatairesAsync(id));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2)
                .GetRetoursDestinatairesAsync(id));
    }

    [Fact]
    public void ClassifierTypeRetour_CouvreLesCinqCas()
    {
        Assert.Equal(
            DemandePaiementRetourType.N2VersN1,
            DemandePaiementRetourLecture.ClassifierTypeRetour(
                DemandePaiementRoutageAction.RejetValidationEntite,
                StatutDemandePaiement.EnValidationN2,
                StatutDemandePaiement.EnValidationN1));
        Assert.Equal(
            DemandePaiementRetourType.N1VersDemandeur,
            DemandePaiementRetourLecture.ClassifierTypeRetour(
                DemandePaiementRoutageAction.RejetValidationEntite,
                StatutDemandePaiement.EnValidationN1,
                StatutDemandePaiement.ACorriger));
        Assert.Equal(
            DemandePaiementRetourType.ChargeVersDemandeur,
            DemandePaiementRetourLecture.ClassifierTypeRetour(
                DemandePaiementRoutageAction.RetourDemandeur,
                StatutDemandePaiement.EnTraitementDpm,
                StatutDemandePaiement.ACorriger));
        Assert.Equal(
            DemandePaiementRetourType.JuniorVersCharge,
            DemandePaiementRetourLecture.ClassifierTypeRetour(
                DemandePaiementRoutageAction.RetourInterEtapes,
                StatutDemandePaiement.EnControleBudgetaire,
                StatutDemandePaiement.EnTraitementDpm));
        Assert.Equal(
            DemandePaiementRetourType.VisaVersControle,
            DemandePaiementRetourLecture.ClassifierTypeRetour(
                DemandePaiementRoutageAction.RetourVisa,
                StatutDemandePaiement.EnControleBudgetaire,
                StatutDemandePaiement.EnControleBudgetaire));
    }

    private static void AssertCoherentAvecRoutage(
        FakeDemandePaiementRepo repo,
        long idDemande,
        DemandePaiementRetourDestinataireDto retour)
    {
        var routage = repo.Routages.Single(r => r.IdRoutage == retour.IdRoutage && r.FK_DemandePaiement == idDemande);
        Assert.Equal(routage.FK_UtilisateurCible, retour.IdUtilisateurDestinataire);
        Assert.Equal(routage.Action, retour.ActionRoutage);
        Assert.Equal(routage.Motif, retour.Motif);
    }
}
