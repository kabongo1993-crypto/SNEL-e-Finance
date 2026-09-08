using System.Diagnostics;
using System.Security.Cryptography;
using BudgetWeb.Application.Diagnostics;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.DpmConsultation;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Interfaces.Referentiels;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;
using Microsoft.Extensions.Logging;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService : IDemandePaiementService
{
    private readonly IDemandePaiementRepository _repository;
    private readonly ICasDossierRepository _casDossiers;
    private readonly IDemandeurRepository _demandeurs;
    private readonly IDeviseRepository _devises;
    private readonly ITauxChangeService _tauxChange;
    private readonly ICurrentUserService _currentUser;
    private readonly IPerimetreUtilisateurReader _perimetre;
    private readonly IItemBIRepository _itemBIRepository;
    private readonly IFileStorage _fileStorage;
    private readonly PieceJointeUploadValidator _pieceValidator;
    private readonly IDemandePaiementDocumentRenderer _dpmDocumentRenderer;
    private readonly IBilletConversionDocumentRenderer _billetDocumentRenderer;
    private readonly IPieceCaisseDocumentRenderer _pieceCaisseDocumentRenderer;
    private readonly IBonProvisoireDocumentRenderer _bonProvisoireDocumentRenderer;
    private readonly IMinuteChequeDocumentRenderer _minuteChequeDocumentRenderer;
    private readonly IFicheImputationBudgetaireRenderer _ficheImputationRenderer;
    private readonly ILogger<DemandePaiementService> _logger;

    public DemandePaiementService(
        IDemandePaiementRepository repository,
        ICasDossierRepository casDossiers,
        IDemandeurRepository demandeurs,
        IDeviseRepository devises,
        ITauxChangeService tauxChange,
        ICurrentUserService currentUser,
        IPerimetreUtilisateurReader perimetre,
        IItemBIRepository itemBIRepository,
        IFileStorage fileStorage,
        PieceJointeUploadValidator pieceValidator,
        IDemandePaiementDocumentRenderer dpmDocumentRenderer,
        IBilletConversionDocumentRenderer billetDocumentRenderer,
        IPieceCaisseDocumentRenderer pieceCaisseDocumentRenderer,
        IBonProvisoireDocumentRenderer bonProvisoireDocumentRenderer,
        IMinuteChequeDocumentRenderer minuteChequeDocumentRenderer,
        IFicheImputationBudgetaireRenderer ficheImputationRenderer,
        ILogger<DemandePaiementService> logger)
    {
        _repository = repository;
        _casDossiers = casDossiers;
        _demandeurs = demandeurs;
        _devises = devises;
        _tauxChange = tauxChange;
        _currentUser = currentUser;
        _perimetre = perimetre;
        _itemBIRepository = itemBIRepository;
        _fileStorage = fileStorage;
        _pieceValidator = pieceValidator;
        _dpmDocumentRenderer = dpmDocumentRenderer;
        _billetDocumentRenderer = billetDocumentRenderer;
        _pieceCaisseDocumentRenderer = pieceCaisseDocumentRenderer;
        _bonProvisoireDocumentRenderer = bonProvisoireDocumentRenderer;
        _minuteChequeDocumentRenderer = minuteChequeDocumentRenderer;
        _ficheImputationRenderer = ficheImputationRenderer;
        _logger = logger;
    }

    /// <summary>Override test : date de traitement DPM (null = jour courant).</summary>
    public static Func<DateOnly>? ResolveDateTraitementDpmForTests;

    /// <summary>Date métier pour l'application des taux par le Chargé DPM (jour du traitement).</summary>
    private static DateOnly DateTraitementDpm()
        => ResolveDateTraitementDpmForTests?.Invoke() ?? DateOnly.FromDateTime(DateTime.Now);

    public async Task<IReadOnlyList<DemandePaiementListDto>> ListAsync(
        DemandePaiementQuery query,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();

        if (query.IdUB is long idUb && !PeutVoirToutesUb())
            await GarantirAccesUbAsync(idUb, cancellationToken);

        var rows = await _repository.ListAsync(query, cancellationToken);
        rows = await ApplyListScopeAsync(rows, query, query.Scope, cancellationToken);
        return rows.Select(MapList).ToList();
    }

    public async Task<DemandePaiementDetailDto?> GetByIdAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetDetailAsync(idDemande, cancellationToken);
        if (row is null)
            return null;

        await GarantirAccesDemandeAsync(row, cancellationToken);
        return MapDetail(row);
    }

    public async Task<DemandePaiementDetailCompletDto?> GetDetailCompletAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var totalSw = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        var row = await _repository.GetDetailAsync(idDemande, cancellationToken);
        _logger.LogInformation(
            "[PERF][ETABLISSEMENT] DemandeId={DemandeId} Etape=GetDetailComplet.GetDetail Duration={DurationMs}ms",
            idDemande,
            sw.ElapsedMilliseconds);
        if (row is null)
            return null;

        sw.Restart();
        await GarantirAccesDemandeAsync(row, cancellationToken);
        _logger.LogInformation(
            "[PERF][ETABLISSEMENT] DemandeId={DemandeId} Etape=GetDetailComplet.Access Duration={DurationMs}ms",
            idDemande,
            sw.ElapsedMilliseconds);

        var demande = MapDetail(row);
        var snapshots = row.Imputations
            .SelectMany(i => i.Snapshots)
            .OrderBy(s => s.DateSnapshot)
            .ThenBy(s => s.IdSnapshot)
            .Select(MapSnapshot)
            .ToList();

        ControleBudgetaireDto? controle = null;
        if (string.Equals(
                StatutDemandePaiement.Normaliser(row.Statut),
                StatutDemandePaiement.EnControleBudgetaire,
                StringComparison.Ordinal))
        {
            controle = await DemandePaiementControleBudgetaire.ControlerDemandeAsync(
                row,
                _repository,
                cancellationToken);
        }

        var piecesManquantes = await ListerPiecesManquantesInterneAsync(row, cancellationToken);
        var historique = (await _repository.GetHistoriqueAsync(idDemande, cancellationToken))
            .Select(MapAudit)
            .ToList();

        BilletConversionDto? billet = null;
        if (BilletConversionRules.NecessiteBillet(row.ModePaiementSollicite, row.Devise))
        {
            var entity = row.BilletConversion
                ?? await _repository.GetBilletConversionByDemandeAsync(idDemande, cancellationToken);
            if (entity is not null)
                billet = MapBilletConversion(row, entity);
        }

        PieceCaisseDto? pieceCaisse = null;
        BonProvisoireDto? bonProvisoire = null;
        MinuteChequeDto? minuteCheque = null;

        if (EstInstrumentCaisseApplicable(row))
        {
            if (row.PieceCaisse is { } pc)
                pieceCaisse = MapPieceCaisse(row, pc);
            else
            {
                var loaded = await _repository.GetPieceCaisseByDemandeAsync(idDemande, cancellationToken);
                if (loaded is not null)
                    pieceCaisse = MapPieceCaisse(row, loaded);
            }

            if (row.BonProvisoire is { } bp)
                bonProvisoire = MapBonProvisoire(row, bp);
            else
            {
                var loaded = await _repository.GetBonProvisoireByDemandeAsync(idDemande, cancellationToken);
                if (loaded is not null)
                    bonProvisoire = MapBonProvisoire(row, loaded);
            }
        }

        if (EstInstrumentBanqueApplicable(row))
        {
            if (row.MinuteCheque is { } mc)
                minuteCheque = MapMinuteCheque(row, mc);
            else
            {
                var loaded = await _repository.GetMinuteChequeByDemandeAsync(idDemande, cancellationToken);
                if (loaded is not null)
                    minuteCheque = MapMinuteCheque(row, loaded);
            }
        }

        _logger.LogInformation(
            "[PERF][ETABLISSEMENT] DemandeId={DemandeId} Etape=GetDetailComplet.TOTAL Duration={DurationMs}ms",
            idDemande,
            totalSw.ElapsedMilliseconds);

        return new DemandePaiementDetailCompletDto(
            demande,
            snapshots,
            controle,
            piecesManquantes,
            historique,
            billet,
            pieceCaisse,
            bonProvisoire,
            minuteCheque);
    }

    public async Task<DemandePaiementDetailCompletDto?> GetDetailConsultationAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetDetailConsultationAsync(idDemande, cancellationToken);
        if (row is null)
            return null;

        await GarantirAccesDemandeAsync(row, cancellationToken);

        var demande = MapDetail(row);
        var piecesManquantes = await ListerPiecesManquantesInterneAsync(row, cancellationToken);
        var historique = (await _repository.GetHistoriqueConsultationAsync(idDemande, cancellationToken))
            .Select(MapAudit)
            .ToList();

        return new DemandePaiementDetailCompletDto(
            demande,
            Array.Empty<SnapshotDto>(),
            null,
            piecesManquantes,
            historique,
            null,
            null,
            null,
            null);
    }

    public async Task<IReadOnlyList<DemandePaiementPieceDto>> GetPiecesAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(row, cancellationToken);
        return row.PiecesJointes.OrderBy(p => p.DateUpload).Select(MapPiece).ToList();
    }

    public async Task<LigneBudgetaireDisponibleDto> GetLigneBudgetaireDisponibleAsync(
        LigneBudgetaireDisponibleQuery query,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        await GarantirAccesUbAsync(query.IdUB, cancellationToken);

        var typeBudget = await _repository.GetTypeBudgetAsync(query.IdTypeBudget, cancellationToken)
            ?? throw new InvalidOperationException("Type budget introuvable.");

        var idVersion = await _repository.ResolveVersionBudgetaireValideeAsync(
            query.IdExercice,
            query.IdUB,
            cancellationToken);

        var imputation = new DemandePaiementImputation
        {
            FK_TypeBudget = query.IdTypeBudget,
            FK_UniteBudgetaire = query.IdUB,
            FK_ExerciceBudgetaire = query.IdExercice,
            FK_RubriqueBudgetaire = query.IdRubriqueBudgetaire,
            Mois = query.Mois,
            LibelleItemAE = query.LibelleItemAE?.Trim(),
            FK_GroupeItemAE = query.IdGroupeItemAE,
            FK_ItemBI = query.IdItemBI,
            DetailBI = query.DetailBI?.Trim(),
            MontantBrut = 0m,
            Devise = "USD",
            TauxConversion = 1m,
            MontantUsd = 0m,
        };

        DemandePaiementImputationRules.ValiderStructure(imputation);
        DemandePaiementImputationRules.ValiderCoherenceTypeBudget(typeBudget.CodeType, imputation);

        PrevisionBudgetaire? prevision = null;
        long? idBudgetLigne = null;
        if (idVersion is long versionId)
        {
            var idPrevision = await _repository.FindPrevisionIdAsync(imputation, versionId, cancellationToken);
            if (idPrevision is long idPrev)
            {
                idBudgetLigne = idPrev;
                prevision = await _repository.GetPrevisionAsync(idPrev, cancellationToken);
            }
        }

        var controle = await DemandePaiementControleBudgetaire.ControleImputationAsync(
            imputation,
            typeBudget.CodeType,
            prevision,
            excludeDemandeId: null,
            _repository,
            cancellationToken: cancellationToken);

        return new LigneBudgetaireDisponibleDto(
            idBudgetLigne,
            typeBudget.CodeType,
            controle.BudgetAnnuel,
            controle.BudgetMensuel,
            controle.MontantPrevision,
            controle.CreditEngageAnnuel,
            controle.CreditEngageMensuel,
            controle.CreditDisponibleAnnuel,
            controle.CreditDisponibleMensuel,
            prevision is not null);
    }

    public async Task<DemandePaiementDetailDto> CreateBrouillonAsync(
        CreateDemandePaiementRequest request,
        CancellationToken cancellationToken = default)
    {
        using var perf = MutationPerfScope.Begin(_logger, MutationPerfOperations.Create);
        await perf.TrackAsync("Permission", () => { ExigerEcrire(); return Task.CompletedTask; });
        var userId = _currentUser.RequireUserId();
        ValiderEnteteCreation(request);

        var demandeur = await perf.TrackAsync("Demandeur.GetById",
            () => _demandeurs.GetByIdAsync(request.IdDemandeur, cancellationToken))
            ?? throw new InvalidOperationException("Demandeur introuvable.");

        DemandeurRules.ExigerActif(demandeur.Actif);
        DemandeurRules.ExigerCoherenceUbDemandeur(demandeur.FK_UniteBudgetaire, request.IdUB);

        var idUB = demandeur.FK_UniteBudgetaire;
        await perf.TrackAsync("AccesUb", () => GarantirAccesUbAsync(idUB, cancellationToken));

        var (idDevise, codeDevise) = await perf.TrackAsync("ResoudreDevise",
            () => ResoudreDeviseAsync(request.IdDevise, request.Devise, exigerActif: true, cancellationToken));

        var reference = await _repository.GenererReferenceAsync(
            (short)request.DateEmission.Year,
            cancellationToken);

        var entity = DemandePaiementFactory.CreerBrouillon(
            reference,
            request.DateEmission,
            request.IdExercice,
            idUB,
            demandeur.IdDemandeur,
            request.IdCasDossier,
            request.Objet,
            request.MontantBrut,
            codeDevise,
            request.TypeBudgetSollicite,
            request.ItemSollicite,
            request.ModePaiementSollicite,
            userId,
            DateTime.Now,
            request.LieuEmission,
            compteSection: request.CompteSection,
            fkUbProposeeParClient: request.IdUB,
            fkDevise: idDevise);

        var created = await _repository.AddAsync(entity, cancellationToken);

        if (request.Beneficiaires is { Count: > 0 })
        {
            var beneficiaires = request.Beneficiaires
                .Select(b => CreerBeneficiaire(b, created.IdDemandePaiement))
                .ToList();
            await _repository.ReplaceBeneficiairesAsync(created.IdDemandePaiement, beneficiaires, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        await _repository.AddAuditAsync(userId, "CREER", created.IdDemandePaiement, null, new
        {
            created.Reference,
            created.Objet,
            created.MontantBrut,
            created.Devise,
            created.FK_Demandeur,
            created.FK_UniteBudgetaire,
            created.Statut,
        }, cancellationToken);

        var dto = await perf.TrackAsync("MapperDetailApresCreation",
            () => MapperDetailApresCreationAsync(created.IdDemandePaiement, cancellationToken));
        perf.LogTotal();
        return dto;
    }

    public async Task<DemandePaiementDetailDto> UpdateBrouillonAsync(
        long idDemande,
        UpdateDemandePaiementRequest request,
        CancellationToken cancellationToken = default)
    {
        using var perf = MutationPerfScope.Begin(_logger, MutationPerfOperations.Update, idDemande);
        await perf.TrackAsync("Permission", () => { ExigerEcrire(); return Task.CompletedTask; });
        var userId = _currentUser.RequireUserId();
        ValiderEnteteModification(request);

        var entity = await _repository.GetByIdAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await perf.TrackAsync("AccesDemande",
            () => GarantirAccesDemandeAsync(entity, DemandePaiementAccesAction.Modifier, cancellationToken));

        var ancien = new
        {
            entity.Objet,
            entity.MontantBrut,
            entity.Devise,
        };

        var (idDevise, codeDevise) = await perf.TrackAsync("ResoudreDevise",
            () => ResoudreDeviseAsync(
                request.IdDevise,
                request.Devise,
                exigerActif: true,
                cancellationToken,
                idDeviseCourant: entity.FK_Devise));

        if (request.MontantBrut < 0)
            throw new ArgumentException("Le montant sollicité ne peut pas être négatif.");

        ValiderSollicitationEntete(
            request.TypeBudgetSollicite,
            request.ItemSollicite,
            request.ModePaiementSollicite);
        var typeBudgetSollicite = DestinationBudgetaireSolliciteeRules.NormaliserType(request.TypeBudgetSollicite);
        var itemSollicite = DestinationBudgetaireSolliciteeRules.NormaliserItem(request.ItemSollicite);
        var modePaiement = ModePaiementDpm.Normaliser(request.ModePaiementSollicite);

        var now = DateTime.Now;
        var patch = new DemandePaiementBrouillonHeaderPatch(
            request.DateEmission,
            request.LieuEmission?.Trim(),
            request.Objet.Trim(),
            request.CompteSection?.Trim(),
            request.MontantBrut,
            idDevise,
            codeDevise,
            typeBudgetSollicite,
            itemSollicite,
            modePaiement,
            userId,
            now);

        var rows = await perf.TrackAsync("UpdateBrouillonHeader",
            () => _repository.UpdateBrouillonHeaderIfModifiableAsync(idDemande, patch, cancellationToken));
        ExigerMiseAJourReussie(rows);

        if (request.Beneficiaires is not null)
        {
            var beneficiaires = request.Beneficiaires
                .Select(b => CreerBeneficiaire(b, idDemande))
                .ToList();
            await _repository.ReplaceBeneficiairesAsync(idDemande, beneficiaires, cancellationToken);
        }

        var tracked = await _repository.GetTrackedAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");
        InvaliderValidationsSiDonneesModifiees(tracked);
        await _repository.SaveChangesAsync(cancellationToken);

        await _repository.AddAuditAsync(userId, "MODIFIER", idDemande, ancien, new
        {
            patch.Objet,
            patch.MontantBrut,
            patch.Devise,
            tracked.MontantUsd,
            patch.ModePaiementSollicite,
        }, cancellationToken);

        AppliquerPatchBrouillonSurDemande(entity, patch);
        SynchroniserCollectionsDepuisTracked(entity, tracked);
        var dto = perf.Track("Mapping", () => MapperDetailApresMutation(entity));
        perf.LogTotal();
        return dto;
    }

    public async Task<DemandePaiementImputationDto> AddImputationAsync(
        long idDemande,
        CreateImputationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();

        var demande = await _repository.GetByIdAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        ExigerStatut(
            demande,
            StatutDemandePaiement.EnControleBudgetaire,
            "L'imputation est réservée au contrôle budgétaire (Gestionnaire Junior).");
        await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Imputer, cancellationToken);

        var typeBudget = await _repository.GetTypeBudgetAsync(request.IdTypeBudget, cancellationToken)
            ?? throw new InvalidOperationException("Type budget introuvable.");

        ExigerImputer(typeBudget.CodeType);
        ExigerTypeBudgetDemande(demande, typeBudget.IdTypeBudget, typeBudget.CodeType);

        var (taux, _, montantUsd) = await AppliquerTauxAsync(
            request.MontantBrut,
            request.Devise,
            demande.DateEmission,
            cancellationToken);

        var imputation = new DemandePaiementImputation
        {
            FK_DemandePaiement = idDemande,
            Ordre = request.Ordre,
            FK_TypeBudget = request.IdTypeBudget,
            FK_UniteBudgetaire = request.IdUB,
            FK_ExerciceBudgetaire = request.IdExercice,
            FK_RubriqueBudgetaire = request.IdRubriqueBudgetaire,
            Mois = request.Mois,
            LibelleItemAE = request.LibelleItemAE?.Trim(),
            FK_GroupeItemAE = request.IdGroupeItemAE,
            FK_ItemBI = request.IdItemBI,
            DetailBI = request.DetailBI?.Trim(),
            FK_BudgetLigne = request.IdBudgetLigne,
            MontantBrut = request.MontantBrut,
            Devise = request.Devise.Trim().ToUpperInvariant(),
            TauxConversion = taux,
            MontantUsd = montantUsd,
            NumeroFicheSuivi = request.NumeroFicheSuivi,
            FK_UtilisateurCreation = userId,
            DateImputation = DateTime.Now,
        };

        DemandePaiementImputationRules.ValiderStructure(imputation);
        DemandePaiementImputationRules.ValiderCoherenceTypeBudget(typeBudget.CodeType, imputation);

        await AppliquerMiseAJourSiStatutAsync(
            idDemande,
            StatutDemandePaiement.EnControleBudgetaire,
            new DemandePaiementConditionalUpdatePatch
            {
                FK_UtilisateurModification = userId,
                DateModification = DateTime.Now,
            },
            cancellationToken);

        await _repository.AddImputationAsync(imputation, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        await _repository.AddAuditAsync(userId, "IMPUTER", idDemande,
            null,
            new { imputation.IdImputation, codeType = typeBudget.CodeType, imputation.MontantUsd },
            cancellationToken);

        return MapImputation(imputation, typeBudget.CodeType);
    }

    public async Task<DemandePaiementImputationDto> UpdateImputationAsync(
        long idDemande,
        long idImputation,
        UpdateImputationRequest request,
        CancellationToken cancellationToken = default)
    {
        var demande = await _repository.GetByIdAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        ExigerStatut(
            demande,
            StatutDemandePaiement.EnControleBudgetaire,
            "L'imputation est réservée au contrôle budgétaire (Gestionnaire Junior).");
        await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Imputer, cancellationToken);

        var imputation = await _repository.GetImputationTrackedAsync(idImputation, cancellationToken)
            ?? throw new InvalidOperationException("Imputation introuvable.");

        if (imputation.FK_DemandePaiement != idDemande)
            throw new InvalidOperationException("L'imputation n'appartient pas à cette demande.");

        var typeBudget = await _repository.GetTypeBudgetAsync(imputation.FK_TypeBudget, cancellationToken)
            ?? throw new InvalidOperationException("Type budget introuvable.");

        ExigerImputer(typeBudget.CodeType);

        var (taux, _, montantUsd) = await AppliquerTauxAsync(
            request.MontantBrut,
            request.Devise,
            demande.DateEmission,
            cancellationToken);

        imputation.Ordre = request.Ordre;
        imputation.FK_RubriqueBudgetaire = request.IdRubriqueBudgetaire;
        imputation.Mois = request.Mois;
        imputation.LibelleItemAE = request.LibelleItemAE?.Trim();
        imputation.FK_GroupeItemAE = request.IdGroupeItemAE;
        imputation.FK_ItemBI = request.IdItemBI;
        imputation.DetailBI = request.DetailBI?.Trim();
        imputation.FK_BudgetLigne = request.IdBudgetLigne;
        imputation.MontantBrut = request.MontantBrut;
        imputation.Devise = request.Devise.Trim().ToUpperInvariant();
        imputation.TauxConversion = taux;
        imputation.MontantUsd = montantUsd;
        imputation.NumeroFicheSuivi = request.NumeroFicheSuivi;

        DemandePaiementImputationRules.ValiderStructure(imputation);
        DemandePaiementImputationRules.ValiderCoherenceTypeBudget(typeBudget.CodeType, imputation);

        var userId = _currentUser.RequireUserId();
        await AppliquerMiseAJourSiStatutAsync(
            idDemande,
            StatutDemandePaiement.EnControleBudgetaire,
            new DemandePaiementConditionalUpdatePatch
            {
                FK_UtilisateurModification = userId,
                DateModification = DateTime.Now,
            },
            cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);
        return MapImputation(imputation, typeBudget.CodeType);
    }

    public async Task DeleteImputationAsync(
        long idDemande,
        long idImputation,
        CancellationToken cancellationToken = default)
    {
        var demande = await _repository.GetByIdAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        ExigerStatut(
            demande,
            StatutDemandePaiement.EnControleBudgetaire,
            "L'imputation est réservée au contrôle budgétaire (Gestionnaire Junior).");
        await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Imputer, cancellationToken);

        var imputation = await _repository.GetImputationTrackedAsync(idImputation, cancellationToken)
            ?? throw new InvalidOperationException("Imputation introuvable.");

        if (imputation.FK_DemandePaiement != idDemande)
            throw new InvalidOperationException("L'imputation n'appartient pas à cette demande.");

        var typeBudget = await _repository.GetTypeBudgetAsync(imputation.FK_TypeBudget, cancellationToken)
            ?? throw new InvalidOperationException("Type budget introuvable.");
        ExigerImputer(typeBudget.CodeType);

        var userId = _currentUser.RequireUserId();
        await AppliquerMiseAJourSiStatutAsync(
            idDemande,
            StatutDemandePaiement.EnControleBudgetaire,
            new DemandePaiementConditionalUpdatePatch
            {
                FK_UtilisateurModification = userId,
                DateModification = DateTime.Now,
            },
            cancellationToken);

        await _repository.RemoveImputationAsync(imputation, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<DemandePaiementPieceDto> AddPieceAsync(
        long idDemande,
        UploadDemandePaiementPieceMetadata metadata,
        Stream content,
        string originalFileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        using var perf = DocumentPerfScope.Begin(_logger, "PIECE-UPLOAD", idDemande);
        _ = contentType;
        var userId = _currentUser.RequireUserId();

        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(metadata.CodeTypePiece))
            throw new ArgumentException("Le type de pièce est obligatoire.");
        if (string.IsNullOrWhiteSpace(metadata.Libelle))
            throw new ArgumentException("Le libellé de la pièce est obligatoire.");

        var codeType = metadata.CodeTypePiece.Trim();

        var sw = Stopwatch.StartNew();
        var demande = await _repository.GetForPieceUploadAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");
        perf.GetForPieceUploadMs = sw.ElapsedMilliseconds;

        await GarantirAccesDemandeAsync(
            demande,
            ResoudreAccesPieceMutation(demande, codeType),
            cancellationToken);
        ExigerPieceModifiable(demande, codeType);

        var safeName = _pieceValidator.SanitizeOriginalFileName(originalFileName);
        var extension = _pieceValidator.GetValidatedExtension(safeName);

        await using var buffer = new MemoryStream();
        await using var instrumented = new InstrumentedReadStream(content);
        sw.Restart();
        await instrumented.CopyToAsync(buffer, cancellationToken);
        perf.ReadStreamMs = instrumented.ReadElapsedMs;
        perf.MemoryStreamMs = sw.ElapsedMilliseconds;
        _pieceValidator.EnsureSizeWithinLimit(buffer.Length);

        buffer.Position = 0;
        sw.Restart();
        var hashBytes = await SHA256.HashDataAsync(buffer, cancellationToken);
        perf.Sha256Ms = sw.ElapsedMilliseconds;
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        buffer.Position = 0;

        var relativePath = _pieceValidator.BuildRelativeStorageKey(idDemande, extension);
        sw.Restart();
        await _fileStorage.SaveAsync(relativePath, buffer, cancellationToken);
        perf.StorageMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var estObligatoire = await ResoudreEstObligatoirePieceAsync(demande, metadata, cancellationToken);
        perf.ObligatoireMs = sw.ElapsedMilliseconds;

        try
        {
            var piece = new PieceJointe
            {
                FK_DemandePaiement = idDemande,
                FK_PieceObligatoire = metadata.IdPieceObligatoire,
                CodeTypePiece = metadata.CodeTypePiece.Trim(),
                Libelle = metadata.Libelle.Trim(),
                EstObligatoire = estObligatoire,
                NomFichierOriginal = safeName,
                CheminRelatif = relativePath,
                HashSha256 = hashHex,
                TailleOctets = buffer.Length,
                FK_Utilisateur = userId,
                DateUpload = DateTime.Now,
            };

            await _repository.AddPieceAsync(piece, cancellationToken);

            if (DemandePaiementEmpreinte.EstPieceJustificativeMetier(piece)
                && await _repository.HasValidatedEntiteValidationsAsync(idDemande, cancellationToken))
            {
                sw.Restart();
                await _repository.AttachEmpreinteCollectionsForUploadAsync(
                    demande,
                    idDemande,
                    cancellationToken);
                perf.EmpreinteCollectionsMs = sw.ElapsedMilliseconds;
                demande.PiecesJointes.Add(piece);
                ValidationEntiteRules.InvaliderValidationsSiEmpreinteObsolete(demande);
            }

            sw.Restart();
            await _repository.SaveChangesAsync(cancellationToken);
            perf.SaveChangesMs = sw.ElapsedMilliseconds;

            if (string.Equals(codeType, TypePieceJointeDpm.DocumentDpmSigne, StringComparison.OrdinalIgnoreCase))
            {
                sw.Restart();
                await _repository.AddAuditAsync(userId, "AJOUTER_DOCUMENT_SIGNE", idDemande,
                    null,
                    new { piece.IdPieceJointe, piece.NomFichierOriginal },
                    cancellationToken);
                perf.AuditMs = sw.ElapsedMilliseconds;
            }

            perf.LogPieceUpload(safeName, buffer.Length, extension);
            return MapPiece(piece);
        }
        catch
        {
            try
            {
                await _fileStorage.DeleteAsync(relativePath, CancellationToken.None);
            }
            catch
            {
                // best-effort cleanup
            }

            throw;
        }
    }

    public async Task DeletePieceAsync(
        long idDemande,
        long idPiece,
        CancellationToken cancellationToken = default)
    {
        var demande = await _repository.GetByIdAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        var piece = await _repository.GetPieceTrackedAsync(idPiece, cancellationToken)
            ?? throw new InvalidOperationException("Pièce jointe introuvable.");

        if (piece.FK_DemandePaiement != idDemande)
            throw new InvalidOperationException("La pièce jointe n'appartient pas à cette demande.");

        await GarantirAccesDemandeAsync(
            demande,
            ResoudreAccesPieceMutation(demande, piece.CodeTypePiece),
            cancellationToken);
        ExigerPieceModifiable(demande, piece.CodeTypePiece);

        var relativePath = piece.CheminRelatif;
        var affecteEmpreinte = DemandePaiementEmpreinte.EstPieceJustificativeMetier(piece);
        await _repository.RemovePieceAsync(piece, cancellationToken);

        if (affecteEmpreinte)
        {
            var tracked = await _repository.GetTrackedAsync(idDemande, cancellationToken)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");
            ValidationEntiteRules.InvaliderValidationsSiEmpreinteObsolete(tracked);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            try
            {
                await _fileStorage.DeleteAsync(relativePath, cancellationToken);
            }
            catch
            {
                // Métadonnées déjà supprimées ; ne pas bloquer si le fichier manque.
            }
        }
    }

    public async Task<bool> DeleteBrouillonAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerEcrire();

        var demande = await _repository.GetByIdAsync(idDemande, cancellationToken);
        if (demande is null)
            return false;

        ExigerStatut(
            demande,
            StatutDemandePaiement.Brouillon,
            "Seuls les brouillons peuvent être supprimés définitivement.");
        await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Modifier, cancellationToken);

        var filePaths = new List<string>();

        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            var tracked = await _repository.GetTrackedAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            if (StatutDemandePaiement.Normaliser(tracked.Statut) != StatutDemandePaiement.Brouillon)
                throw new DemandePaiementConcurrencyException();

            foreach (var piece in tracked.PiecesJointes)
            {
                if (!string.IsNullOrWhiteSpace(piece.CheminRelatif))
                    filePaths.Add(piece.CheminRelatif);
            }

            await _repository.DeleteDemandeGraphAsync(tracked, ct);
            return true;
        }, cancellationToken);

        foreach (var path in filePaths)
        {
            try
            {
                await _fileStorage.DeleteAsync(path, cancellationToken);
            }
            catch
            {
                // best-effort cleanup
            }
        }

        return true;
    }

    public async Task<DemandePaiementPieceContentDto?> GetPieceContentAsync(
        long idDemande,
        long idPiece,
        CancellationToken cancellationToken = default)
    {
        var perf = DocumentPerfScope.Current;
        ExigerLecture();

        var sw = Stopwatch.StartNew();
        var demande = await _repository.GetByIdAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, cancellationToken);
        if (perf is not null)
            perf.GetDemandeAccessMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var piece = await _repository.GetPieceTrackedAsync(idPiece, cancellationToken);
        if (perf is not null)
            perf.GetPieceMs = sw.ElapsedMilliseconds;
        if (piece is null || piece.FK_DemandePaiement != idDemande)
            return null;

        if (string.IsNullOrWhiteSpace(piece.CheminRelatif))
            return null;

        sw.Restart();
        var stream = await _fileStorage.OpenReadAsync(piece.CheminRelatif, cancellationToken);
        if (perf is not null)
            perf.OpenFileMs = sw.ElapsedMilliseconds;
        if (stream is null)
            return null;

        var contentType = ResolveContentType(piece.NomFichierOriginal);
        return new DemandePaiementPieceContentDto(
            stream,
            piece.NomFichierOriginal,
            contentType,
            piece.TailleOctets);
    }

    private static string ResolveContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".tif" or ".tiff" => "image/tiff",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream",
        };
    }

    public Task<DemandePaiementDetailDto> SoumettreAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            using var perf = MutationPerfScope.Begin(_logger, MutationPerfOperations.Soumettre, idDemande);
            await perf.TrackAsync("Permission", () => { ExigerSoumettre(); return Task.CompletedTask; });
            var userId = _currentUser.RequireUserId();

            var header = await perf.TrackAsync("LectureHeader",
                () => _repository.GetMutationHeaderAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            ExigerStatut(header, StatutDemandePaiement.ValideeEntite,
                "La demande doit être validée par l'entité avant soumission au Budget.");
            await perf.TrackAsync("AccesDemande",
                () => GarantirAccesDemandeAsync(header, DemandePaiementAccesAction.Soumettre, ct));
            ValiderEnteteDemande(header);

            var validations = await perf.TrackAsync("LectureValidationsSoumettre",
                () => _repository.GetValidationsEntiteSoumettreAsync(idDemande, ct));
            var demande = await perf.TrackAsync("LectureEmpreinte",
                () => _repository.GetEmpreinteReadAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");
            demande.ValidationsEntite = validations.ToList();

            ValidationEntiteRules.ExigerEntiteEntierementValidee(demande);
            ValidationEntiteRules.ExigerDocumentSigneSiPhysique(demande);

            await perf.TrackAsync("ValiderPiecesObligatoires",
                () => ValiderPiecesObligatoiresAsync(demande, ct));

            var statutAvant = header.Statut;
            var now = DateTime.Now;
            var transitionStub = new DemandePaiementEntity { Statut = header.Statut };
            await perf.TrackAsync("TransitionStatut",
                () => AppliquerTransitionStatutAsync(
                    idDemande,
                    transitionStub,
                    StatutDemandePaiement.Soumise,
                    new DemandePaiementConditionalUpdatePatch
                    {
                        FK_UtilisateurSoumission = userId,
                        DateSoumission = now,
                        FK_UtilisateurModification = userId,
                        DateModification = now,
                    },
                    ct));

            await perf.TrackAsync("Audit",
                () => _repository.AddAuditAsync(userId, "SOUMETTRE", idDemande,
                    new { statut = statutAvant },
                    new { statut = StatutDemandePaiement.Soumise },
                    ct));

            AppliquerSoumissionSurDemande(demande, userId, now);
            demande.Statut = StatutDemandePaiement.Soumise;
            await perf.TrackAsync("EnrichMapDetailShell",
                () => _repository.EnrichDemandeMapDetailShellAsync(demande, ct));
            await perf.TrackAsync("EnrichStatutsInstruments",
                () => _repository.EnrichStatutsInstrumentsMapDetailAsync(demande, ct));
            var dto = perf.Track("Mapping", () => MapperDetailApresMutation(demande));
            perf.LogTotal();
            return dto;
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> ReceptionnerAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerChargeDpm();
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Receptionner, ct);
            ExigerStatut(demande, StatutDemandePaiement.Soumise, "La demande doit être soumise.");

            var statutAvant = demande.Statut;
            var now = DateTime.Now;
            var source = demande.FK_UtilisateurSoumission ?? demande.FK_UtilisateurCreation;
            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                StatutDemandePaiement.EnTraitementDpm,
                new DemandePaiementConditionalUpdatePatch
                {
                    FK_UtilisateurReception = userId,
                    DateReception = now,
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                    FK_UtilisateurAssigne = userId,
                    MettreAJourAssigne = true,
                },
                ct);

            await EnregistrerTransmissionAsync(
                idDemande,
                source,
                userId,
                statutAvant,
                StatutDemandePaiement.EnTraitementDpm,
                DemandePaiementRoutageAction.Receptionner,
                now,
                null,
                ct);

            demande.FK_UtilisateurAssigne = userId;

            await _repository.AddAuditAsync(userId, "RECEPTIONNER", idDemande,
                new { statut = statutAvant },
                new { statut = StatutDemandePaiement.EnTraitementDpm },
                ct);

            return (await MapperDetailApresMutationAsync(idDemande, ct));
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> PrendreEnControleAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerControlerBudget();
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.PrendreEnCharge, ct);

            ExigerStatut(
                demande,
                StatutDemandePaiement.EnTraitementDpm,
                "La demande doit être en traitement DPM.");

            if (demande.FK_TypeBudget is null)
            {
                throw new InvalidOperationException(
                    "Orientez la demande (DC / AE / BI) avant le contrôle budgétaire.");
            }

            var statutAvant = demande.Statut;
            var now = DateTime.Now;
            var routages = await ChargerRoutagesAsync(idDemande, ct);
            var source = DemandePaiementRoutageRules.ResoudreSourcePourPriseEnCharge(demande, routages)
                ?? throw new InvalidOperationException(
                    "Impossible d'identifier l'amont métier pour la prise en charge.");

            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                StatutDemandePaiement.EnControleBudgetaire,
                new DemandePaiementConditionalUpdatePatch
                {
                    FK_UtilisateurControle = userId,
                    DateControle = now,
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                    FK_UtilisateurAssigne = userId,
                    MettreAJourAssigne = true,
                },
                ct);

            await EnregistrerTransmissionAsync(
                idDemande,
                source,
                userId,
                statutAvant,
                StatutDemandePaiement.EnControleBudgetaire,
                DemandePaiementRoutageAction.Controler,
                now,
                null,
                ct);

            demande.FK_UtilisateurAssigne = userId;

            await _repository.AddAuditAsync(userId, "CONTROLER", idDemande,
                new { statut = statutAvant },
                new { statut = StatutDemandePaiement.EnControleBudgetaire },
                ct);

            return await MapperDetailApresMutationAsync(idDemande, ct);
        }, cancellationToken);

    public async Task<ControleBudgetaireDto> ControlerBudgetaireAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerControlerBudget();

        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.ControlerBudget, cancellationToken);

        ExigerStatut(
            demande,
            StatutDemandePaiement.EnControleBudgetaire,
            "La demande doit être en contrôle budgétaire.");

        return await DemandePaiementControleBudgetaire.ControlerDemandeAsync(
            demande,
            _repository,
            cancellationToken);
    }

    public Task<DemandePaiementDetailDto> RetournerAsync(
        long idDemande,
        RetourDemandePaiementRequest request,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerRetourner();
            var userId = _currentUser.RequireUserId();

            var motif = (request.MotifRetour ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(motif))
                throw new InvalidOperationException("Le motif du retour est obligatoire.");

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Retourner, ct);

            var statutActuel = StatutDemandePaiement.Normaliser(demande.Statut);
            ExigerStatutMutation(
                demande,
                [StatutDemandePaiement.EnTraitementDpm, StatutDemandePaiement.EnControleBudgetaire],
                "Le retour n'est possible qu'en traitement DPM ou en contrôle budgétaire.");

            var permissions = _currentUser.Permissions
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var routages = await ChargerRoutagesAsync(idDemande, ct);

            string nouveauStatut;
            long cible;
            string actionRoutage;

            if (statutActuel == StatutDemandePaiement.EnTraitementDpm)
            {
                nouveauStatut = StatutDemandePaiement.ACorriger;
                cible = demande.FK_UtilisateurCreation;
                actionRoutage = DemandePaiementRoutageAction.RetourDemandeur;
            }
            else if (DemandePaiementRoutageRules.EstRetourVisa(demande, userId, permissions))
            {
                var controleur = DemandePaiementRoutageRules.ResoudreRetourVisaVersControleur(
                    routages,
                    demande.FK_UtilisateurAssigne);
                if (controleur is null)
                {
                    throw new InvalidOperationException(
                        "Impossible d'identifier le contrôleur destinataire du retour visa.");
                }

                nouveauStatut = StatutDemandePaiement.EnControleBudgetaire;
                cible = controleur.Value;
                actionRoutage = DemandePaiementRoutageAction.RetourVisa;
            }
            else
            {
                var charge = DemandePaiementRoutageRules.ResoudreRetourJuniorVersCharge(routages);
                if (charge is null)
                {
                    throw new InvalidOperationException(
                        "Impossible d'identifier le chargé DPM destinataire du retour inter-étapes.");
                }

                nouveauStatut = StatutDemandePaiement.EnTraitementDpm;
                cible = charge.Value;
                actionRoutage = DemandePaiementRoutageAction.RetourInterEtapes;
            }

            var etape = string.IsNullOrWhiteSpace(request.EtapeConcernee)
                ? statutActuel
                : request.EtapeConcernee.Trim().ToUpperInvariant();
            var commentaire = request.CommentaireRetour?.Trim();
            if (!string.IsNullOrWhiteSpace(etape))
            {
                commentaire = string.IsNullOrWhiteSpace(commentaire)
                    ? $"Étape concernée : {etape}."
                    : $"Étape concernée : {etape}. {commentaire}";
            }

            var statutAvant = demande.Statut;
            var now = DateTime.Now;
            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                nouveauStatut,
                new DemandePaiementConditionalUpdatePatch
                {
                    MotifRetour = motif,
                    CommentaireRetour = commentaire,
                    FK_UtilisateurRetour = userId,
                    DateRetour = now,
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                    FK_UtilisateurAssigne = cible,
                    MettreAJourAssigne = true,
                },
                ct);

            await EnregistrerTransmissionAsync(
                idDemande,
                userId,
                cible,
                statutAvant,
                nouveauStatut,
                actionRoutage,
                now,
                motif,
                ct);

            demande.FK_UtilisateurAssigne = cible;

            await _repository.AddAuditAsync(userId, "RETOURNER", idDemande,
                new { statut = statutAvant },
                new
                {
                    statut = nouveauStatut,
                    motif,
                    etape,
                    utilisateur = userId,
                    cible,
                    action = actionRoutage,
                },
                ct);

            return await MapperDetailApresMutationAsync(idDemande, ct);
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> RemettreEnBrouillonAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerEcrire();
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Modifier, ct);

            ExigerStatut(
                demande,
                StatutDemandePaiement.ACorriger,
                "La demande doit être à corriger.");

            var statutAvant = demande.Statut;
            var now = DateTime.Now;
            var createur = demande.FK_UtilisateurCreation;
            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                StatutDemandePaiement.Brouillon,
                new DemandePaiementConditionalUpdatePatch
                {
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                    FK_UtilisateurAssigne = createur,
                    MettreAJourAssigne = true,
                },
                ct);

            await _repository.DesactiverRoutagesActifsAsync(idDemande, ct);

            var tracked = await _repository.GetTrackedAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");
            tracked.MotifRetour = null;
            tracked.CommentaireRetour = null;
            tracked.FK_UtilisateurRetour = null;
            tracked.DateRetour = null;
            tracked.FK_UtilisateurAssigne = createur;
            ValidationEntiteRules.ReinitialiserValidations(tracked);
            await _repository.SaveChangesAsync(ct);

            await _repository.AddAuditAsync(userId, "MODIFIER", idDemande,
                new { statut = statutAvant },
                new { statut = StatutDemandePaiement.Brouillon },
                ct);

            return (await RequireDetailAsync(idDemande, ct))!;
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> ViserAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerViserBudget();
            var userId = _currentUser.RequireUserId();
            var now = DateTime.Now;

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.ViserBudget, ct);

            ExigerStatut(
                demande,
                StatutDemandePaiement.EnControleBudgetaire,
                "La demande doit être en contrôle budgétaire.");

            var detail = demande;

            await ValiderPiecesObligatoiresAsync(detail, ct);
            await ValiderImputationsAsync(detail, ct);

            var controle = await DemandePaiementControleBudgetaire.ControlerDemandeAsync(
                detail,
                _repository,
                ct);

            if (!controle.EstValide)
            {
                throw new InvalidOperationException(
                    controle.MotifRejet ?? "La demande ne peut pas être visée : crédit disponible insuffisant.");
            }

            foreach (var imputation in detail.Imputations)
            {
                var codeType = imputation.TypeBudget?.CodeType
                    ?? (await _repository.GetTypeBudgetAsync(imputation.FK_TypeBudget, ct))?.CodeType
                    ?? throw new InvalidOperationException(
                        $"Type budget introuvable pour l'imputation {imputation.Ordre}.");

                var prevision = imputation.FK_BudgetLigne is long idPrev
                    ? await _repository.GetPrevisionAsync(idPrev, ct)
                    : null;

                var controleImputation = await DemandePaiementControleBudgetaire.ControleImputationAsync(
                    imputation,
                    codeType,
                    prevision,
                    idDemande,
                    _repository,
                    cancellationToken: ct);

                var trackedImputation = await _repository.GetImputationTrackedAsync(imputation.IdImputation, ct)
                    ?? throw new InvalidOperationException(
                        $"Imputation {imputation.Ordre} introuvable pour le visa.");

                trackedImputation.Snapshots.Add(
                    DemandePaiementControleBudgetaire.CreerSnapshot(
                        trackedImputation,
                        idDemande,
                        controleImputation,
                        now));
            }

            await _repository.SaveChangesAsync(ct);

            var statutAvant = demande.Statut;
            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                StatutDemandePaiement.ViseeBudgetairement,
                new DemandePaiementConditionalUpdatePatch
                {
                    FK_UtilisateurVisa = userId,
                    DateVisa = now,
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                },
                ct);

            await _repository.AddAuditAsync(userId, "VISER", idDemande,
                new { statut = statutAvant },
                new { statut = StatutDemandePaiement.ViseeBudgetairement, nbSnapshots = detail.Imputations.Count },
                ct);

            return (await RequireDetailAsync(idDemande, ct))!;
        }, cancellationToken);

    public async Task<IReadOnlyList<PieceManquanteDto>> GetPiecesManquantesAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();

        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, cancellationToken);
        return await ListerPiecesManquantesInterneAsync(demande, cancellationToken);
    }

    public async Task<HistoriqueDemandePaiementDto?> GetHistoriqueAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();

        var demande = await _repository.GetByIdAsync(idDemande, cancellationToken);
        if (demande is null)
            return null;

        await GarantirAccesDemandeAsync(demande, cancellationToken);

        var audits = await _repository.GetHistoriqueAsync(idDemande, cancellationToken);
        return new HistoriqueDemandePaiementDto(
            idDemande,
            demande.Reference,
            audits.Select(MapAudit).ToList());
    }

    public async Task<IReadOnlyList<DemandePaiementRoutageDto>?> GetRoutageAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();

        var demande = await _repository.GetByIdAsync(idDemande, cancellationToken);
        if (demande is null)
            return null;

        await GarantirAccesDemandeAsync(demande, cancellationToken);

        var routages = await _repository.GetRoutagesLectureAsync(idDemande, cancellationToken);
        return routages.Select(MapRoutage).ToList();
    }

    public async Task<IReadOnlyList<DemandePaiementRetourDestinataireDto>?> GetRetoursDestinatairesAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();

        var demande = await _repository.GetByIdAsync(idDemande, cancellationToken);
        if (demande is null)
            return null;

        await GarantirAccesDemandeAsync(demande, cancellationToken);

        var routages = await _repository.GetRoutagesLectureAsync(idDemande, cancellationToken);
        return DemandePaiementRetourLecture.FromRoutages(routages);
    }

    private async Task<IReadOnlyList<PieceManquanteDto>> ListerPiecesManquantesInterneAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
    {
        var obligatoires = await _repository.GetPiecesObligatoiresAsync(demande.FK_CasDossier, cancellationToken);
        return CalculerPiecesManquantes(obligatoires, demande.PiecesJointes);
    }

    private async Task ValiderPiecesObligatoiresAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
    {
        var obligatoires = await _repository.GetPiecesObligatoiresAsync(demande.FK_CasDossier, cancellationToken);
        var manquantes = CalculerPiecesManquantes(obligatoires, demande.PiecesJointes);
        if (manquantes.Count > 0)
        {
            throw new InvalidOperationException(
                $"La pièce obligatoire « {manquantes[0].Libelle} » est manquante.");
        }
    }

    private async Task ValiderImputationsAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
    {
        if (demande.Imputations.Count == 0)
            throw new InvalidOperationException("Au moins une imputation budgétaire est requise.");

        foreach (var imputation in demande.Imputations.OrderBy(i => i.Ordre))
        {
            DemandePaiementImputationRules.ValiderStructure(imputation);

            var codeType = imputation.TypeBudget?.CodeType
                ?? (await _repository.GetTypeBudgetAsync(imputation.FK_TypeBudget, cancellationToken))?.CodeType;

            if (codeType is not null)
                DemandePaiementImputationRules.ValiderCoherenceTypeBudget(codeType, imputation);
        }

        var totalImputations = demande.Imputations.Sum(i => i.MontantUsd);
        if (demande.MontantUsd is not decimal montantDemandeUsd)
        {
            throw new InvalidOperationException(
                "Le montant converti de la demande n'est pas encore renseigné ; conversion requise avant contrôle/visa.");
        }

        if (Math.Abs(totalImputations - montantDemandeUsd) >= DemandePaiementMontants.ToleranceUsd)
        {
            throw new InvalidOperationException(
                "La somme des imputations ne correspond pas au montant USD de la demande.");
        }
    }

    private async Task ResoudreVersionEtLignesAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
    {
        var idVersion = await _repository.ResolveVersionBudgetaireValideeAsync(
            demande.FK_ExerciceBudgetaire,
            demande.FK_UniteBudgetaire,
            cancellationToken);

        if (idVersion is null)
        {
            throw new InvalidOperationException(
                "Impossible de soumettre : aucune version budgétaire validée pour cette UB.");
        }

        var statutUb = await _repository.GetWorkflowStatutUbAsync(
            idVersion.Value,
            demande.FK_UniteBudgetaire,
            cancellationToken);

        if (!string.Equals(statutUb, StatutVersionBudgetaire.Validee, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Impossible de soumettre : le budget de l'UB n'est pas validé.");
        }

        demande.FK_VersionBudgetaire = idVersion;

        foreach (var imputation in demande.Imputations)
        {
            if (imputation.FK_BudgetLigne.HasValue)
                continue;

            var idPrevision = await _repository.FindPrevisionIdAsync(
                imputation,
                idVersion.Value,
                cancellationToken);

            if (idPrevision.HasValue)
                imputation.FK_BudgetLigne = idPrevision;
        }
    }

    private async Task AppliquerTauxDemandeEtImputationsAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
    {
        var dateReference = DateTraitementDpm();
        var (taux, idTaux, montantUsd) = await AppliquerTauxAsync(
            demande.MontantBrut,
            demande.Devise,
            dateReference,
            cancellationToken);

        demande.TauxConversion = taux;
        demande.MontantUsd = montantUsd;
        demande.FK_TauxChange = idTaux;

        foreach (var imputation in demande.Imputations)
        {
            var (tauxImp, _, montantUsdImp) = await AppliquerTauxAsync(
                imputation.MontantBrut,
                imputation.Devise,
                dateReference,
                cancellationToken);

            imputation.TauxConversion = tauxImp;
            imputation.MontantUsd = montantUsdImp;
        }
    }

    private async Task<(decimal Taux, long? IdTaux, decimal MontantUsd)> AppliquerTauxAsync(
        decimal montantBrut,
        string devise,
        DateOnly dateEmission,
        CancellationToken cancellationToken)
    {
        var dev = DemandePaiementMontants.NormaliserCodeDevise(devise);
        DemandePaiementMontants.ValiderDevise(dev);

        var conversion = await _tauxChange.ConvertirVersUsdAsync(
            montantBrut,
            dev,
            dateEmission,
            cancellationToken);

        return (conversion.TauxConversion, conversion.IdTauxChange, conversion.MontantUsd);
    }

    private static IReadOnlyList<PieceManquanteDto> CalculerPiecesManquantes(
        IReadOnlyList<CasDossierPieceObligatoire> obligatoires,
        IEnumerable<PieceJointe> fournies)
    {
        var pieces = fournies.ToList();
        var manquantes = new List<PieceManquanteDto>();

        foreach (var obligatoire in obligatoires.OrderBy(o => o.Ordre))
        {
            var presente = pieces.Any(p =>
                p.FK_PieceObligatoire == obligatoire.IdPieceObligatoire
                || string.Equals(p.CodeTypePiece, obligatoire.CodeTypePiece, StringComparison.OrdinalIgnoreCase));

            if (!presente)
            {
                manquantes.Add(new PieceManquanteDto(
                    obligatoire.IdPieceObligatoire,
                    obligatoire.CodeTypePiece,
                    obligatoire.Libelle));
            }
        }

        return manquantes;
    }

    private async Task<bool> ResoudreEstObligatoirePieceAsync(
        DemandePaiementEntity demande,
        UploadDemandePaiementPieceMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (metadata.IdPieceObligatoire is long idConfig)
        {
            var config = await _casDossiers.GetPieceByIdAsync(idConfig, cancellationToken)
                ?? throw new InvalidOperationException("Configuration de pièce justificative introuvable.");

            if (config.FK_CasDossier != demande.FK_CasDossier)
            {
                throw new InvalidOperationException(
                    "La pièce justificative ne correspond pas au cas de dossier de la demande.");
            }

            if (!config.Actif)
            {
                throw new InvalidOperationException(
                    $"La pièce « {config.Libelle} » n'est pas active pour ce cas de dossier.");
            }

            return config.Obligatoire;
        }

        var code = metadata.CodeTypePiece.Trim();
        if (string.Equals(code, TypePieceJointeDpm.DocumentDpmSigne, StringComparison.OrdinalIgnoreCase))
            return false;

        if (TypeInstrumentPaiement.IsValid(code))
            return true;

        return false;
    }

    private async Task<DemandePaiementDetailDto> RequireDetailAsync(
        long idDemande,
        CancellationToken cancellationToken)
        => await GetByIdAsync(idDemande, cancellationToken)
           ?? throw new InvalidOperationException("Demande de paiement introuvable après opération.");

    private static void ValiderEnteteCreation(CreateDemandePaiementRequest request)
    {
        if (request.IdExercice <= 0)
            throw new ArgumentException("L'exercice budgétaire est obligatoire.");

        if (request.IdDemandeur <= 0)
            throw new ArgumentException("Le demandeur est obligatoire.");

        if (request.IdCasDossier <= 0)
            throw new ArgumentException("Le cas de dossier est obligatoire.");

        if (string.IsNullOrWhiteSpace(request.Objet))
            throw new ArgumentException("L'objet est obligatoire.");

        if (request.MontantBrut < 0)
            throw new ArgumentException("Le montant sollicité ne peut pas être négatif.");

        if (request.IdDevise is null or <= 0 && string.IsNullOrWhiteSpace(request.Devise))
            throw new ArgumentException("La devise est obligatoire.");

        ValiderSollicitationEntete(request.TypeBudgetSollicite, request.ItemSollicite, request.ModePaiementSollicite);
    }

    private static void ValiderEnteteModification(UpdateDemandePaiementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Objet))
            throw new ArgumentException("L'objet est obligatoire.");

        if (request.MontantBrut < 0)
            throw new ArgumentException("Le montant sollicité ne peut pas être négatif.");

        if (request.IdDevise is null or <= 0 && string.IsNullOrWhiteSpace(request.Devise))
            throw new ArgumentException("La devise est obligatoire.");

        ValiderSollicitationEntete(request.TypeBudgetSollicite, request.ItemSollicite, request.ModePaiementSollicite);
    }

    private static void ValiderSollicitationEntete(
        string? typeBudgetSollicite,
        string? itemSollicite,
        string? modePaiementSollicite)
    {
        DestinationBudgetaireSolliciteeRules.Valider(typeBudgetSollicite, itemSollicite);

        var mode = ModePaiementDpm.Normaliser(modePaiementSollicite);
        if (!ModePaiementDpm.IsValid(mode))
            throw new ArgumentException("Le mode de paiement sollicité doit être CAISSE ou BANQUE.");
    }

    private static void AppliquerSollicitationEntete(
        DemandePaiementEntity entity,
        string typeBudgetSollicite,
        string? itemSollicite,
        string modePaiementSollicite)
    {
        ValiderSollicitationEntete(typeBudgetSollicite, itemSollicite, modePaiementSollicite);
        entity.TypeBudgetSollicite = DestinationBudgetaireSolliciteeRules.NormaliserType(typeBudgetSollicite);
        entity.ItemSollicite = DestinationBudgetaireSolliciteeRules.NormaliserItem(itemSollicite);
        entity.ModePaiementSollicite = ModePaiementDpm.Normaliser(modePaiementSollicite);
    }

    /// <summary>
    /// Résout IdDevise + code. IdDevise prioritaire ; sinon code (EURO→EUR).
    /// Une devise déjà liée à la demande peut rester sélectionnable même inactive.
    /// </summary>
    private async Task<(long IdDevise, string Code)> ResoudreDeviseAsync(
        long? idDevise,
        string? codeDevise,
        bool exigerActif,
        CancellationToken cancellationToken,
        long? idDeviseCourant = null)
    {
        Devise? row = null;
        if (idDevise is > 0)
        {
            row = await _devises.GetByIdAsync(idDevise.Value, cancellationToken)
                ?? throw new InvalidOperationException("Devise introuvable.");
        }
        else
        {
            var code = DemandePaiementMontants.NormaliserCodeDevise(codeDevise);
            DemandePaiementMontants.ValiderDevise(code);
            row = await _devises.GetByCodeAsync(code, cancellationToken)
                ?? throw new InvalidOperationException($"Devise « {code} » introuvable dans le référentiel.");
        }

        if (exigerActif && !row.Actif && row.IdDevise != idDeviseCourant)
            throw new InvalidOperationException($"La devise « {row.Code} » est inactive.");

        return (row.IdDevise, row.Code);
    }

    private static void ValiderEnteteDemande(DemandePaiementEntity demande)
    {
        if (demande.FK_Demandeur is null or <= 0)
            throw new InvalidOperationException("Le demandeur est obligatoire.");

        if (string.IsNullOrWhiteSpace(demande.Objet))
            throw new InvalidOperationException("L'objet est obligatoire.");

        if (demande.MontantBrut < 0)
            throw new InvalidOperationException("Le montant sollicité ne peut pas être négatif.");

        if (demande.FK_CasDossier <= 0)
            throw new InvalidOperationException("Le cas de dossier est obligatoire.");

        DemandePaiementMontants.ValiderDevise(demande.Devise);

        if (string.IsNullOrWhiteSpace(demande.TypeBudgetSollicite))
            throw new InvalidOperationException("La destination budgétaire sollicitée est obligatoire.");

        if (string.IsNullOrWhiteSpace(demande.ModePaiementSollicite))
            throw new InvalidOperationException("Le mode de paiement sollicité est obligatoire.");

        DestinationBudgetaireSolliciteeRules.Valider(demande.TypeBudgetSollicite, demande.ItemSollicite);
    }

    private static void ValiderEnteteDemande(DemandePaiementMutationHeaderReadModel demande)
    {
        if (demande.FK_Demandeur is null or <= 0)
            throw new InvalidOperationException("Le demandeur est obligatoire.");

        if (string.IsNullOrWhiteSpace(demande.Objet))
            throw new InvalidOperationException("L'objet est obligatoire.");

        if (demande.MontantBrut < 0)
            throw new InvalidOperationException("Le montant sollicité ne peut pas être négatif.");

        if (demande.FK_CasDossier <= 0)
            throw new InvalidOperationException("Le cas de dossier est obligatoire.");

        DemandePaiementMontants.ValiderDevise(demande.Devise);

        if (string.IsNullOrWhiteSpace(demande.TypeBudgetSollicite))
            throw new InvalidOperationException("La destination budgétaire sollicitée est obligatoire.");

        if (string.IsNullOrWhiteSpace(demande.ModePaiementSollicite))
            throw new InvalidOperationException("Le mode de paiement sollicité est obligatoire.");

        DestinationBudgetaireSolliciteeRules.Valider(demande.TypeBudgetSollicite, demande.ItemSollicite);
    }

    private static void ValiderEnteteDemande(DemandePaiementPdfData demande)
    {
        if (demande.FK_Demandeur <= 0)
            throw new InvalidOperationException("Le demandeur est obligatoire.");

        if (string.IsNullOrWhiteSpace(demande.Objet))
            throw new InvalidOperationException("L'objet est obligatoire.");

        if (demande.MontantBrut < 0)
            throw new InvalidOperationException("Le montant sollicité ne peut pas être négatif.");

        if (demande.FK_CasDossier <= 0)
            throw new InvalidOperationException("Le cas de dossier est obligatoire.");

        DemandePaiementMontants.ValiderDevise(demande.Devise);

        if (string.IsNullOrWhiteSpace(demande.TypeBudgetSollicite))
            throw new InvalidOperationException("La destination budgétaire sollicitée est obligatoire.");

        if (string.IsNullOrWhiteSpace(demande.ModePaiementSollicite))
            throw new InvalidOperationException("Le mode de paiement sollicité est obligatoire.");

        DestinationBudgetaireSolliciteeRules.Valider(demande.TypeBudgetSollicite, demande.ItemSollicite);
    }

    private static DemandePaiementBeneficiaire CreerBeneficiaire(
        CreateBeneficiaireRequest request,
        long idDemande)
    {
        if (!TypeBeneficiaireDemandePaiement.IsValid(request.TypeBeneficiaire))
            throw new ArgumentException("Type de bénéficiaire invalide.");

        if (string.IsNullOrWhiteSpace(request.NomComplet))
            throw new ArgumentException("Le nom complet du bénéficiaire est obligatoire.");

        return new DemandePaiementBeneficiaire
        {
            FK_DemandePaiement = idDemande,
            TypeBeneficiaire = TypeBeneficiaireDemandePaiement.Normaliser(request.TypeBeneficiaire),
            NomComplet = request.NomComplet.Trim(),
            Matricule = request.Matricule?.Trim(),
            Fonction = request.Fonction?.Trim(),
            RaisonSociale = request.RaisonSociale?.Trim(),
            Rccm = request.Rccm?.Trim(),
            Adresse = request.Adresse?.Trim(),
            Banque = request.Banque?.Trim(),
            NumeroCompte = request.NumeroCompte?.Trim(),
            EstPrincipal = request.EstPrincipal,
            Ordre = request.Ordre,
        };
    }

    private static void ExigerStatutMutation(
        DemandePaiementEntity demande,
        string statutAttendu,
        string messageErreurMetier)
        => ExigerStatutMutation(demande, [statutAttendu], messageErreurMetier);

    private static void ExigerStatutMutation(
        DemandePaiementEntity demande,
        IReadOnlyList<string> statutsAttendus,
        string messageErreurMetier)
    {
        var actuel = StatutDemandePaiement.Normaliser(demande.Statut);
        if (statutsAttendus.Any(s => string.Equals(actuel, StatutDemandePaiement.Normaliser(s), StringComparison.Ordinal)))
            return;

        if (statutsAttendus.Any(s => StatutDemandePaiement.EstProgressionApres(s, actuel)))
            throw new DemandePaiementConcurrencyException();

        throw new InvalidOperationException(messageErreurMetier);
    }

    private static void ExigerStatut(
        DemandePaiementEntity demande,
        string statutAttendu,
        string message)
        => ExigerStatutMutation(demande, statutAttendu, message);

    private static void ExigerStatut(
        DemandePaiementMutationHeaderReadModel demande,
        string statutAttendu,
        string message)
        => ExigerStatutMutation(
            new DemandePaiementEntity { Statut = demande.Statut },
            statutAttendu,
            message);

    private void ExigerPermission(string permission)
    {
        if (!_currentUser.HasPermission(permission) && !_currentUser.HasPermission(AppPermissions.AdminAll))
            throw new UnauthorizedAccessException($"Permission requise : {permission}.");
    }

    private void ExigerLecture()
    {
        if (_currentUser.HasPermission(AppPermissions.PaiementsLire)
            || _currentUser.HasPermission(AppPermissions.PaiementsEcrire)
            || _currentUser.HasPermission(AppPermissions.PaiementsSoumettre)
            || _currentUser.HasPermission(AppPermissions.PaiementsEnvoyerValidation)
            || _currentUser.HasPermission(AppPermissions.PaiementsValiderN1)
            || _currentUser.HasPermission(AppPermissions.PaiementsValiderN2)
            || _currentUser.HasPermission(AppPermissions.PaiementsDeclarerValidationPhysique)
            || _currentUser.HasPermission(AppPermissions.PaiementsImprimer)
            || _currentUser.HasPermission(AppPermissions.PaiementsReceptionBudget)
            || _currentUser.HasPermission(AppPermissions.PaiementsChargeDpm)
            || _currentUser.HasPermission(AppPermissions.PaiementsImputerDc)
            || _currentUser.HasPermission(AppPermissions.PaiementsImputerAe)
            || _currentUser.HasPermission(AppPermissions.PaiementsImputerBi)
            || _currentUser.HasPermission(AppPermissions.PaiementsControlerBudget)
            || _currentUser.HasPermission(AppPermissions.PaiementsViserBudget)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : paiements.lire.");
    }

    private void ExigerEcrire() => ExigerPermission(AppPermissions.PaiementsEcrire);

    private void ExigerSoumettre() => ExigerPermission(AppPermissions.PaiementsSoumettre);

    private void ExigerChargeDpm()
    {
        if (EstChargeDpm())
            return;
        throw new UnauthorizedAccessException("Permission requise : paiements.charge_dpm.");
    }

    private bool EstChargeDpm()
        => _currentUser.HasPermission(AppPermissions.PaiementsChargeDpm)
           || _currentUser.HasPermission(AppPermissions.PaiementsReceptionBudget)
           || _currentUser.HasPermission(AppPermissions.AdminAll);

    private void ExigerControlerBudget() => ExigerPermission(AppPermissions.PaiementsControlerBudget);

    private void ExigerViserBudget() => ExigerPermission(AppPermissions.PaiementsViserBudget);

    private void ExigerRetourner()
    {
        if (EstChargeDpm()
            || _currentUser.HasPermission(AppPermissions.PaiementsControlerBudget)
            || _currentUser.HasPermission(AppPermissions.PaiementsViserBudget)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;
        throw new UnauthorizedAccessException("Permission requise pour retourner la demande.");
    }

    private void ExigerImputer(string codeTypeBudget)
    {
        var code = codeTypeBudget.Trim().ToUpperInvariant();
        var perm = code switch
        {
            TypeBudgetCode.DepensesCourantes => AppPermissions.PaiementsImputerDc,
            TypeBudgetCode.ActionsExploitation => AppPermissions.PaiementsImputerAe,
            TypeBudgetCode.BudgetInvestissement => AppPermissions.PaiementsImputerBi,
            _ => throw new InvalidOperationException($"Type budget non imputable : {codeTypeBudget}."),
        };

        if (_currentUser.HasPermission(perm) || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;
        throw new UnauthorizedAccessException($"Permission requise : {perm}.");
    }

    private static void ExigerTypeBudgetDemande(
        DemandePaiementEntity demande,
        long idTypeBudget,
        string codeType)
    {
        if (demande.FK_TypeBudget is not long idType
            || idType != idTypeBudget)
        {
            throw new InvalidOperationException(
                $"L'imputation doit correspondre au type budget orienté ({codeType}).");
        }
    }

    private void ExigerPieceModifiable(DemandePaiementEntity demande, string? codeTypePiece = null)
    {
        var statut = StatutDemandePaiement.Normaliser(demande.Statut);
        if (statut == StatutDemandePaiement.EnTraitementDpm)
        {
            ExigerChargeDpm();
            return;
        }

        if (string.Equals(codeTypePiece, TypePieceJointeDpm.DocumentDpmSigne, StringComparison.OrdinalIgnoreCase))
        {
            if (statut is StatutDemandePaiement.EnValidationN2 or StatutDemandePaiement.ValideeEntite
                or StatutDemandePaiement.Brouillon or StatutDemandePaiement.ACorriger)
            {
                ExigerPermission(AppPermissions.PaiementsJoindreDocumentSigne);
                return;
            }

            throw new InvalidOperationException(
                "Le document signé ne peut être joint qu'en validation N2 ou avant soumission Budget.");
        }

        ExigerModifiable(demande, "La demande doit être en brouillon, à corriger ou en traitement DPM.");
        ExigerEcrire();
    }

    private bool PeutVoirToutesUb()
        => _currentUser.HasPermission(AppPermissions.AdminAll)
           || _currentUser.HasPermission(AppPermissions.PaiementsImputerDc)
           || _currentUser.HasPermission(AppPermissions.PaiementsImputerAe)
           || _currentUser.HasPermission(AppPermissions.PaiementsImputerBi)
           || _currentUser.HasPermission(AppPermissions.PaiementsControlerBudget)
           || _currentUser.HasPermission(AppPermissions.PaiementsViserBudget);

    private string? FiliereJuniorExclusive()
    {
        if (EstChargeDpm() || _currentUser.HasPermission(AppPermissions.AdminAll))
            return null;

        var dc = _currentUser.HasPermission(AppPermissions.PaiementsImputerDc);
        var ae = _currentUser.HasPermission(AppPermissions.PaiementsImputerAe);
        var bi = _currentUser.HasPermission(AppPermissions.PaiementsImputerBi);
        var n = (dc ? 1 : 0) + (ae ? 1 : 0) + (bi ? 1 : 0);
        if (n != 1)
            return null;
        if (dc) return TypeBudgetCode.DepensesCourantes;
        if (ae) return TypeBudgetCode.ActionsExploitation;
        return TypeBudgetCode.BudgetInvestissement;
    }

    private async Task GarantirAccesUbAsync(long idUB, CancellationToken cancellationToken)
    {
        if (PeutVoirToutesUb())
            return;

        var userId = _currentUser.RequireUserId();
        if (!await _repository.UtilisateurPeutAccederUbAsync(userId, idUB, cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Vous n'avez pas accès à cette unité budgétaire.");
        }
    }

    private async Task<bool> PeutAccederUbPerimetreAsync(
        PerimetreUtilisateurSnapshot snapshot,
        long idUB,
        CancellationToken cancellationToken)
    {
        var idDepartement = await _perimetre.GetDepartementUbAsync(idUB, cancellationToken);
        if (idDepartement is null)
            return false;

        return PerimetreAccess.PeutAccederUb(snapshot, idUB, idDepartement.Value);
    }

    private static void ExigerModifiable(DemandePaiementEntity demande, string message)
        => ExigerStatutMutation(
            demande,
            [StatutDemandePaiement.Brouillon, StatutDemandePaiement.ACorriger],
            message);

    private static SnapshotDto MapSnapshot(DemandePaiementImputationSnapshot snapshot)
        => new(
            snapshot.IdSnapshot,
            snapshot.FK_Imputation,
            snapshot.DateSnapshot,
            snapshot.BudgetMensuel,
            snapshot.CreditEngageMensuel,
            snapshot.CreditDisponibleMensuelAvantVisa,
            snapshot.BudgetAnnuel,
            snapshot.CreditEngageAnnuel,
            snapshot.CreditDisponibleAnnuelAvantVisa,
            snapshot.MontantPrevision,
            snapshot.EcartPrevisionImputation,
            snapshot.MontantBrut,
            snapshot.Devise,
            snapshot.TauxConversion,
            snapshot.MontantUsd,
            snapshot.FK_BudgetLigne);

    private static (long? Id, string? Nom, string? Prenom) MapAssignationUtilisateur(DemandePaiementEntity d)
    {
        if (d.FK_UtilisateurAssigne is not long id)
            return (null, null, null);

        return (id, d.UtilisateurAssigne?.Nom, d.UtilisateurAssigne?.Prenom);
    }

    private static DemandePaiementRoutageDto MapRoutage(DemandePaiementRoutage r)
    {
        long? idSource = r.FK_UtilisateurSource > 0 ? r.FK_UtilisateurSource : null;
        long? idCible = r.FK_UtilisateurCible > 0 ? r.FK_UtilisateurCible : null;
        return new(
            r.IdRoutage,
            r.Action,
            r.StatutSource,
            r.StatutCible,
            idSource,
            r.UtilisateurSource?.Nom,
            r.UtilisateurSource?.Prenom,
            idCible,
            r.UtilisateurCible?.Nom,
            r.UtilisateurCible?.Prenom,
            r.DateRoutage,
            r.EstActif,
            r.Motif);
    }

    private static DemandePaiementListDto MapList(DemandePaiementEntity d)
    {
        var dept = d.UniteBudgetaire.Departement;
        var assignation = MapAssignationUtilisateur(d);
        return new(
            d.IdDemandePaiement,
            d.Reference,
            d.DateEmission,
            d.FK_ExerciceBudgetaire,
            d.ExerciceBudgetaire.Annee,
            d.FK_UniteBudgetaire,
            d.UniteBudgetaire.CodeUB,
            d.UniteBudgetaire.Libelle,
            d.FK_Demandeur,
            d.Demandeur?.Code,
            d.Demandeur?.Libelle,
            dept?.IdDepartement ?? d.UniteBudgetaire.FK_Departement,
            dept?.Code,
            dept?.Libelle,
            d.FK_CasDossier,
            d.CasDossier.Code,
            d.CasDossier.Libelle,
            d.Objet,
            d.MontantBrut,
            d.Devise,
            d.FK_Devise,
            d.MontantUsd,
            d.FK_TypeBudget,
            d.TypeBudget?.CodeType,
            d.TypeBudgetSollicite,
            d.ItemSollicite,
            StatutDemandePaiement.Normaliser(d.Statut),
            d.DateSoumission,
            d.DateCreation,
            assignation.Id,
            assignation.Nom,
            assignation.Prenom,
            d.ModePaiementSollicite,
            d.TypeInstrumentPaiement);
    }

    private static DemandePaiementDetailDto MapDetail(DemandePaiementEntity d)
    {
        var dept = d.UniteBudgetaire.Departement;
        var assignation = MapAssignationUtilisateur(d);
        return new(
            d.IdDemandePaiement,
            d.Reference,
            d.DateEmission,
            d.LieuEmission,
            d.FK_ExerciceBudgetaire,
            d.ExerciceBudgetaire.Annee,
            d.FK_VersionBudgetaire,
            d.VersionBudgetaire?.NumeroVersion,
            d.FK_UniteBudgetaire,
            d.UniteBudgetaire.CodeUB,
            d.UniteBudgetaire.Libelle,
            d.FK_Demandeur,
            d.Demandeur?.Code,
            d.Demandeur?.Libelle,
            dept?.IdDepartement ?? d.UniteBudgetaire.FK_Departement,
            dept?.Code,
            dept?.Libelle,
            d.FK_CasDossier,
            d.CasDossier.Code,
            d.CasDossier.Libelle,
            d.FK_TypeBudget,
            d.TypeBudget?.CodeType,
            d.TypeBudgetSollicite,
            d.ItemSollicite,
            DestinationBudgetaireSolliciteeRules.FormaterDestination(d.TypeBudgetSollicite, d.ItemSollicite),
            d.Objet,
            d.CompteSection,
            d.MontantBrut,
            d.Devise,
            d.FK_Devise,
            d.TauxConversion,
            d.MontantUsd,
            d.FK_TauxChange,
            d.ModePaiementSollicite,
            d.TypeInstrumentPaiement,
            d.DevisePaiement,
            d.MontantPaiement,
            d.TauxPaiement,
            d.FK_TauxChangePaiement,
            StatutDemandePaiement.Normaliser(d.Statut),
            d.MotifRetour,
            d.CommentaireRetour,
            d.DateCreation,
            d.DateSoumission,
            d.DateReception,
            d.DateControle,
            d.DateVisa,
            d.DateRetour,
            d.Beneficiaires.OrderBy(b => b.Ordre).Select(MapBeneficiaire).ToList(),
            d.Imputations.OrderBy(i => i.Ordre).Select(MapImputation).ToList(),
            d.PiecesJointes.OrderBy(p => p.DateUpload).Select(MapPiece).ToList(),
            ResoudreCircuitEntiteStatut(d.Statut),
            d.ValidationsEntite.OrderBy(v => v.Ordre).Select(MapValidation).ToList(),
            ModePaiementVerrouillageRules.EstVerrouille(d),
            ModePaiementVerrouillageRules.MotifVerrouillage(d),
            assignation.Id,
            assignation.Nom,
            assignation.Prenom);
    }

    private static BeneficiaireDto MapBeneficiaire(DemandePaiementBeneficiaire b)
        => new(
            b.IdBeneficiaire,
            b.TypeBeneficiaire,
            b.NomComplet,
            b.Matricule,
            b.Fonction,
            b.RaisonSociale,
            b.Rccm,
            b.Adresse,
            b.Banque,
            b.NumeroCompte,
            b.EstPrincipal,
            b.Ordre);

    private static DemandePaiementImputationDto MapImputation(DemandePaiementImputation i)
        => new(
            i.IdImputation,
            i.Ordre,
            i.FK_TypeBudget,
            i.TypeBudget?.CodeType ?? string.Empty,
            i.FK_UniteBudgetaire,
            i.FK_ExerciceBudgetaire,
            i.FK_RubriqueBudgetaire,
            i.Mois,
            i.LibelleItemAE,
            i.FK_GroupeItemAE,
            i.FK_ItemBI,
            i.DetailBI,
            i.FK_BudgetLigne,
            i.MontantBrut,
            i.Devise,
            i.TauxConversion,
            i.MontantUsd,
            i.NumeroFicheSuivi);

    private static DemandePaiementImputationDto MapImputation(
        DemandePaiementImputation i,
        string codeTypeBudget)
        => MapImputation(i) with { CodeTypeBudget = codeTypeBudget };

    private static DemandePaiementPieceDto MapPiece(PieceJointe p)
        => new(
            p.IdPieceJointe,
            p.FK_PieceObligatoire,
            p.CodeTypePiece,
            p.Libelle,
            p.EstObligatoire,
            p.NomFichierOriginal,
            p.CheminRelatif,
            p.HashSha256,
            p.TailleOctets,
            p.DateUpload);

    private static JournalAuditDemandeDto MapAudit(JournalAudit audit)
        => new(
            audit.IdAudit,
            audit.Operation,
            audit.DateHeure,
            audit.AnciennesValeurs,
            audit.NouvellesValeurs);
}
