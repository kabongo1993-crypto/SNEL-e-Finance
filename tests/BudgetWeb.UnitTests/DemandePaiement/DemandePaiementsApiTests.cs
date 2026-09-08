using BudgetWeb.API.Controllers.V1;
using BudgetWeb.Application.DpmConsultation;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementsApiTests
{
    private static DemandePaiementsController Ctrl(IDemandePaiementService svc)
        => new(svc, NullLogger<DemandePaiementsController>.Instance);

    private sealed class StubDemandePaiementService : IDemandePaiementService
    {
        public Func<DemandePaiementQuery, Task<IReadOnlyList<DemandePaiementListDto>>>? OnList { get; set; }
        public Func<long, Task<DemandePaiementDetailCompletDto?>>? OnDetailComplet { get; set; }
        public Func<long, Task<DemandePaiementDetailCompletDto?>>? OnDetailConsultation { get; set; }
        public Func<CreateDemandePaiementRequest, Task<DemandePaiementDetailDto>>? OnCreate { get; set; }
        public Func<long, UpdateDemandePaiementRequest, Task<DemandePaiementDetailDto>>? OnUpdate { get; set; }
        public Func<long, Task<DemandePaiementDetailDto>>? OnSoumettre { get; set; }
        public Func<long, Task<DemandePaiementDetailDto>>? OnReceptionner { get; set; }
        public Func<long, Task<DemandePaiementDetailDto>>? OnEntrerTraitement { get; set; }
        public Func<long, TraitementChargeDpmRequest, Task<DemandePaiementDetailDto>>? OnTraiterCharge { get; set; }
        public Func<long, RetenirSollicitationChargeRequest, Task<DemandePaiementDetailDto>>? OnRetenirSollicitationCharge { get; set; }
        public Func<long, OrienterDemandePaiementRequest, Task<DemandePaiementDetailDto>>? OnOrienter { get; set; }
        public Func<long, Task<DemandePaiementDetailDto>>? OnPrendreEnControle { get; set; }
        public Func<long, RetourDemandePaiementRequest, Task<DemandePaiementDetailDto>>? OnRetourner { get; set; }
        public Func<long, Task<DemandePaiementDetailDto>>? OnRemettreEnBrouillon { get; set; }
        public Func<long, Task<DemandePaiementDetailDto>>? OnViser { get; set; }
        public Func<long, Task<ControleBudgetaireDto>>? OnControle { get; set; }
        public Func<long, CreateImputationRequest, Task<DemandePaiementImputationDto>>? OnAddImputation { get; set; }
        public Func<LigneBudgetaireDisponibleQuery, Task<LigneBudgetaireDisponibleDto>>? OnLigne { get; set; }
        public Func<long, Task<HistoriqueDemandePaiementDto?>>? OnHistorique { get; set; }
        public Func<long, Task<IReadOnlyList<DemandePaiementRoutageDto>?>>? OnRoutage { get; set; }
        public Func<long, Task<IReadOnlyList<DemandePaiementRetourDestinataireDto>?>>? OnRetoursDestinataires { get; set; }
        public Func<long, Task<IReadOnlyList<DemandePaiementPieceDto>>>? OnPieces { get; set; }

        public Task<IReadOnlyList<DemandePaiementListDto>> ListAsync(
            DemandePaiementQuery query,
            CancellationToken cancellationToken = default)
            => OnList!(query);

        public Task<DemandePaiementCompteursDto> GetCompteursAsync(
            DemandePaiementCompteursQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DemandePaiementCompteursDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task<DemandePaiementDetailDto?> GetByIdAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult<DemandePaiementDetailDto?>(OnDetailComplet is null ? null : OnDetailComplet(idDemande).Result?.Demande);

        public Task<DemandePaiementDetailCompletDto?> GetDetailCompletAsync(
            long idDemande,
            CancellationToken cancellationToken = default)
            => OnDetailComplet!(idDemande);

        public Task<DemandePaiementDetailCompletDto?> GetDetailConsultationAsync(
            long idDemande,
            CancellationToken cancellationToken = default)
            => OnDetailConsultation!(idDemande);

        public Task<IReadOnlyList<DemandePaiementPieceDto>> GetPiecesAsync(
            long idDemande,
            CancellationToken cancellationToken = default)
            => OnPieces!(idDemande);

        public Task<LigneBudgetaireDisponibleDto> GetLigneBudgetaireDisponibleAsync(
            LigneBudgetaireDisponibleQuery query,
            CancellationToken cancellationToken = default)
            => OnLigne!(query);

        public Task<DemandePaiementDetailDto> CreateBrouillonAsync(
            CreateDemandePaiementRequest request,
            CancellationToken cancellationToken = default)
            => OnCreate!(request);

        public Task<DemandePaiementDetailDto> UpdateBrouillonAsync(
            long idDemande,
            UpdateDemandePaiementRequest request,
            CancellationToken cancellationToken = default)
            => OnUpdate!(idDemande, request);

        public Task<DemandePaiementImputationDto> AddImputationAsync(
            long idDemande,
            CreateImputationRequest request,
            CancellationToken cancellationToken = default)
            => OnAddImputation!(idDemande, request);

        public Task<DemandePaiementImputationDto> UpdateImputationAsync(
            long idDemande,
            long idImputation,
            UpdateImputationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteImputationAsync(long idDemande, long idImputation, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<GrilleImputationDcDto> GetGrilleImputationDcAsync(
            long idDemande,
            byte? mois = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<GrilleImputationDcDto> EnregistrerImputationsDcAsync(
            long idDemande,
            EnregistrerImputationsDcRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<GrilleImputationAeDto> GetGrilleImputationAeAsync(
            long idDemande,
            string libelleItemAE,
            long? idGroupeItemAE = null,
            byte? mois = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<GrilleImputationAeDto> EnregistrerImputationsAeAsync(
            long idDemande,
            EnregistrerImputationsAeRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<GrilleImputationBiDto> GetGrilleImputationBiAsync(
            long idDemande,
            long idItemBI,
            byte? mois = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<GrilleImputationBiDto> EnregistrerImputationsBiAsync(
            long idDemande,
            EnregistrerImputationsBiRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementPieceDto> AddPieceAsync(
            long idDemande,
            UploadDemandePaiementPieceMetadata metadata,
            Stream content,
            string originalFileName,
            string? contentType,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeletePieceAsync(long idDemande, long idPiece, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Func<long, Task<bool>>? OnDeleteBrouillon { get; set; }

        public Task<bool> DeleteBrouillonAsync(long idDemande, CancellationToken cancellationToken = default)
            => OnDeleteBrouillon is null
                ? Task.FromResult(false)
                : OnDeleteBrouillon(idDemande);

        public Task<DemandePaiementPieceContentDto?> GetPieceContentAsync(
            long idDemande,
            long idPiece,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DemandePaiementPieceContentDto?>(null);

        public Task<DemandePaiementDetailDto> SoumettreAsync(long idDemande, CancellationToken cancellationToken = default)
            => OnSoumettre!(idDemande);

        public Task<DemandePaiementDetailDto> ReceptionnerAsync(long idDemande, CancellationToken cancellationToken = default)
            => OnReceptionner!(idDemande);

        public Task<DemandePaiementDetailDto> EntrerTraitementAsync(long idDemande, CancellationToken cancellationToken = default)
            => OnEntrerTraitement!(idDemande);

        public Task<DemandePaiementDetailDto> TraiterChargeAsync(
            long idDemande,
            TraitementChargeDpmRequest request,
            CancellationToken cancellationToken = default)
            => OnTraiterCharge!(idDemande, request);

        public Task<DemandePaiementDetailDto> RetenirSollicitationChargeAsync(
            long idDemande,
            RetenirSollicitationChargeRequest request,
            CancellationToken cancellationToken = default)
            => OnRetenirSollicitationCharge is null
                ? throw new NotImplementedException()
                : OnRetenirSollicitationCharge(idDemande, request);

        public Task<DemandePaiementDetailDto> OrienterAsync(
            long idDemande,
            OrienterDemandePaiementRequest? request,
            CancellationToken cancellationToken = default)
            => OnOrienter!(idDemande, request ?? new OrienterDemandePaiementRequest());

        public Task<DemandePaiementDetailDto> PrendreEnControleAsync(long idDemande, CancellationToken cancellationToken = default)
            => OnPrendreEnControle!(idDemande);

        public Task<ControleBudgetaireDto> ControlerBudgetaireAsync(long idDemande, CancellationToken cancellationToken = default)
            => OnControle!(idDemande);

        public Task<DemandePaiementDetailDto> RetournerAsync(
            long idDemande,
            RetourDemandePaiementRequest request,
            CancellationToken cancellationToken = default)
            => OnRetourner!(idDemande, request);

        public Task<DemandePaiementDetailDto> RemettreEnBrouillonAsync(long idDemande, CancellationToken cancellationToken = default)
            => OnRemettreEnBrouillon!(idDemande);

        public Task<DemandePaiementDetailDto> ViserAsync(long idDemande, CancellationToken cancellationToken = default)
            => OnViser!(idDemande);

        public Task<IReadOnlyList<PieceManquanteDto>> GetPiecesManquantesAsync(
            long idDemande,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PieceManquanteDto>>([]);

        public Task<HistoriqueDemandePaiementDto?> GetHistoriqueAsync(
            long idDemande,
            CancellationToken cancellationToken = default)
            => OnHistorique!(idDemande);

        public Task<IReadOnlyList<DemandePaiementRoutageDto>?> GetRoutageAsync(
            long idDemande,
            CancellationToken cancellationToken = default)
            => OnRoutage is null
                ? Task.FromResult<IReadOnlyList<DemandePaiementRoutageDto>?>([])
                : OnRoutage(idDemande);

        public Task<IReadOnlyList<DemandePaiementRetourDestinataireDto>?> GetRetoursDestinatairesAsync(
            long idDemande,
            CancellationToken cancellationToken = default)
            => OnRetoursDestinataires is null
                ? Task.FromResult<IReadOnlyList<DemandePaiementRetourDestinataireDto>?>([])
                : OnRetoursDestinataires(idDemande);

        public Task<DemandePaiementDetailDto> EnvoyerEnValidationAsync(long idDemande, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementDetailDto> ValiderN1ElectroniqueAsync(long idDemande, ValidationEntiteRequest? request = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementDetailDto> ValiderN2ElectroniqueAsync(long idDemande, ValidationEntiteRequest? request = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementDetailDto> DeclarerValidationPhysiqueN1Async(long idDemande, DeclarationValidationPhysiqueRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementDetailDto> DeclarerValidationPhysiqueN2Async(long idDemande, DeclarationValidationPhysiqueRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementDetailDto> RejeterValidationEntiteAsync(long idDemande, RetourDemandePaiementRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementDetailDto> AnnulerSoumissionAsync(long idDemande, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementDetailDto> AnnulerValidationN2Async(long idDemande, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementDetailDto> AnnulerValidationN1Async(long idDemande, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DemandePaiementDocumentDto?> GetDocumentImpressionAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult<DemandePaiementDocumentDto?>(null);

        public Task<byte[]> GenererDocumentPdfAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<FicheImputationBudgetaireDto> GetFicheImputationAsync(
            long idDemande,
            FicheImputationMode mode,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<byte[]> GenererFicheImputationPdfAsync(
            long idDemande,
            FicheImputationMode mode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<BilletConversionDto?> GetBilletConversionAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult<BilletConversionDto?>(null);

        public Task<BilletConversionDto> EtablirBilletConversionAsync(
            long idDemande,
            EtablirBilletConversionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<byte[]> GenererBilletConversionPdfAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<PieceCaisseDto?> GetPieceCaisseAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult<PieceCaisseDto?>(null);

        public Task<PieceCaisseDto> EtablirPieceCaisseAsync(
            long idDemande,
            EtablirPieceCaisseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<byte[]> GenererPieceCaissePdfAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<BonProvisoireDto?> GetBonProvisoireAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult<BonProvisoireDto?>(null);

        public Task<BonProvisoireDto> EtablirBonProvisoireAsync(
            long idDemande,
            EtablirBonProvisoireRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<byte[]> GenererBonProvisoirePdfAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<MinuteChequeDto?> GetMinuteChequeAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult<MinuteChequeDto?>(null);

        public Task<MinuteChequeDto> EtablirMinuteChequeAsync(
            long idDemande,
            EtablirMinuteChequeRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<byte[]> GenererMinuteChequePdfAsync(long idDemande, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());
    }

    private static DemandePaiementDetailDto SampleDetail(
        long id = 1,
        string statut = StatutDemandePaiement.Brouillon)
        => new(
            id,
            "DP-2026-00001",
            new DateOnly(2026, 3, 1),
            "Kinshasa",
            1,
            2026,
            null,
            null,
            10,
            "UB001",
            "Unité test",
            1,
            "DDK/DKC/DG",
            "Demandeur test",
            1,
            "DG",
            "Direction Générale",
            1,
            "CAS01",
            "Cas test",
            null,
            null,
            TypeBudgetCode.DepensesCourantes,
            null,
            "DC",
            "Objet test",
            null,
            100m,
            "USD",
            2,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            statut,
            null,
            null,
            DateTime.Now,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            []);

    private static DemandePaiementDetailCompletDto SampleComplet(
        long id = 1,
        string statut = StatutDemandePaiement.Brouillon)
        => new(
            SampleDetail(id, statut),
            [],
            null,
            [],
            [],
            null);

    [Fact]
    public async Task List_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnList = _ => Task.FromResult<IReadOnlyList<DemandePaiementListDto>>([]),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.List(null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetById_Inexistant_Retourne_404()
    {
        var svc = new StubDemandePaiementService
        {
            OnDetailConsultation = _ => Task.FromResult<DemandePaiementDetailCompletDto?>(null),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.GetById(999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetById_Retourne_Detail_Consultation()
    {
        var svc = new StubDemandePaiementService
        {
            OnDetailConsultation = _ => Task.FromResult<DemandePaiementDetailCompletDto?>(SampleComplet()),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.GetById(1, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<DemandePaiementDetailCompletDto>(ok.Value);
    }

    [Fact]
    public async Task GetComplet_Retourne_Detail_Complet()
    {
        var svc = new StubDemandePaiementService
        {
            OnDetailComplet = _ => Task.FromResult<DemandePaiementDetailCompletDto?>(SampleComplet()),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.GetComplet(1, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<DemandePaiementDetailCompletDto>(ok.Value);
    }

    [Fact]
    public async Task Create_Retourne_201()
    {
        var svc = new StubDemandePaiementService
        {
            OnCreate = _ => Task.FromResult(SampleDetail()),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Create(
            DemandePaiementTestData.SampleCreateRequest(),
            CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task Update_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnUpdate = (_, _) => Task.FromResult(SampleDetail()),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Update(1, DemandePaiementTestData.SampleUpdateRequest(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Soumettre_Valide_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnSoumettre = _ => Task.FromResult(SampleDetail(statut: StatutDemandePaiement.Soumise)),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Soumettre(1, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DemandePaiementDetailDto>(ok.Value);
        Assert.Equal(StatutDemandePaiement.Soumise, dto.Statut);
    }

    [Fact]
    public async Task Soumettre_Piece_Manquante_Retourne_400()
    {
        var svc = new StubDemandePaiementService
        {
            OnSoumettre = _ => throw new InvalidOperationException("La pièce obligatoire « Facture fournisseur » est manquante."),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Soumettre(1, CancellationToken.None);
        var bad = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task Receptionner_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnReceptionner = _ => Task.FromResult(SampleDetail(statut: StatutDemandePaiement.EnTraitementDpm)),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Receptionner(1, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task PrendreEnCharge_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnPrendreEnControle = _ => Task.FromResult(SampleDetail(statut: StatutDemandePaiement.EnControleBudgetaire)),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.PrendreEnCharge(1, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Retourner_Retourne_Motif()
    {
        var svc = new StubDemandePaiementService
        {
            OnRetourner = (_, _) => Task.FromResult(
                SampleDetail(statut: StatutDemandePaiement.ACorriger) with { MotifRetour = "Pièces incomplètes" }),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Retourner(
            1,
            new RetourDemandePaiementRequest("Pièces incomplètes", "Joindre facture"),
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<DemandePaiementDetailDto>(ok.Value);
        Assert.Equal("Pièces incomplètes", dto.MotifRetour);
    }

    [Fact]
    public async Task Viser_Valide_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnViser = _ => Task.FromResult(SampleDetail(statut: StatutDemandePaiement.ViseeBudgetairement)),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Viser(1, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatutDemandePaiement.ViseeBudgetairement, ((DemandePaiementDetailDto)ok.Value!).Statut);
    }

    [Fact]
    public async Task Viser_Credit_Insuffisant_Retourne_400()
    {
        var svc = new StubDemandePaiementService
        {
            OnViser = _ => throw new InvalidOperationException("Le crédit disponible est insuffisant."),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Viser(1, CancellationToken.None);
        var bad = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task Viser_Sans_Pieces_Retourne_400()
    {
        var svc = new StubDemandePaiementService
        {
            OnViser = _ => throw new InvalidOperationException("La pièce obligatoire « Facture fournisseur » est manquante."),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Viser(1, CancellationToken.None);
        var bad = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task AddImputation_Dc_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnAddImputation = (_, _) => Task.FromResult(
                new DemandePaiementImputationDto(
                    1, 1, 1, TypeBudgetCode.DepensesCourantes, 1, 1, 10, 3, null, null, null, null, null, 100m, "USD", 1m, 100m, null)),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.AddImputation(1, DemandePaiementTestData.ImputationDc(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task AddImputation_Ae_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnAddImputation = (_, _) => Task.FromResult(
                new DemandePaiementImputationDto(
                    2, 1, 2, TypeBudgetCode.ActionsExploitation, 1, 1, 10, null, "Item AE", null, null, null, null, 500m, "USD", 1m, 500m, null)),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.AddImputation(1, DemandePaiementTestData.ImputationAe(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task AddImputation_Bi_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnAddImputation = (_, _) => Task.FromResult(
                new DemandePaiementImputationDto(
                    3, 1, 3, TypeBudgetCode.BudgetInvestissement, 1, 1, null, null, null, null, 5, "Détail BI", null, 1000m, "USD", 1m, 1000m, null)),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.AddImputation(1, DemandePaiementTestData.ImputationBi(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task LigneBudgetaire_Sans_Prevision_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnLigne = _ => Task.FromResult(
                new LigneBudgetaireDisponibleDto(null, TypeBudgetCode.DepensesCourantes, 0, 0, 0, 0, 0, 0, 0, false)),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.LignesBudgetairesDisponibles(
            1, 1, 1, 10, 3, null, null, null, null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ControleBudgetaire_Retourne_Resultat_Sans_Engagement()
    {
        var svc = new StubDemandePaiementService
        {
            OnControle = _ => Task.FromResult(new ControleBudgetaireDto(true, null, [])),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.ControleBudgetaire(1, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ControleBudgetaireDto>(ok.Value);
        Assert.True(dto.EstValide);
    }

    [Fact]
    public async Task Permission_Insuffisante_Retourne_401()
    {
        var svc = new StubDemandePaiementService
        {
            OnList = _ => throw new UnauthorizedAccessException("Permission requise : paiements.lire."),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.List(null, null, null, null, null, null, null, null, null, null, null, null, CancellationToken.None);
        var unauthorized = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Ub_Non_Autorisee_Retourne_401()
    {
        var svc = new StubDemandePaiementService
        {
            OnCreate = _ => throw new UnauthorizedAccessException("Vous n'avez pas accès à cette unité budgétaire."),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Create(DemandePaiementTestData.SampleCreateRequest(), CancellationToken.None);
        var unauthorized = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Historique_Retourne_200()
    {
        var svc = new StubDemandePaiementService
        {
            OnHistorique = _ => Task.FromResult<HistoriqueDemandePaiementDto?>(
                new HistoriqueDemandePaiementDto(1, "DP-2026-00001", [])),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Historique(1, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Routage_Inexistant_Retourne_404()
    {
        var svc = new StubDemandePaiementService
        {
            OnRoutage = _ => Task.FromResult<IReadOnlyList<DemandePaiementRoutageDto>?>(null),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Routage(999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Routage_Retourne_Contrat_Attendu()
    {
        var expected = new List<DemandePaiementRoutageDto>
        {
            new(
                1,
                DemandePaiementRoutageAction.Receptionner,
                StatutDemandePaiement.Soumise,
                StatutDemandePaiement.EnTraitementDpm,
                10,
                "Dupont",
                "Jean",
                101,
                "Martin",
                "Paul",
                new DateTime(2026, 3, 1, 10, 0, 0),
                true,
                null),
        };

        var svc = new StubDemandePaiementService
        {
            OnRoutage = _ => Task.FromResult<IReadOnlyList<DemandePaiementRoutageDto>?>(expected),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Routage(1, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<DemandePaiementRoutageDto>>(ok.Value);
        var row = Assert.Single(rows);
        Assert.Equal(DemandePaiementRoutageAction.Receptionner, row.Action);
        Assert.Equal("Martin", row.NomUtilisateurCible);
    }

    [Fact]
    public async Task RetoursDestinataires_Retourne_Contrat_Attendu()
    {
        var expected = new List<DemandePaiementRetourDestinataireDto>
        {
            new(
                2,
                DemandePaiementRetourType.JuniorVersCharge,
                DemandePaiementRoutageAction.RetourInterEtapes,
                StatutDemandePaiement.EnControleBudgetaire,
                StatutDemandePaiement.EnTraitementDpm,
                101,
                "Martin",
                "Paul",
                new DateTime(2026, 3, 2, 11, 0, 0),
                "Correction"),
        };

        var svc = new StubDemandePaiementService
        {
            OnRetoursDestinataires = _ => Task.FromResult<IReadOnlyList<DemandePaiementRetourDestinataireDto>?>(expected),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.RetoursDestinataires(1, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var row = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<DemandePaiementRetourDestinataireDto>>(ok.Value));
        Assert.Equal(DemandePaiementRetourType.JuniorVersCharge, row.TypeRetour);
        Assert.Equal("Martin", row.NomUtilisateurDestinataire);
    }

    [Fact]
    public async Task ConcurrencyException_Retourne_409()
    {
        var svc = new StubDemandePaiementService
        {
            OnUpdate = (_, __) => throw new DemandePaiementConcurrencyException(),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Update(1, DemandePaiementTestData.SampleUpdateRequest(), CancellationToken.None);
        var conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task Workflow_Incompatible_Retourne_409()
    {
        var svc = new StubDemandePaiementService
        {
            OnSoumettre = _ => throw new InvalidOperationException("La demande doit être en brouillon."),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.Soumettre(1, CancellationToken.None);
        var conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task DeleteBrouillon_Retourne_204()
    {
        var svc = new StubDemandePaiementService
        {
            OnDeleteBrouillon = _ => Task.FromResult(true),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.DeleteBrouillon(1, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteBrouillon_Inexistant_Retourne_404()
    {
        var svc = new StubDemandePaiementService
        {
            OnDeleteBrouillon = _ => Task.FromResult(false),
        };
        var ctrl = Ctrl(svc);
        var result = await ctrl.DeleteBrouillon(999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Service_Integration_Create_Refuse_Ub_Non_Autorisee()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.UbAutorisees.Clear();
        repo.UbProxyPrevision.Clear();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsEcrire]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest()));
    }
}
