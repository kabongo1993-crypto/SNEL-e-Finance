using BudgetWeb.API.Controllers.V1;
using BudgetWeb.Application.DpmConsultation;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Lot 3.6.6 — contrôleur HTTP (DemandePaiementsController) câblé au service réel
/// (pas de stub) pour routage, retours, IDOR et assignation.
/// </summary>
public class DemandePaiementLot366HttpTests
{
    private static DemandePaiementsController Ctrl(DemandePaiementService svc)
        => new(svc, NullLogger<DemandePaiementsController>.Instance);

    private static int HttpStatus(IActionResult result)
        => result switch
        {
            OkObjectResult => StatusCodes.Status200OK,
            NotFoundResult => StatusCodes.Status404NotFound,
            NoContentResult => StatusCodes.Status204NoContent,
            ObjectResult obj => obj.StatusCode ?? 0,
            _ => 0,
        };

    private static T UnwrapOk<T>(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    [Fact]
    public async Task Routage_AvecHistorique_Via_Controller_200_Et_Contrat()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.UtilisateursAssignes[DemandePaiementRoutageTestHelpers.Y1] = new Utilisateur
        {
            IdUtilisateur = DemandePaiementRoutageTestHelpers.Y1,
            Nom = "KABONGO",
            Prenom = "Christian",
            NomUtilisateur = "y1",
            Actif = true,
        };

        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);

        var result = await Ctrl(y1).Routage(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, HttpStatus(result));
        var rows = UnwrapOk<IReadOnlyList<DemandePaiementRoutageDto>>(result);
        var row = Assert.Single(rows);
        var routage = repo.Routages.Single(r => r.FK_DemandePaiement == id);

        Assert.Equal(DemandePaiementRoutageAction.Receptionner, row.Action);
        Assert.Equal(StatutDemandePaiement.Soumise, row.StatutSource);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, row.StatutCible);
        Assert.Equal(routage.FK_UtilisateurSource, row.IdUtilisateurSource);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, row.IdUtilisateurCible);
        Assert.Equal("KABONGO", row.NomUtilisateurCible);
        Assert.Equal("Christian", row.PrenomUtilisateurCible);
        Assert.True(row.EstActif);
        Assert.True(row.DateRoutage > DateTime.MinValue);
        Assert.Null(row.Motif);
    }

    [Fact]
    public async Task Routage_SansHistorique_Via_Controller_200_ListeVide()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);

        var result = await Ctrl(charge).Routage(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, HttpStatus(result));
        Assert.Empty(UnwrapOk<IReadOnlyList<DemandePaiementRoutageDto>>(result));
    }

    [Fact]
    public async Task Routage_DemandeInexistante_Via_Controller_404()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);

        var result = await Ctrl(charge).Routage(999_999, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, HttpStatus(result));
    }

    [Fact]
    public async Task Routage_Y2_NonAutorise_Via_Controller_401_SansFuite()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        DemandePaiementRoutageTestHelpers.Tracked(repo, id).FK_UtilisateurAssigne =
            DemandePaiementRoutageTestHelpers.Y1;

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        Assert.Equal(StatusCodes.Status200OK, HttpStatus(await Ctrl(y1).Routage(id, CancellationToken.None)));

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        var denied = await Ctrl(y2).Routage(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, HttpStatus(denied));
        Assert.IsNotType<OkObjectResult>(denied);
    }

    [Fact]
    public async Task RetoursDestinataires_N2VersN1_Via_Controller_200()
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

        var result = await Ctrl(n1Svc).RetoursDestinataires(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, HttpStatus(result));
        var retour = Assert.Single(UnwrapOk<IReadOnlyList<DemandePaiementRetourDestinataireDto>>(result));
        Assert.Equal(DemandePaiementRetourType.N2VersN1, retour.TypeRetour);
        Assert.Equal(DemandePaiementRoutageTestHelpers.N1, retour.IdUtilisateurDestinataire);
        Assert.Equal("Validateur", retour.NomUtilisateurDestinataire);
    }

    [Fact]
    public async Task RetoursDestinataires_Plusieurs_Via_Controller_200()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

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
        var result = await Ctrl(admin).RetoursDestinataires(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, HttpStatus(result));
        var retours = UnwrapOk<IReadOnlyList<DemandePaiementRetourDestinataireDto>>(result);
        Assert.Equal(2, retours.Count);
        Assert.Equal(DemandePaiementRetourType.N2VersN1, retours[0].TypeRetour);
        Assert.Equal(DemandePaiementRetourType.N1VersDemandeur, retours[1].TypeRetour);
    }

    [Fact]
    public async Task RetoursDestinataires_Y2_NonAutorise_Via_Controller_401()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        DemandePaiementRoutageTestHelpers.Tracked(repo, id).FK_UtilisateurAssigne =
            DemandePaiementRoutageTestHelpers.Y1;

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        Assert.Equal(StatusCodes.Status200OK, HttpStatus(await Ctrl(y1).RetoursDestinataires(id, CancellationToken.None)));

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        var denied = await Ctrl(y2).RetoursDestinataires(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, HttpStatus(denied));
    }

    [Fact]
    public async Task GetById_Y1_Ok_Y2_401_Via_Controller()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);

        var ok = await Ctrl(y1).GetById(id, CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, HttpStatus(ok));
        Assert.NotNull(UnwrapOk<DemandePaiementDetailCompletDto>(ok).Demande);

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        var denied = await Ctrl(y2).GetById(id, CancellationToken.None);
        Assert.Equal(StatusCodes.Status401Unauthorized, HttpStatus(denied));
    }

    [Fact]
    public async Task Receptionner_Y2_Sur_Dpm_Y1_Via_Controller_401()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        var denied = await Ctrl(y2).Receptionner(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, HttpStatus(denied));
    }

    [Fact]
    public async Task ControleBudgetaire_Z2_Sur_Dpm_Z1_Via_Controller_401()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z2 = DemandePaiementRoutageTestHelpers.Junior(repo, 302);
        var denied = await Ctrl(z2).ControleBudgetaire(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, HttpStatus(denied));
    }

    [Fact]
    public async Task GetById_Ub_HorsPerimetre_Via_Controller_401()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbAutorisees.Add(10);
        repo.UbDepartements[20] = 2;
        repo.UbAutorisees.Add(20);

        var created = await DemandePaiementTestData.CreateService(repo).CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest());
        DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement).FK_UniteBudgetaire = 20;

        var demandeurUb10 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var denied = await Ctrl(demandeurUb10).GetById(created.IdDemandePaiement, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, HttpStatus(denied));
    }

    [Fact]
    public async Task GetById_Assignation_Nominative_Via_Controller_200()
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
            Nom = "Martin",
            Prenom = "Paul",
            NomUtilisateur = "y1",
            Actif = true,
        };

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        var result = await Ctrl(y1).GetById(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, HttpStatus(result));
        var detail = UnwrapOk<DemandePaiementDetailCompletDto>(result).Demande;
        Assert.NotNull(detail);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, detail!.IdUtilisateurAssigne);
        Assert.Equal("Martin", detail.NomUtilisateurAssigne);
        Assert.Equal("Paul", detail.PrenomUtilisateurAssigne);
    }

    [Fact]
    public async Task GetById_Pool_SansAssignation_Via_Controller_200()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        var result = await Ctrl(y1).GetById(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, HttpStatus(result));
        var detail = UnwrapOk<DemandePaiementDetailCompletDto>(result).Demande;
        Assert.NotNull(detail);
        Assert.Null(detail!.IdUtilisateurAssigne);
        Assert.Null(detail.NomUtilisateurAssigne);
    }
}
