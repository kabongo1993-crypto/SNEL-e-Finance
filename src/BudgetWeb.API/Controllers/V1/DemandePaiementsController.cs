using BudgetWeb.API.Helpers;
using BudgetWeb.API.Infrastructure;
using BudgetWeb.Application.Diagnostics;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Controllers.V1;

/// <summary>Demandes de paiement — workflow jusqu'au visa budgétaire.</summary>
[ApiController]
[Route("api/v1/demandes-paiement")]
public class DemandePaiementsController : ControllerBase
{
    private readonly IDemandePaiementService _service;
    private readonly ILogger<DemandePaiementsController> _logger;

    public DemandePaiementsController(
        IDemandePaiementService service,
        ILogger<DemandePaiementsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Liste filtrée des demandes de paiement.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DemandePaiementListDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] string? scope,
        [FromQuery] long? idExercice,
        [FromQuery] long? idUB,
        [FromQuery] long? idDepartement,
        [FromQuery] long? idCasDossier,
        [FromQuery] long? idTypeBudget,
        [FromQuery] long? idDemandeur,
        [FromQuery] string? statut,
        [FromQuery] string? reference,
        [FromQuery] string? beneficiaire,
        [FromQuery] DateOnly? dateDebut,
        [FromQuery] DateOnly? dateFin,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var rows = await _service.ListAsync(
                new DemandePaiementQuery(
                    idExercice,
                    idUB,
                    idDepartement,
                    idCasDossier,
                    idTypeBudget,
                    idDemandeur,
                    statut,
                    reference,
                    beneficiaire,
                    dateDebut,
                    dateFin,
                    scope),
                cancellationToken);
            return Ok(rows);
        });

    /// <summary>Compteurs par statut (même périmètre métier que la liste, sans filtre statut).</summary>
    [HttpGet("compteurs")]
    [ProducesResponseType(typeof(DemandePaiementCompteursDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Compteurs(
        [FromQuery] string scope,
        [FromQuery] long? idExercice,
        [FromQuery] long? idUB,
        [FromQuery] long? idDepartement,
        [FromQuery] long? idCasDossier,
        [FromQuery] long? idTypeBudget,
        [FromQuery] long? idDemandeur,
        [FromQuery] string? reference,
        [FromQuery] string? beneficiaire,
        [FromQuery] DateOnly? dateDebut,
        [FromQuery] DateOnly? dateFin,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var compteurs = await _service.GetCompteursAsync(
                new DemandePaiementCompteursQuery(
                    scope,
                    idExercice,
                    idUB,
                    idDepartement,
                    idCasDossier,
                    idTypeBudget,
                    idDemandeur,
                    reference,
                    beneficiaire,
                    dateDebut,
                    dateFin),
                cancellationToken);
            return Ok(compteurs);
        });

    /// <summary>Consultation d'une ligne budgétaire disponible (aide à la saisie).</summary>
    [HttpGet("lignes-budgetaires-disponibles")]
    [ProducesResponseType(typeof(LigneBudgetaireDisponibleDto), StatusCodes.Status200OK)]
    public Task<IActionResult> LignesBudgetairesDisponibles(
        [FromQuery] long idExercice,
        [FromQuery] long idUB,
        [FromQuery] long idTypeBudget,
        [FromQuery] long? idRubriqueBudgetaire,
        [FromQuery] byte? mois,
        [FromQuery] string? libelleItemAE,
        [FromQuery] long? idGroupeItemAE,
        [FromQuery] long? idItemBI,
        [FromQuery] string? detailBI,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var ligne = await _service.GetLigneBudgetaireDisponibleAsync(
                new LigneBudgetaireDisponibleQuery(
                    idExercice,
                    idUB,
                    idTypeBudget,
                    idRubriqueBudgetaire,
                    mois,
                    libelleItemAE,
                    idGroupeItemAE,
                    idItemBI,
                    detailBI),
                cancellationToken);
            return Ok(ligne);
        });

    /// <summary>Consultation légère : demande, pièces manquantes, historique (sans instruments ni snapshots).</summary>
    [HttpGet("{idDemande:long}")]
    [ProducesResponseType(typeof(DemandePaiementDetailCompletDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var detail = await _service.GetDetailConsultationAsync(idDemande, cancellationToken);
            return detail is null ? NotFound() : Ok(detail);
        });

    /// <summary>Détail complet : demande, snapshots, contrôle, pièces manquantes, historique, instruments.</summary>
    [HttpGet("{idDemande:long}/complet")]
    [ProducesResponseType(typeof(DemandePaiementDetailCompletDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetComplet(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var detail = await _service.GetDetailCompletAsync(idDemande, cancellationToken);
            return detail is null ? NotFound() : Ok(detail);
        });

    /// <summary>Crée une demande en statut BROUILLON (statut initial imposé côté serveur).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Create(
        [FromBody] CreateDemandePaiementRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var created = await _service.CreateBrouillonAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { idDemande = created.IdDemandePaiement }, created);
        });

    /// <summary>Modifie une demande en brouillon ou à corriger.</summary>
    [HttpPut("{idDemande:long}")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Update(
        long idDemande,
        [FromBody] UpdateDemandePaiementRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateBrouillonAsync(idDemande, request, cancellationToken);
            return Ok(updated);
        });

    [HttpPost("{idDemande:long}/soumettre")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Soumettre(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.SoumettreAsync(idDemande, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/annuler-soumission")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> AnnulerSoumission(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.AnnulerSoumissionAsync(idDemande, cancellationToken);
            return Ok(result);
        });

    /// <summary>Envoie une DPM en validation entité (N1).</summary>
    [HttpPost("{idDemande:long}/envoyer-validation")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> EnvoyerValidation(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.EnvoyerEnValidationAsync(idDemande, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/validation/n1")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> ValiderN1(
        long idDemande,
        [FromBody] ValidationEntiteRequest? request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.ValiderN1ElectroniqueAsync(idDemande, request, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/validation/n2")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> ValiderN2(
        long idDemande,
        [FromBody] ValidationEntiteRequest? request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.ValiderN2ElectroniqueAsync(idDemande, request, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/validation/n1/physique")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> DeclarerValidationPhysiqueN1(
        long idDemande,
        [FromBody] DeclarationValidationPhysiqueRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.DeclarerValidationPhysiqueN1Async(idDemande, request, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/validation/n2/physique")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> DeclarerValidationPhysiqueN2(
        long idDemande,
        [FromBody] DeclarationValidationPhysiqueRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.DeclarerValidationPhysiqueN2Async(idDemande, request, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/validation/n2/annuler")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> AnnulerValidationN2(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.AnnulerValidationN2Async(idDemande, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/validation/n1/annuler")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> AnnulerValidationN1(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.AnnulerValidationN1Async(idDemande, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/rejeter-validation-entite")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> RejeterValidationEntite(
        long idDemande,
        [FromBody] RetourDemandePaiementRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.RejeterValidationEntiteAsync(idDemande, request, cancellationToken);
            return Ok(result);
        });

    [HttpGet("{idDemande:long}/document-pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> DocumentPdf(
        long idDemande,
        [FromQuery] bool inline = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var pdf = await _service.GenererDocumentPdfAsync(idDemande, cancellationToken);
            var fileName = $"demande-paiement-{idDemande}.pdf";
            return inline
                ? HttpFileResponses.Inline(pdf, "application/pdf", fileName)
                : HttpFileResponses.Attachment(pdf, "application/pdf", fileName);
        });

    [HttpGet("{idDemande:long}/billet-conversion")]
    [ProducesResponseType(typeof(BilletConversionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetBilletConversion(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var billet = await _service.GetBilletConversionAsync(idDemande, cancellationToken);
            return billet is null ? NotFound() : Ok(billet);
        });

    [HttpPost("{idDemande:long}/billet-conversion")]
    [ProducesResponseType(typeof(BilletConversionDto), StatusCodes.Status200OK)]
    public Task<IActionResult> EtablirBilletConversion(
        long idDemande,
        [FromBody] EtablirBilletConversionRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var billet = await _service.EtablirBilletConversionAsync(idDemande, request, cancellationToken);
            return Ok(billet);
        });

    [HttpGet("{idDemande:long}/billet-conversion/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> BilletConversionPdf(
        long idDemande,
        [FromQuery] bool inline = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var pdf = await _service.GenererBilletConversionPdfAsync(idDemande, cancellationToken);
            var fileName = $"billet-conversion-{idDemande}.pdf";
            return inline
                ? HttpFileResponses.Inline(pdf, "application/pdf", fileName)
                : HttpFileResponses.Attachment(pdf, "application/pdf", fileName);
        });

    [HttpGet("{idDemande:long}/piece-caisse")]
    [ProducesResponseType(typeof(PieceCaisseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetPieceCaisse(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var piece = await _service.GetPieceCaisseAsync(idDemande, cancellationToken);
            return piece is null ? NotFound() : Ok(piece);
        });

    [HttpPost("{idDemande:long}/piece-caisse")]
    [ProducesResponseType(typeof(PieceCaisseDto), StatusCodes.Status200OK)]
    public Task<IActionResult> EtablirPieceCaisse(
        long idDemande,
        [FromBody] EtablirPieceCaisseRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var piece = await _service.EtablirPieceCaisseAsync(idDemande, request, cancellationToken);
            return Ok(piece);
        });

    [HttpGet("{idDemande:long}/piece-caisse/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> PieceCaissePdf(
        long idDemande,
        [FromQuery] bool inline = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var pdf = await _service.GenererPieceCaissePdfAsync(idDemande, cancellationToken);
            var fileName = $"piece-caisse-{idDemande}.pdf";
            return inline
                ? HttpFileResponses.Inline(pdf, "application/pdf", fileName)
                : HttpFileResponses.Attachment(pdf, "application/pdf", fileName);
        });

    [HttpGet("{idDemande:long}/bon-provisoire")]
    [ProducesResponseType(typeof(BonProvisoireDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetBonProvisoire(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var bon = await _service.GetBonProvisoireAsync(idDemande, cancellationToken);
            return bon is null ? NotFound() : Ok(bon);
        });

    [HttpPost("{idDemande:long}/bon-provisoire")]
    [ProducesResponseType(typeof(BonProvisoireDto), StatusCodes.Status200OK)]
    public Task<IActionResult> EtablirBonProvisoire(
        long idDemande,
        [FromBody] EtablirBonProvisoireRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var bon = await _service.EtablirBonProvisoireAsync(idDemande, request, cancellationToken);
            return Ok(bon);
        });

    [HttpGet("{idDemande:long}/bon-provisoire/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> BonProvisoirePdf(
        long idDemande,
        [FromQuery] bool inline = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var pdf = await _service.GenererBonProvisoirePdfAsync(idDemande, cancellationToken);
            var fileName = $"bon-provisoire-{idDemande}.pdf";
            return inline
                ? HttpFileResponses.Inline(pdf, "application/pdf", fileName)
                : HttpFileResponses.Attachment(pdf, "application/pdf", fileName);
        });

    [HttpGet("{idDemande:long}/minute-cheque")]
    [ProducesResponseType(typeof(MinuteChequeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetMinuteCheque(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var minute = await _service.GetMinuteChequeAsync(idDemande, cancellationToken);
            return minute is null ? NotFound() : Ok(minute);
        });

    [HttpPost("{idDemande:long}/minute-cheque")]
    [ProducesResponseType(typeof(MinuteChequeDto), StatusCodes.Status200OK)]
    public Task<IActionResult> EtablirMinuteCheque(
        long idDemande,
        [FromBody] EtablirMinuteChequeRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var minute = await _service.EtablirMinuteChequeAsync(idDemande, request, cancellationToken);
            return Ok(minute);
        });

    [HttpGet("{idDemande:long}/minute-cheque/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> MinuteChequePdf(
        long idDemande,
        [FromQuery] bool inline = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var pdf = await _service.GenererMinuteChequePdfAsync(idDemande, cancellationToken);
            var fileName = $"minute-cheque-{idDemande}.pdf";
            return inline
                ? HttpFileResponses.Inline(pdf, "application/pdf", fileName)
                : HttpFileResponses.Attachment(pdf, "application/pdf", fileName);
        });

    [HttpPost("{idDemande:long}/receptionner")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Receptionner(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.ReceptionnerAsync(idDemande, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/entrer-traitement")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> EntrerTraitement(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.EntrerTraitementAsync(idDemande, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/traitement-charge")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> TraitementCharge(
        long idDemande,
        [FromBody] TraitementChargeDpmRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.TraiterChargeAsync(idDemande, request, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/retenir-sollicitation-charge")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> RetenirSollicitationCharge(
        long idDemande,
        [FromBody] RetenirSollicitationChargeRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.RetenirSollicitationChargeAsync(idDemande, request, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/orienter")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Orienter(
        long idDemande,
        [FromBody] OrienterDemandePaiementRequest? request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.OrienterAsync(idDemande, request, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/prendre-en-charge")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> PrendreEnCharge(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.PrendreEnControleAsync(idDemande, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/retourner")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Retourner(
        long idDemande,
        [FromBody] RetourDemandePaiementRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.RetournerAsync(idDemande, request, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/remettre-en-brouillon")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> RemettreEnBrouillon(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.RemettreEnBrouillonAsync(idDemande, cancellationToken);
            return Ok(result);
        });

    [HttpPost("{idDemande:long}/viser")]
    [ProducesResponseType(typeof(DemandePaiementDetailDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Viser(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var result = await _service.ViserAsync(idDemande, cancellationToken);
            return Ok(result);
        });

    /// <summary>Prévisualisation du contrôle budgétaire — sans engagement ni snapshot.</summary>
    [HttpPost("{idDemande:long}/controle-budgetaire")]
    [ProducesResponseType(typeof(ControleBudgetaireDto), StatusCodes.Status200OK)]
    public Task<IActionResult> ControleBudgetaire(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var controle = await _service.ControlerBudgetaireAsync(idDemande, cancellationToken);
            return Ok(controle);
        });

    [HttpGet("{idDemande:long}/fiche-imputation")]
    [ProducesResponseType(typeof(FicheImputationBudgetaireDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetFicheImputation(
        long idDemande,
        [FromQuery] FicheImputationMode mode = FicheImputationMode.Travail,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var fiche = await _service.GetFicheImputationAsync(idDemande, mode, cancellationToken);
            return Ok(fiche);
        });

    [HttpGet("{idDemande:long}/fiche-imputation/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public Task<IActionResult> FicheImputationPdf(
        long idDemande,
        [FromQuery] FicheImputationMode mode = FicheImputationMode.Travail,
        [FromQuery] bool inline = false,
        CancellationToken cancellationToken = default)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var pdf = await _service.GenererFicheImputationPdfAsync(idDemande, mode, cancellationToken);
            var suffix = mode == FicheImputationMode.Definitive ? "definitive" : "travail";
            var fileName = $"fiche-imputation-{idDemande}-{suffix}.pdf";
            return inline
                ? HttpFileResponses.Inline(pdf, "application/pdf", fileName)
                : HttpFileResponses.Attachment(pdf, "application/pdf", fileName);
        });

    [HttpGet("{idDemande:long}/retours-destinataires")]
    [ProducesResponseType(typeof(IReadOnlyList<DemandePaiementRetourDestinataireDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> RetoursDestinataires(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var retours = await _service.GetRetoursDestinatairesAsync(idDemande, cancellationToken);
            return retours is null ? NotFound() : Ok(retours);
        });

    [HttpGet("{idDemande:long}/routage")]
    [ProducesResponseType(typeof(IReadOnlyList<DemandePaiementRoutageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Routage(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var routages = await _service.GetRoutageAsync(idDemande, cancellationToken);
            return routages is null ? NotFound() : Ok(routages);
        });

    [HttpGet("{idDemande:long}/historique")]
    [ProducesResponseType(typeof(HistoriqueDemandePaiementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Historique(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var historique = await _service.GetHistoriqueAsync(idDemande, cancellationToken);
            return historique is null ? NotFound() : Ok(historique);
        });

    [HttpGet("{idDemande:long}/pieces")]
    [ProducesResponseType(typeof(IReadOnlyList<DemandePaiementPieceDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetPieces(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var pieces = await _service.GetPiecesAsync(idDemande, cancellationToken);
            return Ok(pieces);
        });

    /// <summary>Upload réel d'une pièce justificative (multipart/form-data).</summary>
    [HttpPost("{idDemande:long}/pieces")]
    [RequestSizeLimit(15_728_640)]
    [RequestFormLimits(MultipartBodyLengthLimit = 15_728_640)]
    [ProducesResponseType(typeof(DemandePaiementPieceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> AddPiece(
        long idDemande,
        [FromForm] long? idPieceObligatoire,
        [FromForm] string codeTypePiece,
        [FromForm] string libelle,
        IFormFile? fichier,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            if (fichier is null || fichier.Length <= 0)
                throw new ArgumentException("Le fichier de la pièce justificative est obligatoire.");

            await using var stream = fichier.OpenReadStream();
            var piece = await _service.AddPieceAsync(
                idDemande,
                new UploadDemandePaiementPieceMetadata(
                    idPieceObligatoire,
                    codeTypePiece ?? string.Empty,
                    libelle ?? string.Empty),
                stream,
                fichier.FileName,
                fichier.ContentType,
                cancellationToken);
            return Ok(piece);
        });

    /// <summary>Télécharge le contenu binaire d'une pièce (Content-Disposition: attachment).</summary>
    [HttpGet("{idDemande:long}/pieces/{idPiece:long}/contenu")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DownloadPiece(
        long idDemande,
        long idPiece,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var file = await _service.GetPieceContentAsync(idDemande, idPiece, cancellationToken);
            if (file is null)
                return NotFound();

            return HttpFileResponses.Attachment(file.Content, file.ContentType, file.FileName);
        });

    /// <summary>Consultation inline d'une pièce (Content-Disposition: inline, mêmes droits que contenu).</summary>
    [HttpGet("{idDemande:long}/pieces/{idPiece:long}/apercu")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> PreviewPiece(
        long idDemande,
        long idPiece,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            using var perf = DocumentPerfScope.Begin(_logger, "PIECE-PREVIEW", idDemande);
            var file = await _service.GetPieceContentAsync(idDemande, idPiece, cancellationToken);
            if (file is null)
                return NotFound();

            perf.LogPiecePreview(file.Content.CanSeek ? file.Content.Length : null);
            return HttpFileResponses.Inline(file.Content, file.ContentType, file.FileName);
        });

    [HttpDelete("{idDemande:long}/pieces/{idPiece:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeletePiece(
        long idDemande,
        long idPiece,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            await _service.DeletePieceAsync(idDemande, idPiece, cancellationToken);
            return NoContent();
        });

    [HttpGet("{idDemande:long}/imputation-dc")]
    [ProducesResponseType(typeof(GrilleImputationDcDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetImputationDc(
        long idDemande,
        [FromQuery] byte? mois,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var grille = await _service.GetGrilleImputationDcAsync(idDemande, mois, cancellationToken);
            return Ok(grille);
        });

    [HttpPut("{idDemande:long}/imputations-dc")]
    [ProducesResponseType(typeof(GrilleImputationDcDto), StatusCodes.Status200OK)]
    public Task<IActionResult> EnregistrerImputationsDc(
        long idDemande,
        [FromBody] EnregistrerImputationsDcRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var grille = await _service.EnregistrerImputationsDcAsync(idDemande, request, cancellationToken);
            return Ok(grille);
        });

    [HttpGet("{idDemande:long}/imputation-ae")]
    [ProducesResponseType(typeof(GrilleImputationAeDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetImputationAe(
        long idDemande,
        [FromQuery] string libelleItemAE,
        [FromQuery] long? idGroupeItemAE,
        [FromQuery] byte? mois,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var grille = await _service.GetGrilleImputationAeAsync(
                idDemande,
                libelleItemAE,
                idGroupeItemAE,
                mois,
                cancellationToken);
            return Ok(grille);
        });

    [HttpPut("{idDemande:long}/imputations-ae")]
    [ProducesResponseType(typeof(GrilleImputationAeDto), StatusCodes.Status200OK)]
    public Task<IActionResult> EnregistrerImputationsAe(
        long idDemande,
        [FromBody] EnregistrerImputationsAeRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var grille = await _service.EnregistrerImputationsAeAsync(idDemande, request, cancellationToken);
            return Ok(grille);
        });

    [HttpGet("{idDemande:long}/imputation-bi")]
    [ProducesResponseType(typeof(GrilleImputationBiDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetImputationBi(
        long idDemande,
        [FromQuery] long idItemBI,
        [FromQuery] byte? mois,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var grille = await _service.GetGrilleImputationBiAsync(
                idDemande,
                idItemBI,
                mois,
                cancellationToken);
            return Ok(grille);
        });

    [HttpPut("{idDemande:long}/imputations-bi")]
    [ProducesResponseType(typeof(GrilleImputationBiDto), StatusCodes.Status200OK)]
    public Task<IActionResult> EnregistrerImputationsBi(
        long idDemande,
        [FromBody] EnregistrerImputationsBiRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var grille = await _service.EnregistrerImputationsBiAsync(idDemande, request, cancellationToken);
            return Ok(grille);
        });

    [HttpPost("{idDemande:long}/imputations")]
    [ProducesResponseType(typeof(DemandePaiementImputationDto), StatusCodes.Status200OK)]
    public Task<IActionResult> AddImputation(
        long idDemande,
        [FromBody] CreateImputationRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var imputation = await _service.AddImputationAsync(idDemande, request, cancellationToken);
            return Ok(imputation);
        });

    [HttpPut("{idDemande:long}/imputations/{idImputation:long}")]
    [ProducesResponseType(typeof(DemandePaiementImputationDto), StatusCodes.Status200OK)]
    public Task<IActionResult> UpdateImputation(
        long idDemande,
        long idImputation,
        [FromBody] UpdateImputationRequest request,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var imputation = await _service.UpdateImputationAsync(
                idDemande,
                idImputation,
                request,
                cancellationToken);
            return Ok(imputation);
        });

    [HttpDelete("{idDemande:long}/imputations/{idImputation:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteImputation(
        long idDemande,
        long idImputation,
        CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            await _service.DeleteImputationAsync(idDemande, idImputation, cancellationToken);
            return NoContent();
        });

    /// <summary>Suppression définitive d'un brouillon (statut BROUILLON uniquement).</summary>
    [HttpDelete("{idDemande:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeleteBrouillon(long idDemande, CancellationToken cancellationToken)
        => DemandePaiementApiResults.ExecuteAsync(async () =>
        {
            var deleted = await _service.DeleteBrouillonAsync(idDemande, cancellationToken);
            return deleted ? NoContent() : NotFound();
        });
}
