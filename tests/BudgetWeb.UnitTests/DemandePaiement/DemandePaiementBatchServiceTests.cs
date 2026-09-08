using BudgetWeb.API.Controllers.V1;
using BudgetWeb.API.Models;
using BudgetWeb.Application.DpmBatch;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementBatchServiceTests
{
    private static DemandePaiementBatchService CreateBatch(DemandePaiementService svc, IDemandePaiementRepository repo)
        => new(svc, repo, NullLogger<DemandePaiementBatchService>.Instance);

    private static DemandePaiementBatchRequest Req(string statut, params long[] ids)
        => new(statut, ids.Select(id => new DemandePaiementBatchItemRequest(id)).ToList());

    private static async Task<(FakeDemandePaiementRepo Repo, DemandePaiementService Svc, long Id)> CreateBrouillonReadyAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        return (repo, svc, created.IdDemandePaiement);
    }

    [Fact]
    public void ValidateRequest_StatutFiltreVide_Refuse()
    {
        Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.EnvoyerValidation,
                Req("", 1)));
    }

    [Fact]
    public void ValidateRequest_FiltreIncoherent_Refuse()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.ValiderN1,
                Req(StatutDemandePaiement.Brouillon, 1)));
        Assert.Contains("incompatible", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRequest_ListeVide_Refuse()
    {
        Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.EnvoyerValidation,
                new DemandePaiementBatchRequest(StatutDemandePaiement.Brouillon, [])));
    }

    [Fact]
    public void ValidateRequest_PlusDe100_Refuse()
    {
        var ids = Enumerable.Range(1, 101).Select(i => new DemandePaiementBatchItemRequest(i)).ToList();
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.EnvoyerValidation,
                new DemandePaiementBatchRequest(StatutDemandePaiement.Brouillon, ids)));
        Assert.Contains("100", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRequest_IdsDupliques_Refuse()
    {
        Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.EnvoyerValidation,
                Req(StatutDemandePaiement.Brouillon, 1, 1)));
    }

    [Fact]
    public async Task EnvoyerValidation_BatchHomogene_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var b = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Autre"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, b.IdDemandePaiement);

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, a.IdDemandePaiement, b.IdDemandePaiement));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(0, result.Erreurs);
        Assert.Equal(0, result.Ignorees);
        Assert.Equal(2, result.Traitees);
        Assert.NotEqual(Guid.Empty, result.CorrelationId);
        Assert.All(result.Details, d => Assert.Equal(DemandePaiementBatchOutcome.Success, d.Outcome));
        Assert.Contains(repo.Audits, x => x.Operation == "ENVOYER_EN_VALIDATION_N1" && x.IdEntite == a.IdDemandePaiement);
        Assert.Contains(repo.Audits, x => x.Operation == "ENVOYER_EN_VALIDATION_N1" && x.IdEntite == b.IdDemandePaiement);
    }

    [Fact]
    public async Task EnvoyerValidation_MauvaisStatut_Ignore()
    {
        var (repo, svc, idOk) = await CreateBrouillonReadyAsync();
        var deja = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Déjà envoyée"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, deja.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(deja.IdDemandePaiement);

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, idOk, deja.IdDemandePaiement));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(0, result.Erreurs);
        var ignore = Assert.Single(result.Details, d => d.Outcome == DemandePaiementBatchOutcome.Ignored);
        Assert.Equal(DemandePaiementBatchErrorCode.StatutMismatch, ignore.CodeErreur);
        Assert.Equal(deja.IdDemandePaiement, ignore.IdDemandePaiement);
    }

    [Fact]
    public async Task EnvoyerValidation_Introuvable_ErrorNotFound()
    {
        var (repo, svc, idOk) = await CreateBrouillonReadyAsync();
        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, idOk, 999_999));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        var err = Assert.Single(result.Details, d => d.Outcome == DemandePaiementBatchOutcome.Error);
        Assert.Equal(DemandePaiementBatchErrorCode.NotFound, err.CodeErreur);
    }

    [Fact]
    public async Task EnvoyerValidation_SansPermission_ErrorAccessDenied()
    {
        var (repo, _, id) = await CreateBrouillonReadyAsync();
        var lecteur = DemandePaiementTestData.CreateService(repo, [AppPermissions.PaiementsLire]);
        var result = await CreateBatch(lecteur, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, id));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.Brouillon, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
    }

    [Fact]
    public async Task EnvoyerValidation_LotMixte_AccesAutoriseEtRefuse_IsoleEtHttp200()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);

        var userA = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 1,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });
        var userB = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 2,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });

        var dpmOk = await userA.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Accessible"));
        await DemandePaiementTestData.AddSamplePieceAsync(userA, dpmOk.IdDemandePaiement);

        var dpmRefusee = await userB.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Inaccessible"));
        await DemandePaiementTestData.AddSamplePieceAsync(userB, dpmRefusee.IdDemandePaiement);

        var ctrl = new DemandePaiementsBatchController(CreateBatch(userA, repo));
        var http = await ctrl.EnvoyerValidation(
            Req(StatutDemandePaiement.Brouillon, dpmOk.IdDemandePaiement, dpmRefusee.IdDemandePaiement),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(http);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var result = Assert.IsType<DemandePaiementBatchResultDto>(okResult.Value);

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(0, result.Ignorees);

        var ok = Assert.Single(result.Details, d => d.IdDemandePaiement == dpmOk.IdDemandePaiement);
        Assert.Equal(DemandePaiementBatchOutcome.Success, ok.Outcome);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, ok.StatutApres);
        Assert.Equal(
            StatutDemandePaiement.EnValidationN1,
            repo.Demandes.Single(d => d.IdDemandePaiement == dpmOk.IdDemandePaiement).Statut);
        Assert.Contains(repo.Audits, a =>
            a.Operation == "ENVOYER_EN_VALIDATION_N1" && a.IdEntite == dpmOk.IdDemandePaiement);

        var refusee = Assert.Single(result.Details, d => d.IdDemandePaiement == dpmRefusee.IdDemandePaiement);
        Assert.Equal(DemandePaiementBatchOutcome.Error, refusee.Outcome);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, refusee.CodeErreur);
        Assert.Equal(
            StatutDemandePaiement.Brouillon,
            repo.Demandes.Single(d => d.IdDemandePaiement == dpmRefusee.IdDemandePaiement).Statut);
        Assert.DoesNotContain(repo.Audits, a =>
            a.Operation == "ENVOYER_EN_VALIDATION_N1" && a.IdEntite == dpmRefusee.IdDemandePaiement);
    }

    [Fact]
    public async Task ValiderN1_HorsPerimetreUb_ErrorAccessDenied()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbDepartements[20] = 2;

        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        // DPM passée hors UB autorisée pour le validateur N1 (acteur métier : UB contrôlée).
        var row = repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement);
        row.FK_UniteBudgetaire = 20;
        // Force le rechargement de la navigation UB hors périmètre.
        row.UniteBudgetaire = new BudgetWeb.Domain.Entities.UniteBudgetaire
        {
            IdUB = 20,
            CodeUB = "UB020",
            Libelle = "UB hors périmètre",
            FK_Departement = 2,
            Actif = true,
        };

        var n1 = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var result = await CreateBatch(n1, repo).ExecuterAsync(
            DemandePaiementBatchOperation.ValiderN1,
            Req(StatutDemandePaiement.EnValidationN1, created.IdDemandePaiement));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, row.Statut);
        Assert.DoesNotContain(repo.Audits, a =>
            a.Operation == "VALIDER_N1" && a.IdEntite == created.IdDemandePaiement);
    }

    [Fact]
    public async Task EnvoyerValidation_AutreDemandeur_MemeUb_ErrorAccessDenied()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);

        var createur = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 1,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });
        var created = await createur.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(createur, created.IdDemandePaiement);

        var collegue = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 2,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });

        var result = await CreateBatch(collegue, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, created.IdDemandePaiement));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Equal(
            StatutDemandePaiement.Brouillon,
            repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).Statut);
        Assert.DoesNotContain(repo.Audits, a =>
            a.Operation == "ENVOYER_EN_VALIDATION_N1" && a.IdEntite == created.IdDemandePaiement);
    }

    [Fact]
    public async Task InternalError_NeRetournePasMessageTechnique()
    {
        var (repo, svc, id) = await CreateBrouillonReadyAsync();
        repo.ForceInternalErrorOnGetDetailIds.Add(id);

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, id));

        Assert.Equal(1, result.Erreurs);
        var err = Assert.Single(result.Details);
        Assert.Equal(DemandePaiementBatchErrorCode.InternalError, err.CodeErreur);
        Assert.Equal(DemandePaiementBatchService.InternalErrorClientMessage, err.Message);
        Assert.DoesNotContain("SqlException", err.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dbo.", err.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StatutDemandePaiement.Brouillon, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
    }

    [Fact]
    public async Task EnvoyerValidation_TroisDpmMemeLot_SansContaminationChangeTracker()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SimulateCasDossierChangeTrackerContamination = true;
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        // Miroir 00422 / 00423 / 00424 : trois brouillons dans le même lot.
        var a = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "DP-422"));
        var b = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "DP-423"));
        var c = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "DP-424"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, b.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, c.IdDemandePaiement);

        repo.Demandes.Single(d => d.IdDemandePaiement == a.IdDemandePaiement).FK_CasDossier = 3;
        repo.Demandes.Single(d => d.IdDemandePaiement == b.IdDemandePaiement).FK_CasDossier = 5;
        repo.Demandes.Single(d => d.IdDemandePaiement == c.IdDemandePaiement).FK_CasDossier = 5;

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, a.IdDemandePaiement, b.IdDemandePaiement, c.IdDemandePaiement));

        Assert.Equal(3, result.Reussies);
        Assert.Equal(0, result.Erreurs);
        Assert.Equal(0, result.Ignorees);
        Assert.Equal(3, repo.ClearChangeTrackerCallCount);
        Assert.False(repo.IsChangeTrackerPolluted);
        Assert.All(result.Details, d =>
        {
            Assert.Equal(DemandePaiementBatchOutcome.Success, d.Outcome);
            Assert.Equal(StatutDemandePaiement.Brouillon, d.StatutAvant);
            Assert.Equal(StatutDemandePaiement.EnValidationN1, d.StatutApres);
        });
    }

    [Fact]
    public async Task EnvoyerValidation_SuccesNeContaminePasSuivante()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SimulateCasDossierChangeTrackerContamination = true;
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "A"));
        var b = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "B"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, b.IdDemandePaiement);

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, a.IdDemandePaiement, b.IdDemandePaiement));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(0, result.Erreurs);
        Assert.Equal(2, repo.ClearChangeTrackerCallCount);
        Assert.False(repo.IsChangeTrackerPolluted);
    }

    [Fact]
    public async Task EnvoyerValidation_EchecNeContaminePasSuivante()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SimulateCasDossierChangeTrackerContamination = true;
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Fail"));
        var b = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Ok"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, b.IdDemandePaiement);

        // Contamine le tracker pendant GetTracked de A ; le SaveChanges suivant échoue (INTERNAL_ERROR).
        repo.PolluteChangeTrackerOnGetTrackedIds.Add(a.IdDemandePaiement);

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, a.IdDemandePaiement, b.IdDemandePaiement));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(1, result.Reussies);
        var fail = Assert.Single(result.Details, d => d.IdDemandePaiement == a.IdDemandePaiement);
        Assert.Equal(DemandePaiementBatchOutcome.Error, fail.Outcome);
        Assert.Equal(DemandePaiementBatchErrorCode.InternalError, fail.CodeErreur);
        var ok = Assert.Single(result.Details, d => d.IdDemandePaiement == b.IdDemandePaiement);
        Assert.Equal(DemandePaiementBatchOutcome.Success, ok.Outcome);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, ok.StatutApres);
        Assert.Equal(2, repo.ClearChangeTrackerCallCount);
        Assert.False(repo.IsChangeTrackerPolluted);
    }

    [Fact]
    public async Task EnvoyerValidation_MemeFkCasDossier_LotReussit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SimulateCasDossierChangeTrackerContamination = true;
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Cas5-A"));
        var b = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Cas5-B"));
        var c = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Cas5-C"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, b.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, c.IdDemandePaiement);

        const long idCas = 5;
        foreach (var id in new[] { a.IdDemandePaiement, b.IdDemandePaiement, c.IdDemandePaiement })
            repo.Demandes.Single(d => d.IdDemandePaiement == id).FK_CasDossier = idCas;

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, a.IdDemandePaiement, b.IdDemandePaiement, c.IdDemandePaiement));

        Assert.Equal(3, result.Reussies);
        Assert.Equal(0, result.Erreurs);
        Assert.All(repo.Demandes.Where(d => new[] { a.IdDemandePaiement, b.IdDemandePaiement, c.IdDemandePaiement }.Contains(d.IdDemandePaiement)),
            d =>
            {
                Assert.Equal(idCas, d.FK_CasDossier);
                Assert.Equal(StatutDemandePaiement.EnValidationN1, d.Statut);
            });
        Assert.Equal(3, repo.ClearChangeTrackerCallCount);
    }

    [Fact]
    public async Task EnvoyerValidation_IgnoreAussiClearChangeTracker()
    {
        var (repo, svc, idOk) = await CreateBrouillonReadyAsync();
        var deja = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Déjà envoyée"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, deja.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(deja.IdDemandePaiement);

        // Active la simulation après l'unitaire (qui ne clear pas le tracker).
        repo.ClearChangeTracker();
        repo.SimulateCasDossierChangeTrackerContamination = true;
        var clearsAvantBatch = repo.ClearChangeTrackerCallCount;

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, idOk, deja.IdDemandePaiement));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(clearsAvantBatch + 2, repo.ClearChangeTrackerCallCount);
        Assert.False(repo.IsChangeTrackerPolluted);
    }

    [Fact]
    public async Task EnvoyerEnValidation_Unitaire_ContinueDeFonctionner()
    {
        var (repo, svc, id) = await CreateBrouillonReadyAsync();
        repo.SimulateCasDossierChangeTrackerContamination = true;

        var detail = await svc.EnvoyerEnValidationAsync(id);

        Assert.Equal(StatutDemandePaiement.EnValidationN1, detail.Statut);
        Assert.Equal(
            StatutDemandePaiement.EnValidationN1,
            repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
        Assert.Contains(repo.Audits, a => a.Operation == "ENVOYER_EN_VALIDATION_N1" && a.IdEntite == id);
    }

    [Fact]
    public async Task EnvoyerValidation_ConcurrenceSurUne_AutresContinuent()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var b = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "B"));
        var c = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "C"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, b.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, c.IdDemandePaiement);

        repo.ForceConcurrencyOnUpdateIds.Add(b.IdDemandePaiement);

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, a.IdDemandePaiement, b.IdDemandePaiement, c.IdDemandePaiement));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(0, result.Ignorees);
        Assert.Contains(result.Details, d => d.IdDemandePaiement == a.IdDemandePaiement && d.Outcome == DemandePaiementBatchOutcome.Success);
        Assert.Contains(result.Details, d => d.IdDemandePaiement == c.IdDemandePaiement && d.Outcome == DemandePaiementBatchOutcome.Success);
        var bDetail = Assert.Single(result.Details, d => d.IdDemandePaiement == b.IdDemandePaiement);
        Assert.Equal(DemandePaiementBatchOutcome.Error, bDetail.Outcome);
        Assert.Equal(DemandePaiementBatchErrorCode.Concurrency, bDetail.CodeErreur);
        Assert.Equal(StatutDemandePaiement.Brouillon, repo.Demandes.Single(d => d.IdDemandePaiement == b.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task ErreurMetierSurDeuxieme_NArretePasLesAutres()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var b = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Sans pièce"));
        var c = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "C"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, b.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, c.IdDemandePaiement);
        repo.Demandes.Single(d => d.IdDemandePaiement == b.IdDemandePaiement).ModePaiementSollicite = null;

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EnvoyerValidation,
            Req(StatutDemandePaiement.Brouillon, a.IdDemandePaiement, b.IdDemandePaiement, c.IdDemandePaiement));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.BusinessRule,
            Assert.Single(result.Details, d => d.IdDemandePaiement == b.IdDemandePaiement).CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, repo.Demandes.Single(d => d.IdDemandePaiement == a.IdDemandePaiement).Statut);
        Assert.Equal(StatutDemandePaiement.Brouillon, repo.Demandes.Single(d => d.IdDemandePaiement == b.IdDemandePaiement).Statut);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, repo.Demandes.Single(d => d.IdDemandePaiement == c.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task ValiderN1_Batch_PasseEnValidationN2()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var n1 = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var result = await CreateBatch(n1, repo).ExecuterAsync(
            DemandePaiementBatchOperation.ValiderN1,
            Req(StatutDemandePaiement.EnValidationN1, created.IdDemandePaiement));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, result.Details[0].StatutAvant);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, result.Details[0].StatutApres);
        Assert.Equal(
            StatutDemandePaiement.EnValidationN2,
            repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).Statut);
        Assert.Contains(repo.Audits, a =>
            a.IdEntite == created.IdDemandePaiement && a.Operation == "VALIDER_N1");
    }

    [Fact]
    public async Task ValiderN2_Batch_PasseEnValideeEntite()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(
                repo,
                AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur))
            .ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        var n2 = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));
        var result = await CreateBatch(n2, repo).ExecuterAsync(
            DemandePaiementBatchOperation.ValiderN2,
            Req(StatutDemandePaiement.EnValidationN2, created.IdDemandePaiement));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, result.Details[0].StatutAvant);
        Assert.Equal(StatutDemandePaiement.ValideeEntite, result.Details[0].StatutApres);
        Assert.Equal(
            StatutDemandePaiement.ValideeEntite,
            repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).Statut);
        Assert.Contains(repo.Audits, a =>
            a.IdEntite == created.IdDemandePaiement && a.Operation == "VALIDER_N2");
    }

    [Fact]
    public async Task SoumettreBudget_Batch_ProduitAuditUnitaire()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur))
            .ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(repo, AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice))
            .ValiderN2ElectroniqueAsync(created.IdDemandePaiement);

        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.SoumettreBudget,
            Req(StatutDemandePaiement.ValideeEntite, created.IdDemandePaiement));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.Soumise, result.Details[0].StatutApres);
        Assert.Contains(repo.Audits, a => a.Operation == "SOUMETTRE" && a.IdEntite == created.IdDemandePaiement);
    }

    [Fact]
    public async Task Controller_StatutFiltreVide_Http400()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);
        var ctrl = new DemandePaiementsBatchController(CreateBatch(svc, repo));
        var result = await ctrl.EnvoyerValidation(Req("", 1), CancellationToken.None);
        var obj = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
        var body = Assert.IsType<ApiErrorResponse>(obj.Value);
        Assert.Contains("filtre statut", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Controller_BatchOk_Http200()
    {
        var (repo, svc, id) = await CreateBrouillonReadyAsync();
        var ctrl = new DemandePaiementsBatchController(CreateBatch(svc, repo));
        var result = await ctrl.EnvoyerValidation(
            Req(StatutDemandePaiement.Brouillon, id),
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<DemandePaiementBatchResultDto>(ok.Value);
        Assert.Equal(1, body.Reussies);
    }

    private static DeclarationValidationPhysiqueRequest Physique(
        string nom = "Jean Dupont",
        string? fonction = "Directeur",
        DateOnly? date = null,
        string? commentaire = null)
        => new(nom, fonction, date ?? new DateOnly(2026, 9, 4), commentaire);

    private static DemandePaiementBatchRequest ReqPhysique(
        string statut,
        params (long Id, DeclarationValidationPhysiqueRequest Declaration)[] items)
        => new(
            statut,
            items.Select(x => new DemandePaiementBatchItemRequest(x.Id, Declaration: x.Declaration)).ToList());

    private static async Task<(FakeDemandePaiementRepo Repo, DemandePaiementService Agent, long Id)>
        PrepareEnValidationN1Async()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        return (repo, agent, created.IdDemandePaiement);
    }

    private static async Task<(FakeDemandePaiementRepo Repo, DemandePaiementService Agent, long Id)>
        PrepareEnValidationN2PhysiqueN1Async(bool addDocumentSigne)
    {
        var (repo, agent, id) = await PrepareEnValidationN1Async();
        await agent.DeclarerValidationPhysiqueN1Async(id, Physique("Resp N1", "Chef", new DateOnly(2026, 9, 1)));
        if (addDocumentSigne)
            await DemandePaiementTestData.AddDocumentSigneSampleAsync(agent, id);
        return (repo, agent, id);
    }

    [Fact]
    public void ValidateRequest_DeclarationAbsente_PhysiqueN1_Refuse()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1,
                Req(StatutDemandePaiement.EnValidationN1, 1)));
        Assert.Contains("déclaration", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRequest_DeclarationAbsente_PhysiqueN2_Refuse()
    {
        Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2,
                Req(StatutDemandePaiement.EnValidationN2, 1)));
    }

    [Fact]
    public async Task PhysiqueN1_Batch_SuccesNominal()
    {
        var (repo, agent, id) = await PrepareEnValidationN1Async();
        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1,
            ReqPhysique(StatutDemandePaiement.EnValidationN1, (id, Physique())));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, result.Details[0].StatutAvant);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, result.Details[0].StatutApres);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
        Assert.Contains(repo.Audits, a =>
            a.IdEntite == id && a.Operation == "DECLARER_VALIDATION_PHYSIQUE_N1");
    }

    [Fact]
    public async Task PhysiqueN1_StatutMismatch_IgnoreSansAppelUnitaire()
    {
        var (repo, agent, idOk) = await PrepareEnValidationN1Async();
        var autre = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Déjà N2"));
        await DemandePaiementTestData.AddSamplePieceAsync(agent, autre.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(autre.IdDemandePaiement);
        await agent.DeclarerValidationPhysiqueN1Async(autre.IdDemandePaiement, Physique("Autre"));

        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1,
            ReqPhysique(
                StatutDemandePaiement.EnValidationN1,
                (idOk, Physique()),
                (autre.IdDemandePaiement, Physique("X"))));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Ignorees);
        var ignore = Assert.Single(result.Details, d => d.Outcome == DemandePaiementBatchOutcome.Ignored);
        Assert.Equal(DemandePaiementBatchErrorCode.StatutMismatch, ignore.CodeErreur);
        Assert.Equal(autre.IdDemandePaiement, ignore.IdDemandePaiement);
        Assert.Equal(1, repo.Audits.Count(a =>
            a.IdEntite == autre.IdDemandePaiement && a.Operation == "DECLARER_VALIDATION_PHYSIQUE_N1"));
    }

    [Fact]
    public async Task PhysiqueN1_AccessDenied_AutreDemandeur()
    {
        var (repo, agent, id) = await PrepareEnValidationN1Async();
        var collegue = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 99,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });

        var result = await CreateBatch(collegue, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1,
            ReqPhysique(StatutDemandePaiement.EnValidationN1, (id, Physique())));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "DECLARER_VALIDATION_PHYSIQUE_N1" && a.IdEntite == id);
    }

    [Fact]
    public async Task PhysiqueN1_BusinessRule_NomSignataireVide()
    {
        var (repo, agent, id) = await PrepareEnValidationN1Async();
        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1,
            ReqPhysique(StatutDemandePaiement.EnValidationN1, (id, Physique("  "))));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.Validation, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
    }

    [Fact]
    public async Task PhysiqueN1_Concurrence_AutresContinuent()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var b = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "B"));
        var c = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "C"));
        foreach (var id in new[] { a.IdDemandePaiement, b.IdDemandePaiement, c.IdDemandePaiement })
        {
            await DemandePaiementTestData.AddSamplePieceAsync(agent, id);
            await agent.EnvoyerEnValidationAsync(id);
        }

        repo.ForceConcurrencyOnUpdateIds.Add(b.IdDemandePaiement);

        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1,
            ReqPhysique(
                StatutDemandePaiement.EnValidationN1,
                (a.IdDemandePaiement, Physique("A")),
                (b.IdDemandePaiement, Physique("B")),
                (c.IdDemandePaiement, Physique("C"))));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.Concurrency,
            Assert.Single(result.Details, d => d.IdDemandePaiement == b.IdDemandePaiement).CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, repo.Demandes.Single(d => d.IdDemandePaiement == b.IdDemandePaiement).Statut);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, repo.Demandes.Single(d => d.IdDemandePaiement == a.IdDemandePaiement).Statut);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, repo.Demandes.Single(d => d.IdDemandePaiement == c.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task PhysiqueN1_PayloadsIndividuels_ParDpm()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var b = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "B"));
        await DemandePaiementTestData.AddSamplePieceAsync(agent, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(agent, b.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(a.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(b.IdDemandePaiement);

        var declA = Physique("Jean Dupont", "Directeur", new DateOnly(2026, 9, 4), "A");
        var declB = Physique("Paul Martin", "Chef de service", new DateOnly(2026, 9, 3), "B");

        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1,
            ReqPhysique(
                StatutDemandePaiement.EnValidationN1,
                (a.IdDemandePaiement, declA),
                (b.IdDemandePaiement, declB)));

        Assert.Equal(2, result.Reussies);
        var n1A = repo.Demandes.Single(d => d.IdDemandePaiement == a.IdDemandePaiement)
            .ValidationsEntite.Single(v => v.Niveau == 1);
        var n1B = repo.Demandes.Single(d => d.IdDemandePaiement == b.IdDemandePaiement)
            .ValidationsEntite.Single(v => v.Niveau == 1);
        Assert.Equal("Jean Dupont", n1A.NomSignatairePhysique);
        Assert.Equal("Paul Martin", n1B.NomSignatairePhysique);
        Assert.Equal(new DateOnly(2026, 9, 4), n1A.DateSignaturePhysique);
        Assert.Equal(new DateOnly(2026, 9, 3), n1B.DateSignaturePhysique);
    }

    [Fact]
    public async Task PhysiqueN1_LotMixte_AccesAutoriseEtRefuse()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var userA = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 1,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });
        var userB = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 2,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });

        var dpmOk = await userA.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "OK"));
        await DemandePaiementTestData.AddSamplePieceAsync(userA, dpmOk.IdDemandePaiement);
        await userA.EnvoyerEnValidationAsync(dpmOk.IdDemandePaiement);

        var dpmRefusee = await userB.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "KO"));
        await DemandePaiementTestData.AddSamplePieceAsync(userB, dpmRefusee.IdDemandePaiement);
        await userB.EnvoyerEnValidationAsync(dpmRefusee.IdDemandePaiement);

        var result = await CreateBatch(userA, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN1,
            ReqPhysique(
                StatutDemandePaiement.EnValidationN1,
                (dpmOk.IdDemandePaiement, Physique("A")),
                (dpmRefusee.IdDemandePaiement, Physique("B"))));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == dpmOk.IdDemandePaiement).Outcome);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied,
            Assert.Single(result.Details, d => d.IdDemandePaiement == dpmRefusee.IdDemandePaiement).CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN1,
            repo.Demandes.Single(d => d.IdDemandePaiement == dpmRefusee.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task PhysiqueN2_Batch_SuccesAvecDocumentSigne()
    {
        var (repo, agent, id) = await PrepareEnValidationN2PhysiqueN1Async(addDocumentSigne: true);
        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2,
            ReqPhysique(StatutDemandePaiement.EnValidationN2, (id, Physique("Resp N2"))));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, result.Details[0].StatutAvant);
        Assert.Equal(StatutDemandePaiement.ValideeEntite, result.Details[0].StatutApres);
        Assert.Equal(StatutDemandePaiement.ValideeEntite, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
        Assert.Contains(repo.Audits, a => a.IdEntite == id && a.Operation == "DECLARER_VALIDATION_PHYSIQUE_N2");
    }

    [Fact]
    public async Task PhysiqueN2_DocumentSigneAbsent_BusinessRule()
    {
        var (repo, agent, id) = await PrepareEnValidationN2PhysiqueN1Async(addDocumentSigne: false);
        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2,
            ReqPhysique(StatutDemandePaiement.EnValidationN2, (id, Physique("Resp N2"))));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.BusinessRule, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "DECLARER_VALIDATION_PHYSIQUE_N2" && a.IdEntite == id);
    }

    [Fact]
    public async Task PhysiqueN2_StatutMismatch_Ignore()
    {
        var (repo, agent, idOk) = await PrepareEnValidationN2PhysiqueN1Async(addDocumentSigne: true);
        var autre = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "Encore N1"));
        await DemandePaiementTestData.AddSamplePieceAsync(agent, autre.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(autre.IdDemandePaiement);

        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2,
            ReqPhysique(
                StatutDemandePaiement.EnValidationN2,
                (idOk, Physique("N2")),
                (autre.IdDemandePaiement, Physique("X"))));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(DemandePaiementBatchErrorCode.StatutMismatch,
            Assert.Single(result.Details, d => d.IdDemandePaiement == autre.IdDemandePaiement).CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN1,
            repo.Demandes.Single(d => d.IdDemandePaiement == autre.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task PhysiqueN2_AccessDenied_AutreDemandeur()
    {
        var (repo, _, id) = await PrepareEnValidationN2PhysiqueN1Async(addDocumentSigne: true);
        var collegue = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 99,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });

        var result = await CreateBatch(collegue, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2,
            ReqPhysique(StatutDemandePaiement.EnValidationN2, (id, Physique("N2"))));

        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
    }

    [Fact]
    public async Task PhysiqueN2_Concurrence_AutresContinuent()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        async Task<long> PrepAsync(string objet)
        {
            var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: objet));
            await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
            await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
            await agent.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, Physique($"N1-{objet}"));
            await DemandePaiementTestData.AddDocumentSigneSampleAsync(agent, created.IdDemandePaiement);
            return created.IdDemandePaiement;
        }

        var idA = await PrepAsync("A");
        var idB = await PrepAsync("B");
        var idC = await PrepAsync("C");
        repo.ForceConcurrencyOnUpdateIds.Add(idB);

        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2,
            ReqPhysique(
                StatutDemandePaiement.EnValidationN2,
                (idA, Physique("A")),
                (idB, Physique("B")),
                (idC, Physique("C"))));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.Concurrency,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idB).CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, repo.Demandes.Single(d => d.IdDemandePaiement == idB).Statut);
        Assert.Equal(StatutDemandePaiement.ValideeEntite, repo.Demandes.Single(d => d.IdDemandePaiement == idA).Statut);
        Assert.Equal(StatutDemandePaiement.ValideeEntite, repo.Demandes.Single(d => d.IdDemandePaiement == idC).Statut);
    }

    [Fact]
    public async Task PhysiqueN2_Isolation_DocumentPresentEtAbsent()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        async Task<long> PrepAsync(string objet, bool withDoc)
        {
            var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: objet));
            await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
            await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
            await agent.DeclarerValidationPhysiqueN1Async(created.IdDemandePaiement, Physique($"N1-{objet}"));
            if (withDoc)
                await DemandePaiementTestData.AddDocumentSigneSampleAsync(agent, created.IdDemandePaiement);
            return created.IdDemandePaiement;
        }

        var id1 = await PrepAsync("1", withDoc: true);
        var id2 = await PrepAsync("2", withDoc: false);
        var id3 = await PrepAsync("3", withDoc: true);

        var result = await CreateBatch(agent, repo).ExecuterAsync(
            DemandePaiementBatchOperation.DeclarerValidationPhysiqueN2,
            ReqPhysique(
                StatutDemandePaiement.EnValidationN2,
                (id1, Physique("1")),
                (id2, Physique("2")),
                (id3, Physique("3"))));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id1).Outcome);
        Assert.Equal(DemandePaiementBatchErrorCode.BusinessRule,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id2).CodeErreur);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id3).Outcome);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, repo.Demandes.Single(d => d.IdDemandePaiement == id2).Statut);
    }

    // --- Phase 3A : réception batch ---

    [Fact]
    public async Task Receptionner_BatchUnitaire_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Receptionner,
            Req(StatutDemandePaiement.Soumise, id));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(0, result.Erreurs);
        Assert.Equal(0, result.Ignorees);
        Assert.Equal(DemandePaiementBatchOutcome.Success, result.Details[0].Outcome);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, result.Details[0].StatutApres);
        Assert.Equal(
            StatutDemandePaiement.EnTraitementDpm,
            repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
        Assert.Contains(repo.Audits, a => a.Operation == "RECEPTIONNER" && a.IdEntite == id);
    }

    [Fact]
    public async Task Receptionner_BatchPlusieurs_ToutesSuccess()
    {
        var repo = new FakeDemandePaiementRepo();
        var id1 = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var id2 = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var id3 = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Receptionner,
            Req(StatutDemandePaiement.Soumise, id1, id2, id3));

        Assert.Equal(3, result.Reussies);
        Assert.Equal(0, result.Erreurs);
        Assert.Equal(0, result.Ignorees);
        Assert.All(result.Details, d =>
        {
            Assert.Equal(DemandePaiementBatchOutcome.Success, d.Outcome);
            Assert.Equal(StatutDemandePaiement.EnTraitementDpm, d.StatutApres);
        });
        Assert.Equal(3, repo.Audits.Count(a => a.Operation == "RECEPTIONNER"));
    }

    [Fact]
    public void Receptionner_FiltreVide_RefuseInvalidStatusFilter()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.Receptionner,
                Req("", 1)));
        Assert.Equal("INVALID_STATUS_FILTER", ex.ErrorCode);
        Assert.Contains("SOUMISE", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Receptionner_FiltreEnTraitement_RefuseInvalidStatusFilter()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.Receptionner,
                Req(StatutDemandePaiement.EnTraitementDpm, 1)));
        Assert.Equal("INVALID_STATUS_FILTER", ex.ErrorCode);
        Assert.Contains("SOUMISE", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Controller_Receptionner_FiltreToutes_Http400()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));
        var http = await ctrl.Receptionner(Req("", 1), CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(http);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var body = Assert.IsType<ApiErrorResponse>(bad.Value);
        Assert.Equal("INVALID_STATUS_FILTER", body.Code);
        Assert.Contains("SOUMISE", body.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Receptionner_DejaEnTraitement_IgnoreStatutMismatch()
    {
        var repo = new FakeDemandePaiementRepo();
        var idOk = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var idDeja = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        await charge.ReceptionnerAsync(idDeja);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Receptionner,
            Req(StatutDemandePaiement.Soumise, idOk, idDeja));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(0, result.Erreurs);
        var ignore = Assert.Single(result.Details, d => d.Outcome == DemandePaiementBatchOutcome.Ignored);
        Assert.Equal(idDeja, ignore.IdDemandePaiement);
        Assert.Equal(DemandePaiementBatchErrorCode.StatutMismatch, ignore.CodeErreur);
        Assert.Equal(1, repo.Audits.Count(a => a.Operation == "RECEPTIONNER" && a.IdEntite == idOk));
        Assert.Equal(1, repo.Audits.Count(a => a.Operation == "RECEPTIONNER" && a.IdEntite == idDeja));
    }

    [Fact]
    public async Task Receptionner_SansPermission_ErrorAccessDenied()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var lecteur = DemandePaiementTestData.CreateService(repo, [AppPermissions.PaiementsLire]);

        var result = await CreateBatch(lecteur, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Receptionner,
            Req(StatutDemandePaiement.Soumise, id));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.Soumise, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "RECEPTIONNER" && a.IdEntite == id);
    }

    [Fact]
    public async Task Receptionner_HorsPoolJunior_ErrorAccessDenied()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var junior = DemandePaiementRoutageTestHelpers.Junior(repo);

        var result = await CreateBatch(junior, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Receptionner,
            Req(StatutDemandePaiement.Soumise, id));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.Soumise, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
    }

    [Fact]
    public async Task Receptionner_AutreDemandeurMemeUb_ErrorAccessDenied()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var demandeur = DemandePaiementRoutageTestHelpers.Demandeur(repo, DemandePaiementRoutageTestHelpers.X1);

        var result = await CreateBatch(demandeur, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Receptionner,
            Req(StatutDemandePaiement.Soumise, id));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.Soumise, repo.Demandes.Single(d => d.IdDemandePaiement == id).Statut);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "RECEPTIONNER");
    }

    [Fact]
    public async Task Receptionner_AutreUb_ErrorAccessDenied()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbDepartements[20] = 2;

        var row = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        row.FK_UniteBudgetaire = 20;
        row.UniteBudgetaire = new BudgetWeb.Domain.Entities.UniteBudgetaire
        {
            IdUB = 20,
            CodeUB = "UB020",
            Libelle = "UB hors périmètre",
            FK_Departement = 2,
            Actif = true,
        };

        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Receptionner,
            Req(StatutDemandePaiement.Soumise, id));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Equal(StatutDemandePaiement.Soumise, row.Statut);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "RECEPTIONNER" && a.IdEntite == id);
    }

    [Fact]
    public async Task Receptionner_LotMixte_AccesAutoriseEtRefuse_IsoleHttp200()
    {
        var repo = new FakeDemandePaiementRepo();
        var idOk = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var idRefusee = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbDepartements[20] = 2;

        var refusee = DemandePaiementRoutageTestHelpers.Tracked(repo, idRefusee);
        refusee.FK_UniteBudgetaire = 20;
        refusee.UniteBudgetaire = new BudgetWeb.Domain.Entities.UniteBudgetaire
        {
            IdUB = 20,
            CodeUB = "UB020",
            Libelle = "UB hors",
            FK_Departement = 2,
            Actif = true,
        };

        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));
        var http = await ctrl.Receptionner(
            Req(StatutDemandePaiement.Soumise, idOk, idRefusee),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(http);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var result = Assert.IsType<DemandePaiementBatchResultDto>(okResult.Value);

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idOk).Outcome);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idRefusee).CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm,
            DemandePaiementRoutageTestHelpers.Tracked(repo, idOk).Statut);
        Assert.Equal(StatutDemandePaiement.Soumise, refusee.Statut);
        Assert.Contains(repo.Audits, a => a.Operation == "RECEPTIONNER" && a.IdEntite == idOk);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "RECEPTIONNER" && a.IdEntite == idRefusee);
    }

    [Fact]
    public async Task Receptionner_ConcurrenceSurUne_AutresContinuent()
    {
        var repo = new FakeDemandePaiementRepo();
        var idA = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var idB = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var idC = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        repo.ForceConcurrencyOnUpdateIds.Add(idB);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Receptionner,
            Req(StatutDemandePaiement.Soumise, idA, idB, idC));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.Concurrency,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idB).CodeErreur);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm,
            DemandePaiementRoutageTestHelpers.Tracked(repo, idA).Statut);
        Assert.Equal(StatutDemandePaiement.Soumise,
            DemandePaiementRoutageTestHelpers.Tracked(repo, idB).Statut);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm,
            DemandePaiementRoutageTestHelpers.Tracked(repo, idC).Statut);
    }

    [Fact]
    public async Task Receptionner_Isolation_SuccessErrorSuccess_Http200()
    {
        var repo = new FakeDemandePaiementRepo();
        var id1 = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var id2 = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var id3 = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        repo.ForceConcurrencyOnUpdateIds.Add(id2);
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));

        var http = await ctrl.Receptionner(
            Req(StatutDemandePaiement.Soumise, id1, id2, id3),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(http);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var result = Assert.IsType<DemandePaiementBatchResultDto>(ok.Value);
        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id1).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Error,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id2).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id3).Outcome);
    }

    [Fact]
    public void Receptionner_Exactement100_Accepte()
    {
        var ids = Enumerable.Range(1, 100).Select(i => new DemandePaiementBatchItemRequest(i)).ToList();
        DemandePaiementBatchOperationRules.ValidateRequest(
            DemandePaiementBatchOperation.Receptionner,
            new DemandePaiementBatchRequest(StatutDemandePaiement.Soumise, ids));
    }

    [Fact]
    public void Receptionner_101_Refuse()
    {
        var ids = Enumerable.Range(1, 101).Select(i => new DemandePaiementBatchItemRequest(i)).ToList();
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.Receptionner,
                new DemandePaiementBatchRequest(StatutDemandePaiement.Soumise, ids)));
        Assert.Contains("100", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Receptionner_IdsDupliques_Refuse()
    {
        Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.Receptionner,
                Req(StatutDemandePaiement.Soumise, 1, 1)));
    }

    // --- Phase 3B : traitement de charge batch ---

    private static TraitementChargeDpmRequest Traitement(
        string instrument = TypeInstrumentPaiement.PieceCaisse,
        string? devisePaiement = "CDF",
        string? mode = null,
        string? typeBudget = null,
        string? item = null,
        decimal? taux = null,
        long? idTaux = null)
        => new(instrument, devisePaiement, taux, idTaux, mode, typeBudget, item);

    private static DemandePaiementBatchRequest ReqTraitement(
        params (long Id, TraitementChargeDpmRequest Traitement)[] items)
        => new(
            StatutDemandePaiement.EnTraitementDpm,
            items.Select(x => new DemandePaiementBatchItemRequest(x.Id, Traitement: x.Traitement)).ToList());

    private static async Task<(FakeDemandePaiementRepo Repo, DemandePaiementService Charge, long Id)> PrepTraiterReadyAsync(
        string devise = "USD",
        string mode = ModePaiementDpm.Caisse,
        string typeBudget = TypeBudgetCode.DepensesCourantes,
        string? item = null,
        string instrument = TypeInstrumentPaiement.PieceCaisse,
        bool withInstrument = true,
        bool withBilletIfNeeded = true,
        FakeDemandePaiementRepo? existingRepo = null)
    {
        var repo = existingRepo ?? new FakeDemandePaiementRepo();
        if (existingRepo is null)
        {
            repo.SeedPieceObligatoire();
            DemandePaiementRoutageTestHelpers.SeedUb(repo);
        }

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await admin.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: devise,
                modePaiementSollicite: mode,
                typeBudgetSollicite: typeBudget,
                itemSollicite: item,
                objet: $"Traiter-{Guid.NewGuid():N}"[..20]));
        await DemandePaiementTestData.AddSamplePieceAsync(admin, created.IdDemandePaiement);
        await admin.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await admin.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await admin.ValiderN2ElectroniqueAsync(created.IdDemandePaiement);
        await admin.SoumettreAsync(created.IdDemandePaiement);

        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        await charge.ReceptionnerAsync(created.IdDemandePaiement);

        if (withInstrument)
        {
            await DemandePaiementTestData.AddSampleInstrumentPieceAsync(
                repo, charge, created.IdDemandePaiement, instrument);
        }
        else if (withBilletIfNeeded)
        {
            await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);
        }

        return (repo, charge, created.IdDemandePaiement);
    }

    [Fact]
    public async Task TraiterCharge_BatchUnitaire_Reussit()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(devise: "CDF");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement(TypeInstrumentPaiement.PieceCaisse, "CDF", ModePaiementDpm.Caisse, TypeBudgetCode.DepensesCourantes))));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(0, result.Erreurs);
        Assert.Equal(DemandePaiementBatchOutcome.Success, result.Details[0].Outcome);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, result.Details[0].StatutApres);
        Assert.Equal(TypeInstrumentPaiement.PieceCaisse,
            repo.Demandes.Single(d => d.IdDemandePaiement == id).TypeInstrumentPaiement);
        Assert.Contains(repo.Audits, a => a.Operation == "TRAITER" && a.IdEntite == id);
    }

    [Fact]
    public async Task TraiterCharge_BatchPlusieurs_ToutesSuccess()
    {
        var (repo, charge, id1) = await PrepTraiterReadyAsync(devise: "CDF");
        var (_, _, id2) = await PrepTraiterReadyAsync(devise: "CDF", existingRepo: repo);
        var (_, _, id3) = await PrepTraiterReadyAsync(devise: "CDF", existingRepo: repo);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement(
                (id1, Traitement()),
                (id2, Traitement()),
                (id3, Traitement())));

        Assert.Equal(3, result.Reussies);
        Assert.Equal(0, result.Erreurs);
        Assert.Equal(3, repo.Audits.Count(a => a.Operation == "TRAITER"));
    }

    [Fact]
    public async Task TraiterCharge_Heterogene_ModesInstrumentsTypes_Reussit()
    {
        var (repo, charge, idCaisse) = await PrepTraiterReadyAsync(
            devise: "CDF",
            mode: ModePaiementDpm.Caisse,
            instrument: TypeInstrumentPaiement.PieceCaisse);
        var (_, _, idBanque) = await PrepTraiterReadyAsync(
            devise: "USD",
            mode: ModePaiementDpm.Banque,
            typeBudget: TypeBudgetCode.ActionsExploitation,
            item: "025",
            instrument: TypeInstrumentPaiement.MinuteCheque,
            existingRepo: repo);
        var (_, _, idBon) = await PrepTraiterReadyAsync(
            devise: "CDF",
            mode: ModePaiementDpm.Caisse,
            typeBudget: TypeBudgetCode.BudgetInvestissement,
            item: "IVT-01",
            instrument: TypeInstrumentPaiement.BonProvisoire,
            existingRepo: repo);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement(
                (idCaisse, Traitement(TypeInstrumentPaiement.PieceCaisse, "CDF", ModePaiementDpm.Caisse, TypeBudgetCode.DepensesCourantes)),
                (idBanque, Traitement(TypeInstrumentPaiement.MinuteCheque, "USD", ModePaiementDpm.Banque, TypeBudgetCode.ActionsExploitation, "025")),
                (idBon, Traitement(TypeInstrumentPaiement.BonProvisoire, "CDF", ModePaiementDpm.Caisse, TypeBudgetCode.BudgetInvestissement, "IVT-01"))));

        Assert.Equal(3, result.Reussies);
        Assert.Equal(TypeInstrumentPaiement.PieceCaisse, repo.Demandes.Single(d => d.IdDemandePaiement == idCaisse).TypeInstrumentPaiement);
        Assert.Equal(TypeInstrumentPaiement.MinuteCheque, repo.Demandes.Single(d => d.IdDemandePaiement == idBanque).TypeInstrumentPaiement);
        Assert.Equal(TypeInstrumentPaiement.BonProvisoire, repo.Demandes.Single(d => d.IdDemandePaiement == idBon).TypeInstrumentPaiement);
        Assert.Equal(TypeBudgetCode.ActionsExploitation, repo.Demandes.Single(d => d.IdDemandePaiement == idBanque).TypeBudgetSollicite);
        Assert.Equal(TypeBudgetCode.BudgetInvestissement, repo.Demandes.Single(d => d.IdDemandePaiement == idBon).TypeBudgetSollicite);
    }

    [Fact]
    public void TraiterCharge_TraitementAbsent_Refuse()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.TraiterCharge,
                Req(StatutDemandePaiement.EnTraitementDpm, 1)));
        Assert.Contains("traitement", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TraiterCharge_FiltreVide_RefuseInvalidStatusFilter()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.TraiterCharge,
                new DemandePaiementBatchRequest("", [new DemandePaiementBatchItemRequest(1, Traitement: Traitement())])));
        Assert.Equal("INVALID_STATUS_FILTER", ex.ErrorCode);
        Assert.Contains("EN_TRAITEMENT_DPM", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TraiterCharge_FiltreSoumise_RefuseInvalidStatusFilter()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.TraiterCharge,
                new DemandePaiementBatchRequest(
                    StatutDemandePaiement.Soumise,
                    [new DemandePaiementBatchItemRequest(1, Traitement: Traitement())])));
        Assert.Equal("INVALID_STATUS_FILTER", ex.ErrorCode);
    }

    [Fact]
    public async Task Controller_TraiterCharge_FiltreToutes_Http400()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));
        var http = await ctrl.TraiterCharge(
            new DemandePaiementBatchRequest("", [new DemandePaiementBatchItemRequest(1, Traitement: Traitement())]),
            CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(http);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var body = Assert.IsType<ApiErrorResponse>(bad.Value);
        Assert.Equal("INVALID_STATUS_FILTER", body.Code);
    }

    [Fact]
    public async Task TraiterCharge_DejaOrientee_IgnoreStatutMismatch()
    {
        var (repo, charge, idOk) = await PrepTraiterReadyAsync(devise: "CDF");
        var (_, _, idMismatch) = await PrepTraiterReadyAsync(devise: "CDF", existingRepo: repo);
        // Simule une évolution de statut entre sélection et exécution (DPM encore lisible).
        DemandePaiementRoutageTestHelpers.Tracked(repo, idMismatch).Statut = StatutDemandePaiement.Soumise;

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement(
                (idOk, Traitement()),
                (idMismatch, Traitement())));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Ignorees);
        var ignore = Assert.Single(result.Details, d => d.Outcome == DemandePaiementBatchOutcome.Ignored);
        Assert.Equal(idMismatch, ignore.IdDemandePaiement);
        Assert.Equal(DemandePaiementBatchErrorCode.StatutMismatch, ignore.CodeErreur);
    }

    [Fact]
    public async Task TraiterCharge_SansBillet_ErrorBusinessRule()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(
            devise: "USD",
            withInstrument: false,
            withBilletIfNeeded: false);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement())));

        Assert.Equal(0, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.BusinessRule, result.Details[0].CodeErreur);
        Assert.Contains("billet", result.Details[0].Message!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "TRAITER" && a.IdEntite == id);
    }

    [Fact]
    public async Task TraiterCharge_SansDocumentInstrument_ErrorBusinessRule()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(
            devise: "CDF",
            withInstrument: false,
            withBilletIfNeeded: false);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement())));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.BusinessRule, result.Details[0].CodeErreur);
        Assert.Contains("document instrument", result.Details[0].Message!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "TRAITER");
    }

    [Fact]
    public async Task TraiterCharge_MemeDevise_Identite()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(devise: "CDF");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement(TypeInstrumentPaiement.PieceCaisse, "CDF"))));

        Assert.Equal(1, result.Reussies);
        var detail = await charge.GetByIdAsync(id);
        Assert.Equal(1m, detail!.TauxPaiement);
        Assert.Null(detail.IdTauxChangePaiement);
        Assert.Equal(detail.MontantBrut, detail.MontantPaiement);
    }

    [Fact]
    public async Task TraiterCharge_DeviseDifferente_ResolutReferentiel()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(devise: "USD");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement(TypeInstrumentPaiement.PieceCaisse, "CDF", ModePaiementDpm.Caisse))));

        Assert.Equal(1, result.Reussies);
        var detail = await charge.GetByIdAsync(id);
        Assert.Equal("USD", detail!.Devise);
        Assert.Equal("CDF", detail.DevisePaiement);
        Assert.NotNull(detail.TauxPaiement);
        Assert.True(detail.TauxPaiement > 1m);
        Assert.Equal(detail.MontantBrut * detail.TauxPaiement, detail.MontantPaiement);
    }

    [Fact]
    public async Task TraiterCharge_TauxManuel_ErrorBusinessRule()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(devise: "USD");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement(taux: 9_999m))));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.BusinessRule, result.Details[0].CodeErreur);
        Assert.Contains("saisie manuelle", result.Details[0].Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TraiterCharge_FkTauxManuel_ErrorBusinessRule()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(devise: "USD");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement(idTaux: 99L))));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.BusinessRule, result.Details[0].CodeErreur);
        Assert.Contains("saisie manuelle", result.Details[0].Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TraiterCharge_SansPermission_ErrorAccessDenied()
    {
        var (repo, _, id) = await PrepTraiterReadyAsync(devise: "CDF");
        var lecteur = DemandePaiementTestData.CreateService(repo, [AppPermissions.PaiementsLire]);
        var result = await CreateBatch(lecteur, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement())));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task TraiterCharge_HorsPoolJunior_ErrorAccessDenied()
    {
        var (repo, _, id) = await PrepTraiterReadyAsync(devise: "CDF");
        var junior = DemandePaiementRoutageTestHelpers.Junior(repo);
        var result = await CreateBatch(junior, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement())));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task TraiterCharge_AssigneeAutreCharge_ErrorAccessDenied()
    {
        var (repo, y1, id) = await PrepTraiterReadyAsync(devise: "CDF");
        _ = y1;
        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        var result = await CreateBatch(y2, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement())));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task TraiterCharge_AutreUb_ErrorAccessDenied()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(devise: "CDF");
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbDepartements[20] = 2;
        var row = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        row.FK_UniteBudgetaire = 20;
        row.UniteBudgetaire = new BudgetWeb.Domain.Entities.UniteBudgetaire
        {
            IdUB = 20,
            CodeUB = "UB020",
            Libelle = "UB hors",
            FK_Departement = 2,
            Actif = true,
        };

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement())));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task TraiterCharge_LotMixte_AccesAutoriseEtRefuse_Http200()
    {
        var (repo, charge, idOk) = await PrepTraiterReadyAsync(devise: "CDF");
        var (_, _, idRefusee) = await PrepTraiterReadyAsync(devise: "CDF", existingRepo: repo);
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbDepartements[20] = 2;
        var refusee = DemandePaiementRoutageTestHelpers.Tracked(repo, idRefusee);
        refusee.FK_UniteBudgetaire = 20;
        refusee.UniteBudgetaire = new BudgetWeb.Domain.Entities.UniteBudgetaire
        {
            IdUB = 20,
            CodeUB = "UB020",
            Libelle = "UB hors",
            FK_Departement = 2,
            Actif = true,
        };

        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));
        var http = await ctrl.TraiterCharge(
            ReqTraitement((idOk, Traitement()), (idRefusee, Traitement())),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(http);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var result = Assert.IsType<DemandePaiementBatchResultDto>(ok.Value);
        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idRefusee).CodeErreur);
        Assert.Contains(repo.Audits, a => a.Operation == "TRAITER" && a.IdEntite == idOk);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "TRAITER" && a.IdEntite == idRefusee);
    }

    [Fact]
    public async Task TraiterCharge_ConcurrenceSurUne_AutresContinuent()
    {
        var (repo, charge, idA) = await PrepTraiterReadyAsync(devise: "CDF");
        var (_, _, idB) = await PrepTraiterReadyAsync(devise: "CDF", existingRepo: repo);
        var (_, _, idC) = await PrepTraiterReadyAsync(devise: "CDF", existingRepo: repo);
        repo.ForceConcurrencyOnUpdateIds.Add(idB);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement(
                (idA, Traitement()),
                (idB, Traitement()),
                (idC, Traitement())));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.Concurrency,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idB).CodeErreur);
    }

    [Fact]
    public async Task TraiterCharge_Isolation_SuccessErrorSuccess_Http200()
    {
        var (repo, charge, id1) = await PrepTraiterReadyAsync(devise: "CDF");
        var (_, _, id2) = await PrepTraiterReadyAsync(
            devise: "USD", withInstrument: false, withBilletIfNeeded: false, existingRepo: repo);
        var (_, _, id3) = await PrepTraiterReadyAsync(devise: "CDF", existingRepo: repo);
        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));

        var http = await ctrl.TraiterCharge(
            ReqTraitement((id1, Traitement()), (id2, Traitement()), (id3, Traitement())),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(http);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var result = Assert.IsType<DemandePaiementBatchResultDto>(ok.Value);
        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id1).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Error,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id2).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id3).Outcome);
    }

    [Fact]
    public void TraiterCharge_Exactement100_Accepte()
    {
        var items = Enumerable.Range(1, 100)
            .Select(i => new DemandePaiementBatchItemRequest(i, Traitement: Traitement()))
            .ToList();
        DemandePaiementBatchOperationRules.ValidateRequest(
            DemandePaiementBatchOperation.TraiterCharge,
            new DemandePaiementBatchRequest(StatutDemandePaiement.EnTraitementDpm, items));
    }

    [Fact]
    public void TraiterCharge_101_Refuse()
    {
        var items = Enumerable.Range(1, 101)
            .Select(i => new DemandePaiementBatchItemRequest(i, Traitement: Traitement()))
            .ToList();
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.TraiterCharge,
                new DemandePaiementBatchRequest(StatutDemandePaiement.EnTraitementDpm, items)));
        Assert.Contains("100", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TraiterCharge_IdsDupliques_Refuse()
    {
        Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.TraiterCharge,
                ReqTraitement((1, Traitement()), (1, Traitement()))));
    }

    [Fact]
    public async Task TraiterCharge_RejeuMemePayload_ReussitANouveau()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(devise: "CDF");
        var req = ReqTraitement((id, Traitement()));

        var first = await CreateBatch(charge, repo).ExecuterAsync(DemandePaiementBatchOperation.TraiterCharge, req);
        var second = await CreateBatch(charge, repo).ExecuterAsync(DemandePaiementBatchOperation.TraiterCharge, req);

        Assert.Equal(1, first.Reussies);
        Assert.Equal(1, second.Reussies);
        Assert.Equal(2, repo.Audits.Count(a => a.Operation == "TRAITER" && a.IdEntite == id));
    }

    [Fact]
    public async Task TraiterCharge_RejeuPayloadDifferent_ModifieSelonUnitaire()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(
            devise: "CDF",
            instrument: TypeInstrumentPaiement.PieceCaisse);

        var first = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement(
                TypeInstrumentPaiement.PieceCaisse,
                "CDF",
                ModePaiementDpm.Caisse,
                TypeBudgetCode.DepensesCourantes))));
        var second = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement(
                TypeInstrumentPaiement.PieceCaisse,
                "CDF",
                ModePaiementDpm.Caisse,
                TypeBudgetCode.ActionsExploitation,
                "025"))));

        Assert.Equal(1, first.Reussies);
        Assert.Equal(1, second.Reussies);
        Assert.Equal(
            TypeBudgetCode.ActionsExploitation,
            repo.Demandes.Single(d => d.IdDemandePaiement == id).TypeBudgetSollicite);
        Assert.Equal("025", repo.Demandes.Single(d => d.IdDemandePaiement == id).ItemSollicite);
        Assert.Equal(2, repo.Audits.Count(a => a.Operation == "TRAITER" && a.IdEntite == id));
    }

    [Fact]
    public async Task TraiterCharge_InternalError_NeRetournePasMessageTechnique()
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(devise: "CDF");
        repo.ForceInternalErrorOnGetDetailIds.Add(id);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.TraiterCharge,
            ReqTraitement((id, Traitement())));

        Assert.Equal(1, result.Erreurs);
        var err = Assert.Single(result.Details);
        Assert.Equal(DemandePaiementBatchErrorCode.InternalError, err.CodeErreur);
        Assert.Equal(DemandePaiementBatchService.InternalErrorClientMessage, err.Message);
        Assert.DoesNotContain("SqlException", err.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, result.CorrelationId);
    }

    // --- Phase 3C : établissement documents batch ---

    private static DemandePaiementBatchDocumentsRequest Docs(
        EtablirBilletConversionRequest? billet = null,
        EtablirPieceCaisseRequest? piece = null,
        EtablirBonProvisoireRequest? bon = null,
        EtablirMinuteChequeRequest? minute = null,
        string? typeInstrumentForce = null)
        => new(billet, piece, bon, minute, typeInstrumentForce);

    private static DemandePaiementBatchRequest ReqDocuments(
        params (long Id, DemandePaiementBatchDocumentsRequest Documents)[] items)
        => new(
            StatutDemandePaiement.EnTraitementDpm,
            items.Select(x => new DemandePaiementBatchItemRequest(x.Id, Documents: x.Documents)).ToList());

    private static async Task<(FakeDemandePaiementRepo Repo, DemandePaiementService Charge, long Id)> PrepEtablirDocsReadyAsync(
        string devise = "CDF",
        string mode = ModePaiementDpm.Caisse,
        string typeBudget = TypeBudgetCode.DepensesCourantes,
        string? item = null,
        string? seedInstrumentParam = TypeInstrumentPaiement.PieceCaisse,
        FakeDemandePaiementRepo? existingRepo = null)
    {
        var repo = existingRepo ?? new FakeDemandePaiementRepo();
        if (existingRepo is null)
        {
            repo.SeedPieceObligatoire();
            DemandePaiementRoutageTestHelpers.SeedUb(repo);
        }

        if (!string.IsNullOrWhiteSpace(seedInstrumentParam))
            repo.SeedParametreInstrument(seedInstrumentParam);

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await admin.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: devise,
                modePaiementSollicite: mode,
                typeBudgetSollicite: typeBudget,
                itemSollicite: item,
                objet: $"Docs-{Guid.NewGuid():N}"[..20]));
        await DemandePaiementTestData.AddSamplePieceAsync(admin, created.IdDemandePaiement);
        await admin.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await admin.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await admin.ValiderN2ElectroniqueAsync(created.IdDemandePaiement);
        await admin.SoumettreAsync(created.IdDemandePaiement);

        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        await charge.ReceptionnerAsync(created.IdDemandePaiement);
        return (repo, charge, created.IdDemandePaiement);
    }

    [Fact]
    public void EtablirDocuments_FiltreEnTraitement_Accepte()
    {
        DemandePaiementBatchOperationRules.ValidateRequest(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((1, Docs(piece: new EtablirPieceCaisseRequest()))));
    }

    [Fact]
    public void EtablirDocuments_FiltreAbsent_RefuseInvalidStatusFilter()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.EtablirDocuments,
                new DemandePaiementBatchRequest(
                    "",
                    [new DemandePaiementBatchItemRequest(1, Documents: Docs(piece: new EtablirPieceCaisseRequest()))])));
        Assert.Equal("INVALID_STATUS_FILTER", ex.ErrorCode);
        Assert.Contains("EN_TRAITEMENT_DPM", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Controller_EtablirDocuments_FiltreToutes_Http400()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));
        var http = await ctrl.EtablirDocuments(
            new DemandePaiementBatchRequest(
                "",
                [new DemandePaiementBatchItemRequest(1, Documents: Docs(piece: new EtablirPieceCaisseRequest()))]),
            CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(http);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var body = Assert.IsType<ApiErrorResponse>(bad.Value);
        Assert.Equal("INVALID_STATUS_FILTER", body.Code);
    }

    [Fact]
    public void EtablirDocuments_FiltreSoumise_RefuseInvalidStatusFilter()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.EtablirDocuments,
                new DemandePaiementBatchRequest(
                    StatutDemandePaiement.Soumise,
                    [new DemandePaiementBatchItemRequest(1, Documents: Docs(piece: new EtablirPieceCaisseRequest()))])));
        Assert.Equal("INVALID_STATUS_FILTER", ex.ErrorCode);
    }

    [Fact]
    public void EtablirDocuments_101_Refuse()
    {
        var items = Enumerable.Range(1, 101)
            .Select(i => new DemandePaiementBatchItemRequest(i, Documents: Docs(piece: new EtablirPieceCaisseRequest())))
            .ToList();
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.EtablirDocuments,
                new DemandePaiementBatchRequest(StatutDemandePaiement.EnTraitementDpm, items)));
        Assert.Contains("100", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EtablirDocuments_IdsDupliques_Refuse()
    {
        Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.EtablirDocuments,
                ReqDocuments(
                    (1, Docs(piece: new EtablirPieceCaisseRequest())),
                    (1, Docs(piece: new EtablirPieceCaisseRequest())))));
    }

    [Fact]
    public void EtablirDocuments_DocumentsAbsent_Refuse()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.EtablirDocuments,
                Req(StatutDemandePaiement.EnTraitementDpm, 1)));
        Assert.Contains("documents", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EtablirDocuments_PieceCaisse_Nominal()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.NotNull(repo.PiecesCaisse.SingleOrDefault(p => p.FK_DemandePaiement == id));
        Assert.Contains(repo.Audits, a => a.Operation == "ETABLIR_PIECE_CAISSE" && a.IdEntite == id);
    }

    [Fact]
    public async Task EtablirDocuments_BonProvisoire_Nominal()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(
            devise: "CDF",
            seedInstrumentParam: TypeInstrumentPaiement.BonProvisoire);
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(bon: new EtablirBonProvisoireRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.NotNull(repo.BonsProvisoire.SingleOrDefault(b => b.FK_DemandePaiement == id));
        Assert.Contains(repo.Audits, a => a.Operation == "ETABLIR_BON_PROVISOIRE" && a.IdEntite == id);
    }

    [Fact]
    public async Task EtablirDocuments_MinuteCheque_Nominal()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(
            devise: "USD",
            mode: ModePaiementDpm.Banque,
            seedInstrumentParam: TypeInstrumentPaiement.MinuteCheque);
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(minute: new EtablirMinuteChequeRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.NotNull(repo.MinutesCheque.SingleOrDefault(m => m.FK_DemandePaiement == id));
        Assert.Contains(repo.Audits, a => a.Operation == "ETABLIR_MINUTE_CHEQUE" && a.IdEntite == id);
    }

    [Fact]
    public async Task EtablirDocuments_InstrumentDejaEtabli_IdempotentSuccess()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var req = ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest())));
        var first = await CreateBatch(charge, repo).ExecuterAsync(DemandePaiementBatchOperation.EtablirDocuments, req);
        var second = await CreateBatch(charge, repo).ExecuterAsync(DemandePaiementBatchOperation.EtablirDocuments, req);

        Assert.Equal(1, first.Reussies);
        Assert.Equal(1, second.Reussies);
        Assert.Single(repo.PiecesCaisse, p => p.FK_DemandePaiement == id);
    }

    [Fact]
    public async Task EtablirDocuments_MauvaisModeInstrument_ErrorBusinessRule()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(
            devise: "USD",
            mode: ModePaiementDpm.Banque,
            seedInstrumentParam: TypeInstrumentPaiement.PieceCaisse);
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.BusinessRule, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task EtablirDocuments_CaisseCdf_BilletNull_PieceSeule_Success()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.DoesNotContain(repo.Billets, b => b.FK_DemandePaiement == id);
        Assert.NotNull(repo.PiecesCaisse.SingleOrDefault(p => p.FK_DemandePaiement == id));
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "ETABLIR_BILLET_CONVERSION" && a.IdEntite == id);
        Assert.Contains(repo.Audits, a => a.Operation == "ETABLIR_PIECE_CAISSE" && a.IdEntite == id);
    }

    [Fact]
    public async Task EtablirDocuments_CaisseUsd_BilletNull_AutoBilletPuisPiece_Success()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "USD");
        // Payload sans Billet : le batch doit appeler EtablirBilletConversionAsync via NecessiteBillet.
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.NotNull(repo.Billets.SingleOrDefault(b => b.FK_DemandePaiement == id));
        Assert.NotNull(repo.PiecesCaisse.SingleOrDefault(p => p.FK_DemandePaiement == id));
        Assert.Contains(repo.Audits, a => a.Operation == "ETABLIR_BILLET_CONVERSION" && a.IdEntite == id);
        Assert.Contains(repo.Audits, a => a.Operation == "ETABLIR_PIECE_CAISSE" && a.IdEntite == id);
        var idxBillet = repo.Audits.FindIndex(a => a.Operation == "ETABLIR_BILLET_CONVERSION" && a.IdEntite == id);
        var idxPiece = repo.Audits.FindIndex(a => a.Operation == "ETABLIR_PIECE_CAISSE" && a.IdEntite == id);
        Assert.True(idxBillet >= 0 && idxPiece > idxBillet, "Le billet doit être établi avant la pièce.");
    }

    [Fact]
    public async Task EtablirDocuments_CaisseUsd_BilletNull_AutoBilletPuisBon_Success()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(
            devise: "USD",
            seedInstrumentParam: TypeInstrumentPaiement.BonProvisoire);
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(bon: new EtablirBonProvisoireRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.NotNull(repo.Billets.SingleOrDefault(b => b.FK_DemandePaiement == id));
        Assert.NotNull(repo.BonsProvisoire.SingleOrDefault(b => b.FK_DemandePaiement == id));
        Assert.Contains(repo.Audits, a => a.Operation == "ETABLIR_BILLET_CONVERSION" && a.IdEntite == id);
        Assert.Contains(repo.Audits, a => a.Operation == "ETABLIR_BON_PROVISOIRE" && a.IdEntite == id);
    }

    [Fact]
    public async Task EtablirDocuments_BanqueUsd_BilletNull_MinuteSeule_Success()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(
            devise: "USD",
            mode: ModePaiementDpm.Banque,
            seedInstrumentParam: TypeInstrumentPaiement.MinuteCheque);
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(minute: new EtablirMinuteChequeRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.DoesNotContain(repo.Billets, b => b.FK_DemandePaiement == id);
        Assert.NotNull(repo.MinutesCheque.SingleOrDefault(m => m.FK_DemandePaiement == id));
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "ETABLIR_BILLET_CONVERSION" && a.IdEntite == id);
    }

    [Fact]
    public async Task EtablirDocuments_CaisseUsd_BilletDejaEtabli_BilletNull_IdempotentPuisPiece()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "USD");
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, id);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.Single(repo.Billets, b => b.FK_DemandePaiement == id);
        Assert.NotNull(repo.PiecesCaisse.SingleOrDefault(p => p.FK_DemandePaiement == id));
    }

    [Fact]
    public async Task EtablirDocuments_ErreurEtablissementBillet_ErrorBusinessRule()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(billet: new EtablirBilletConversionRequest()))));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.BusinessRule, result.Details[0].CodeErreur);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "ETABLIR_BILLET_CONVERSION" && a.IdEntite == id);
    }

    [Fact]
    public async Task EtablirDocuments_LotHeterogene_BilletAuto_ModesInstruments()
    {
        var (repo, charge, idCdf) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var (_, _, idUsdPiece) = await PrepEtablirDocsReadyAsync(devise: "USD", existingRepo: repo);
        var (_, _, idUsdBon) = await PrepEtablirDocsReadyAsync(
            devise: "USD",
            seedInstrumentParam: TypeInstrumentPaiement.BonProvisoire,
            existingRepo: repo);
        var (_, _, idBanque) = await PrepEtablirDocsReadyAsync(
            devise: "USD",
            mode: ModePaiementDpm.Banque,
            seedInstrumentParam: TypeInstrumentPaiement.MinuteCheque,
            existingRepo: repo);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments(
                (idCdf, Docs(piece: new EtablirPieceCaisseRequest())),
                (idUsdPiece, Docs(piece: new EtablirPieceCaisseRequest())),
                (idUsdBon, Docs(bon: new EtablirBonProvisoireRequest())),
                (idBanque, Docs(minute: new EtablirMinuteChequeRequest()))));

        Assert.Equal(4, result.Reussies);
        Assert.DoesNotContain(repo.Billets, b => b.FK_DemandePaiement == idCdf);
        Assert.NotNull(repo.PiecesCaisse.SingleOrDefault(p => p.FK_DemandePaiement == idCdf));
        Assert.NotNull(repo.Billets.SingleOrDefault(b => b.FK_DemandePaiement == idUsdPiece));
        Assert.NotNull(repo.PiecesCaisse.SingleOrDefault(p => p.FK_DemandePaiement == idUsdPiece));
        Assert.NotNull(repo.Billets.SingleOrDefault(b => b.FK_DemandePaiement == idUsdBon));
        Assert.NotNull(repo.BonsProvisoire.SingleOrDefault(b => b.FK_DemandePaiement == idUsdBon));
        Assert.DoesNotContain(repo.Billets, b => b.FK_DemandePaiement == idBanque);
        Assert.NotNull(repo.MinutesCheque.SingleOrDefault(m => m.FK_DemandePaiement == idBanque));
    }

    [Fact]
    public async Task EtablirDocuments_TypeInstrumentForce_PieceCaisse()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(typeInstrumentForce: TypeInstrumentPaiement.PieceCaisse))));

        Assert.Equal(1, result.Reussies);
        Assert.NotNull(repo.PiecesCaisse.SingleOrDefault(p => p.FK_DemandePaiement == id));
    }

    [Fact]
    public async Task EtablirDocuments_SansPermission_ErrorAccessDenied()
    {
        var (repo, _, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var lecteur = DemandePaiementTestData.CreateService(repo, [AppPermissions.PaiementsLire]);
        var result = await CreateBatch(lecteur, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task EtablirDocuments_AutreUb_ErrorAccessDenied()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        repo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        repo.UbDepartements[10] = 1;
        repo.UbDepartements[20] = 2;
        var row = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        row.FK_UniteBudgetaire = 20;
        row.UniteBudgetaire = new BudgetWeb.Domain.Entities.UniteBudgetaire
        {
            IdUB = 20,
            CodeUB = "UB020",
            Libelle = "UB hors",
            FK_Departement = 2,
            Actif = true,
        };

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task EtablirDocuments_AssigneeAutreCharge_ErrorAccessDenied()
    {
        var (repo, y1, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        _ = y1;
        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        var result = await CreateBatch(y2, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task EtablirDocuments_PoolCharge_Autorise()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, result.Details[0].StatutApres);
    }

    [Fact]
    public async Task EtablirDocuments_IsolationDemandeur_ErrorAccessDenied()
    {
        var (repo, _, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var demandeur = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var result = await CreateBatch(demandeur, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task EtablirDocuments_EnTraitement_Success()
    {
        var (repo, charge, id) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments((id, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(DemandePaiementBatchOutcome.Success, result.Details[0].Outcome);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, result.Details[0].StatutAvant);
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, result.Details[0].StatutApres);
    }

    [Fact]
    public async Task EtablirDocuments_DevenueSoumise_IgnoreStatutMismatch()
    {
        var (repo, charge, idOk) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var (_, _, idMismatch) = await PrepEtablirDocsReadyAsync(devise: "CDF", existingRepo: repo);
        DemandePaiementRoutageTestHelpers.Tracked(repo, idMismatch).Statut = StatutDemandePaiement.Soumise;

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments(
                (idOk, Docs(piece: new EtablirPieceCaisseRequest())),
                (idMismatch, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(DemandePaiementBatchErrorCode.StatutMismatch,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idMismatch).CodeErreur);
    }

    [Fact]
    public async Task EtablirDocuments_DejaOrientee_IgnoreStatutMismatch()
    {
        var (repo, charge, idOk) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var (_, _, idMismatch) = await PrepEtablirDocsReadyAsync(devise: "CDF", existingRepo: repo);
        DemandePaiementRoutageTestHelpers.Tracked(repo, idMismatch).Statut =
            StatutDemandePaiement.EnControleBudgetaire;

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments(
                (idOk, Docs(piece: new EtablirPieceCaisseRequest())),
                (idMismatch, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(1, result.Ignorees);
        Assert.Equal(DemandePaiementBatchErrorCode.StatutMismatch,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idMismatch).CodeErreur);
    }

    [Fact]
    public async Task EtablirDocuments_Isolation_SuccessErrorSuccess_Http200()
    {
        var (repo, charge, id1) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var (_, _, id2) = await PrepEtablirDocsReadyAsync(
            devise: "USD",
            mode: ModePaiementDpm.Banque,
            seedInstrumentParam: TypeInstrumentPaiement.PieceCaisse,
            existingRepo: repo);
        var (_, _, id3) = await PrepEtablirDocsReadyAsync(devise: "CDF", existingRepo: repo);
        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));

        var http = await ctrl.EtablirDocuments(
            ReqDocuments(
                (id1, Docs(piece: new EtablirPieceCaisseRequest())),
                (id2, Docs(piece: new EtablirPieceCaisseRequest())),
                (id3, Docs(piece: new EtablirPieceCaisseRequest()))),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(http);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var result = Assert.IsType<DemandePaiementBatchResultDto>(ok.Value);
        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id1).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Error,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id2).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id3).Outcome);
    }

    [Fact]
    public async Task EtablirDocuments_ConcurrenceSurUne_AutresContinuent()
    {
        var (repo, charge, idA) = await PrepEtablirDocsReadyAsync(devise: "CDF");
        var (_, _, idB) = await PrepEtablirDocsReadyAsync(devise: "CDF", existingRepo: repo);
        var (_, _, idC) = await PrepEtablirDocsReadyAsync(devise: "CDF", existingRepo: repo);
        repo.ForceConcurrencyOnDocumentSaveIds.Add(idB);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.EtablirDocuments,
            ReqDocuments(
                (idA, Docs(piece: new EtablirPieceCaisseRequest())),
                (idB, Docs(piece: new EtablirPieceCaisseRequest())),
                (idC, Docs(piece: new EtablirPieceCaisseRequest()))));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.Concurrency,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idB).CodeErreur);
    }

    [Fact]
    public void EtablirDocuments_Exactement100_Accepte()
    {
        var items = Enumerable.Range(1, 100)
            .Select(i => new DemandePaiementBatchItemRequest(i, Documents: Docs(piece: new EtablirPieceCaisseRequest())))
            .ToList();
        DemandePaiementBatchOperationRules.ValidateRequest(
            DemandePaiementBatchOperation.EtablirDocuments,
            new DemandePaiementBatchRequest(StatutDemandePaiement.EnTraitementDpm, items));
    }

    // --- Phase 3D : orientation batch ---

    private static DemandePaiementBatchRequest ReqOrientation(
        params (long Id, OrienterDemandePaiementRequest? Orientation)[] items)
        => new(
            StatutDemandePaiement.EnTraitementDpm,
            items.Select(x => new DemandePaiementBatchItemRequest(x.Id, Orientation: x.Orientation)).ToList());

    private static async Task<(FakeDemandePaiementRepo Repo, DemandePaiementService Charge, long Id)> PrepOrienterReadyAsync(
        FakeDemandePaiementRepo? existingRepo = null)
    {
        var (repo, charge, id) = await PrepTraiterReadyAsync(devise: "CDF", existingRepo: existingRepo);
        var result = await charge.TraiterChargeAsync(
            id,
            Traitement(TypeInstrumentPaiement.PieceCaisse, "CDF", ModePaiementDpm.Caisse, TypeBudgetCode.DepensesCourantes));
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, result.Statut);
        return (repo, charge, id);
    }

    [Fact]
    public async Task Orienter_Pool_OrientationNull_Success()
    {
        var (repo, charge, id) = await PrepOrienterReadyAsync();
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Orienter,
            ReqOrientation((id, null)));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, result.Details[0].StatutApres);
        Assert.Null(repo.Demandes.Single(d => d.IdDemandePaiement == id).FK_UtilisateurAssigne);
        Assert.Contains(repo.Audits, a => a.Operation == "ORIENTER" && a.IdEntite == id);
    }

    [Fact]
    public async Task Orienter_Pool_OrientationIdNull_Success()
    {
        var (repo, charge, id) = await PrepOrienterReadyAsync();
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Orienter,
            ReqOrientation((id, new OrienterDemandePaiementRequest(null))));

        Assert.Equal(1, result.Reussies);
        Assert.Null(repo.Demandes.Single(d => d.IdDemandePaiement == id).FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task Orienter_Nominatif_Success()
    {
        var (repo, charge, id) = await PrepOrienterReadyAsync();
        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Orienter,
            ReqOrientation((id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1))));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, result.Details[0].StatutApres);
        Assert.Equal(
            DemandePaiementRoutageTestHelpers.Z1,
            repo.Demandes.Single(d => d.IdDemandePaiement == id).FK_UtilisateurAssigne);
        var routage = Assert.Single(repo.Routages, r => r.FK_DemandePaiement == id && r.EstActif);
        Assert.Equal(DemandePaiementRoutageAction.Orienter, routage.Action);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, routage.FK_UtilisateurCible);
    }

    [Fact]
    public async Task Orienter_Heterogene_A_B_Pool_Success()
    {
        var (repo, charge, idA) = await PrepOrienterReadyAsync();
        var (_, _, idB) = await PrepOrienterReadyAsync(existingRepo: repo);
        var (_, _, idPool) = await PrepOrienterReadyAsync(existingRepo: repo);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Orienter,
            ReqOrientation(
                (idA, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1)),
                (idB, new OrienterDemandePaiementRequest(302L)),
                (idPool, null)));

        Assert.Equal(3, result.Reussies);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1,
            repo.Demandes.Single(d => d.IdDemandePaiement == idA).FK_UtilisateurAssigne);
        Assert.Equal(302L, repo.Demandes.Single(d => d.IdDemandePaiement == idB).FK_UtilisateurAssigne);
        Assert.Null(repo.Demandes.Single(d => d.IdDemandePaiement == idPool).FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task Orienter_StatutIncompatible_IgnoreStatutMismatch()
    {
        var (repo, charge, idOk) = await PrepOrienterReadyAsync();
        var (_, _, idMismatch) = await PrepOrienterReadyAsync(existingRepo: repo);
        DemandePaiementRoutageTestHelpers.Tracked(repo, idMismatch).Statut = StatutDemandePaiement.Brouillon;

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Orienter,
            ReqOrientation((idOk, null), (idMismatch, null)));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(DemandePaiementBatchErrorCode.StatutMismatch,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idMismatch).CodeErreur);
        Assert.DoesNotContain(repo.Audits, a => a.Operation == "ORIENTER" && a.IdEntite == idMismatch);
    }

    [Fact]
    public async Task Orienter_SansPermission_ErrorAccessDenied()
    {
        var (repo, _, id) = await PrepOrienterReadyAsync();
        var lecteur = DemandePaiementTestData.CreateService(repo, [AppPermissions.PaiementsLire]);
        var result = await CreateBatch(lecteur, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Orienter,
            ReqOrientation((id, null)));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task Orienter_AssigneeAutreCharge_ErrorAccessDenied()
    {
        var (repo, y1, id) = await PrepOrienterReadyAsync();
        _ = y1;
        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        var result = await CreateBatch(y2, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Orienter,
            ReqOrientation((id, null)));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
    }

    [Fact]
    public async Task Orienter_ConcurrenceSurUne_AutresContinuent()
    {
        var (repo, charge, idA) = await PrepOrienterReadyAsync();
        var (_, _, idB) = await PrepOrienterReadyAsync(existingRepo: repo);
        var (_, _, idC) = await PrepOrienterReadyAsync(existingRepo: repo);
        repo.ForceConcurrencyOnUpdateIds.Add(idB);

        var result = await CreateBatch(charge, repo).ExecuterAsync(
            DemandePaiementBatchOperation.Orienter,
            ReqOrientation((idA, null), (idB, null), (idC, null)));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.Concurrency,
            Assert.Single(result.Details, d => d.IdDemandePaiement == idB).CodeErreur);
    }

    [Fact]
    public async Task Orienter_Isolation_SuccessErrorSuccess_Http200()
    {
        var (repo, charge, id1) = await PrepOrienterReadyAsync();
        var (_, _, id2) = await PrepOrienterReadyAsync(existingRepo: repo);
        var (_, _, id3) = await PrepOrienterReadyAsync(existingRepo: repo);
        repo.ForceConcurrencyOnUpdateIds.Add(id2);

        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));
        var http = await ctrl.Orienter(
            ReqOrientation((id1, null), (id2, null), (id3, null)),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(http);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var result = Assert.IsType<DemandePaiementBatchResultDto>(ok.Value);
        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id1).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Error,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id2).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == id3).Outcome);
    }

    [Fact]
    public void Orienter_IdsDupliques_Refuse()
    {
        Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.Orienter,
                ReqOrientation((1, null), (1, null))));
    }

    [Fact]
    public void Orienter_FiltreAbsent_RefuseInvalidStatusFilter()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.Orienter,
                new DemandePaiementBatchRequest("", [new DemandePaiementBatchItemRequest(1)])));
        Assert.Equal("INVALID_STATUS_FILTER", ex.ErrorCode);
        Assert.Contains("EN_TRAITEMENT_DPM", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Orienter_MauvaisFiltre_RefuseInvalidStatusFilter()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.Orienter,
                new DemandePaiementBatchRequest(
                    StatutDemandePaiement.EnControleBudgetaire,
                    [new DemandePaiementBatchItemRequest(1)])));
        Assert.Equal("INVALID_STATUS_FILTER", ex.ErrorCode);
    }

    [Fact]
    public async Task Controller_Orienter_FiltreToutes_Http400()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementRoutageTestHelpers.Charge(repo);
        var ctrl = new DemandePaiementsBatchController(CreateBatch(charge, repo));
        var http = await ctrl.Orienter(
            new DemandePaiementBatchRequest("", [new DemandePaiementBatchItemRequest(1)]),
            CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(http);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var body = Assert.IsType<ApiErrorResponse>(bad.Value);
        Assert.Equal("INVALID_STATUS_FILTER", body.Code);
    }

    [Fact]
    public void Orienter_101_Refuse()
    {
        var items = Enumerable.Range(1, 101).Select(i => new DemandePaiementBatchItemRequest(i)).ToList();
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.Orienter,
                new DemandePaiementBatchRequest(StatutDemandePaiement.EnTraitementDpm, items)));
        Assert.Contains("100", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Orienter_Exactement100_Accepte()
    {
        var items = Enumerable.Range(1, 100).Select(i => new DemandePaiementBatchItemRequest(i)).ToList();
        DemandePaiementBatchOperationRules.ValidateRequest(
            DemandePaiementBatchOperation.Orienter,
            new DemandePaiementBatchRequest(StatutDemandePaiement.EnTraitementDpm, items));
    }

    private static DemandePaiementBatchRequest ReqRejet(
        string statut,
        string motif,
        params long[] ids)
        => new(
            statut,
            ids.Select(id => new DemandePaiementBatchItemRequest(
                id,
                Retour: new RetourDemandePaiementRequest(motif, null))).ToList());

    [Fact]
    public void Supprimer_FiltreIncoherent_Refuse()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.SupprimerBrouillon,
                Req(StatutDemandePaiement.EnValidationN1, 1)));
        Assert.Equal("INVALID_STATUS_FILTER", ex.ErrorCode);
    }

    [Fact]
    public void RejeterN1_SansRetour_Refuse()
    {
        var ex = Assert.Throws<DemandePaiementBatchRequestException>(() =>
            DemandePaiementBatchOperationRules.ValidateRequest(
                DemandePaiementBatchOperation.RejeterValidationN1,
                Req(StatutDemandePaiement.EnValidationN1, 1)));
        Assert.Contains("motif", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SupprimerBrouillon_BatchHomogene_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var b = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "B"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, b.IdDemandePaiement);

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.SupprimerBrouillon,
            Req(StatutDemandePaiement.Brouillon, a.IdDemandePaiement, b.IdDemandePaiement));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(0, result.Erreurs);
        Assert.All(result.Details, d =>
        {
            Assert.Equal(DemandePaiementBatchOutcome.Success, d.Outcome);
            Assert.Null(d.StatutApres);
        });
        Assert.DoesNotContain(repo.Demandes, d => d.IdDemandePaiement == a.IdDemandePaiement);
        Assert.DoesNotContain(repo.Demandes, d => d.IdDemandePaiement == b.IdDemandePaiement);
    }

    [Fact]
    public async Task SupprimerBrouillon_ErreurSurUne_NArretePasLesAutres()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var b = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "B"));
        var c = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "C"));
        await DemandePaiementTestData.AddSamplePieceAsync(svc, a.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, b.IdDemandePaiement);
        await DemandePaiementTestData.AddSamplePieceAsync(svc, c.IdDemandePaiement);
        await svc.EnvoyerEnValidationAsync(b.IdDemandePaiement);

        var result = await CreateBatch(svc, repo).ExecuterAsync(
            DemandePaiementBatchOperation.SupprimerBrouillon,
            Req(StatutDemandePaiement.Brouillon, a.IdDemandePaiement, b.IdDemandePaiement, c.IdDemandePaiement));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Ignorees);
        Assert.Equal(0, result.Erreurs);
        Assert.Equal(
            DemandePaiementBatchOutcome.Ignored,
            Assert.Single(result.Details, d => d.IdDemandePaiement == b.IdDemandePaiement).Outcome);
        Assert.DoesNotContain(repo.Demandes, d => d.IdDemandePaiement == a.IdDemandePaiement);
        Assert.Contains(repo.Demandes, d => d.IdDemandePaiement == b.IdDemandePaiement);
        Assert.DoesNotContain(repo.Demandes, d => d.IdDemandePaiement == c.IdDemandePaiement);
    }

    [Fact]
    public async Task SupprimerBrouillon_SansEcrire_ErrorAccessDenied()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var writer = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await writer.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(writer, created.IdDemandePaiement);

        var reader = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire]);
        var result = await CreateBatch(reader, repo).ExecuterAsync(
            DemandePaiementBatchOperation.SupprimerBrouillon,
            Req(StatutDemandePaiement.Brouillon, created.IdDemandePaiement));

        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchErrorCode.AccessDenied, result.Details[0].CodeErreur);
        Assert.Contains(repo.Demandes, d => d.IdDemandePaiement == created.IdDemandePaiement);
    }

    [Fact]
    public async Task RejeterValidationN1_Batch_PasseACorriger()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);

        var n1 = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var result = await CreateBatch(n1, repo).ExecuterAsync(
            DemandePaiementBatchOperation.RejeterValidationN1,
            ReqRejet(StatutDemandePaiement.EnValidationN1, "Correction requise", created.IdDemandePaiement));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, result.Details[0].StatutAvant);
        Assert.Equal(StatutDemandePaiement.ACorriger, result.Details[0].StatutApres);
        Assert.Equal(
            StatutDemandePaiement.ACorriger,
            repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task RejeterValidationN1_ErreurSurUne_NArretePasLesAutres()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var a = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var b = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "B"));
        var c = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "C"));
        foreach (var id in new[] { a.IdDemandePaiement, b.IdDemandePaiement, c.IdDemandePaiement })
        {
            await DemandePaiementTestData.AddSamplePieceAsync(agent, id);
            await agent.EnvoyerEnValidationAsync(id);
        }

        repo.ForceConcurrencyOnUpdateIds.Add(b.IdDemandePaiement);

        var n1 = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var result = await CreateBatch(n1, repo).ExecuterAsync(
            DemandePaiementBatchOperation.RejeterValidationN1,
            ReqRejet(
                StatutDemandePaiement.EnValidationN1,
                "Motif batch",
                a.IdDemandePaiement,
                b.IdDemandePaiement,
                c.IdDemandePaiement));

        Assert.Equal(2, result.Reussies);
        Assert.Equal(1, result.Erreurs);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == a.IdDemandePaiement).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Error,
            Assert.Single(result.Details, d => d.IdDemandePaiement == b.IdDemandePaiement).Outcome);
        Assert.Equal(DemandePaiementBatchOutcome.Success,
            Assert.Single(result.Details, d => d.IdDemandePaiement == c.IdDemandePaiement).Outcome);
        Assert.Equal(StatutDemandePaiement.ACorriger,
            repo.Demandes.Single(d => d.IdDemandePaiement == a.IdDemandePaiement).Statut);
        Assert.Equal(StatutDemandePaiement.EnValidationN1,
            repo.Demandes.Single(d => d.IdDemandePaiement == b.IdDemandePaiement).Statut);
        Assert.Equal(StatutDemandePaiement.ACorriger,
            repo.Demandes.Single(d => d.IdDemandePaiement == c.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task RejeterValidationN2_Batch_PasseEnValidationN1()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var created = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(agent, created.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.CreateService(
                repo,
                AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur))
            .ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        var n2 = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));
        var result = await CreateBatch(n2, repo).ExecuterAsync(
            DemandePaiementBatchOperation.RejeterValidationN2,
            ReqRejet(StatutDemandePaiement.EnValidationN2, "Retour N1", created.IdDemandePaiement));

        Assert.Equal(1, result.Reussies);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, result.Details[0].StatutAvant);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, result.Details[0].StatutApres);
        Assert.Equal(
            StatutDemandePaiement.EnValidationN1,
            repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement).Statut);
    }

    [Fact]
    public async Task Controller_Supprimer_Et_Rejeter_Endpoints_AcceptentCorpsValide()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var agent = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));
        var brouillon = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(agent, brouillon.IdDemandePaiement);

        var n1Demande = await agent.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest(objet: "N1"));
        await DemandePaiementTestData.AddSamplePieceAsync(agent, n1Demande.IdDemandePaiement);
        await agent.EnvoyerEnValidationAsync(n1Demande.IdDemandePaiement);

        var ctrlAgent = new DemandePaiementsBatchController(CreateBatch(agent, repo));
        var delHttp = await ctrlAgent.Supprimer(
            Req(StatutDemandePaiement.Brouillon, brouillon.IdDemandePaiement),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(delHttp);

        var n1 = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var ctrlN1 = new DemandePaiementsBatchController(CreateBatch(n1, repo));
        var rejHttp = await ctrlN1.RejeterValidationN1(
            ReqRejet(StatutDemandePaiement.EnValidationN1, "Motif", n1Demande.IdDemandePaiement),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(rejHttp);
    }
}