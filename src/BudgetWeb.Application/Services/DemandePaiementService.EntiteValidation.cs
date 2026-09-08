using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Diagnostics;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;
using System.Diagnostics;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public Task<DemandePaiementDetailDto> EnvoyerEnValidationAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            using var perf = MutationPerfScope.Begin(_logger, MutationPerfOperations.EnvoyerValidation, idDemande);
            await perf.TrackAsync("Permission", () => { ExigerEnvoyerValidation(); return Task.CompletedTask; });
            var userId = _currentUser.RequireUserId();

            var header = await perf.TrackAsync("LectureHeader",
                () => _repository.GetMutationHeaderAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            ExigerStatut(header, StatutDemandePaiement.Brouillon, "La demande doit être en brouillon.");
            await perf.TrackAsync("AccesDemande",
                () => GarantirAccesDemandeAsync(header, DemandePaiementAccesAction.EnvoyerValidation, ct));
            ValiderEnteteDemande(header);

            var statutAvant = header.Statut;
            var now = DateTime.Now;
            var transitionStub = new DemandePaiementEntity { Statut = header.Statut };
            // Pool N1 : après retour A_CORRIGER / brouillon, l'assignation créateur
            // doit être levée pour ne pas bloquer la validation (physique ou N1).
            await perf.TrackAsync("TransitionStatut",
                () => AppliquerTransitionStatutAsync(
                    idDemande,
                    transitionStub,
                    StatutDemandePaiement.EnValidationN1,
                    new DemandePaiementConditionalUpdatePatch
                    {
                        FK_UtilisateurModification = userId,
                        DateModification = now,
                        FK_UtilisateurAssigne = null,
                        MettreAJourAssigne = true,
                    },
                    ct));

            var tracked = await perf.TrackAsync("GetTrackedAsync",
                () => _repository.GetTrackedAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");
            await perf.TrackAsync("AssurerValidationsInitiales",
                () => AssurerValidationsInitialesAsync(tracked, ct));

            await perf.TrackAsync("Audit",
                () => _repository.AddAuditAsync(userId, "ENVOYER_EN_VALIDATION_N1", idDemande,
                    new { statut = statutAvant },
                    new { statut = StatutDemandePaiement.EnValidationN1 },
                    ct));

            var demande = await perf.TrackAsync("LectureDtoApresEnvoyer",
                () => _repository.GetDetailDtoApresEnvoyerAsync(tracked, ct));
            var dto = perf.Track("Mapping", () => MapDetail(demande));
            perf.LogTotal();
            return dto;
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> RejeterValidationEntiteAsync(
        long idDemande,
        RetourDemandePaiementRequest request,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerRejeterValidationEntite();
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            var statutValidation = StatutDemandePaiement.Normaliser(demande.Statut);
            var accesRejet = statutValidation == StatutDemandePaiement.EnValidationN2
                ? DemandePaiementAccesAction.ValiderN2
                : DemandePaiementAccesAction.ValiderN1;
            await GarantirAccesDemandeAsync(demande, accesRejet, ct);
            ExigerStatutMutation(
                demande,
                [StatutDemandePaiement.EnValidationN1, StatutDemandePaiement.EnValidationN2],
                "La demande doit être en validation entité.");

            if (string.IsNullOrWhiteSpace(request.MotifRetour))
                throw new ArgumentException("Le motif de retour est obligatoire.");

            var statutAvant = StatutDemandePaiement.Normaliser(demande.Statut);
            var now = DateTime.Now;
            var motif = request.MotifRetour.Trim();

            if (statutAvant == StatutDemandePaiement.EnValidationN2)
            {
                var tracked = await _repository.GetTrackedAsync(idDemande, ct)
                    ?? throw new InvalidOperationException("Demande de paiement introuvable.");
                await AssurerValidationsInitialesAsync(tracked, ct);
                var n1 = ValidationEntiteRules.ObtenirNiveau(tracked, ValidationEntiteNiveau.N1);
                var idN1 = DemandePaiementRoutageRules.ResoudreIdentiteValidateurN1(n1)
                    ?? throw new InvalidOperationException(
                        "Impossible d'identifier le validateur N1 destinataire du retour.");

                if (StatutValidationEntite.Normaliser(n1.Statut) != StatutValidationEntite.Validee)
                {
                    throw new InvalidOperationException(
                        "La validation N1 doit être satisfaite pour un retour N2 vers N1.");
                }

                await AppliquerTransitionStatutAsync(
                    idDemande,
                    demande,
                    StatutDemandePaiement.EnValidationN1,
                    new DemandePaiementConditionalUpdatePatch
                    {
                        MotifRetour = motif,
                        CommentaireRetour = request.CommentaireRetour?.Trim(),
                        FK_UtilisateurRetour = userId,
                        DateRetour = now,
                        FK_UtilisateurModification = userId,
                        DateModification = now,
                        FK_UtilisateurAssigne = idN1,
                        MettreAJourAssigne = true,
                    },
                    ct);

                var trackedApres = await _repository.GetTrackedAsync(idDemande, ct)
                    ?? throw new InvalidOperationException("Demande de paiement introuvable.");
                ValidationEntiteRules.ReinitialiserValidationN2(trackedApres);
                await _repository.SaveChangesAsync(ct);

                await EnregistrerTransmissionAsync(
                    idDemande,
                    userId,
                    idN1,
                    statutAvant,
                    StatutDemandePaiement.EnValidationN1,
                    DemandePaiementRoutageAction.RejetValidationEntite,
                    now,
                    motif,
                    ct);

                demande.FK_UtilisateurAssigne = idN1;

                await _repository.AddAuditAsync(userId, "RETOURNER", idDemande,
                    new { statut = statutAvant },
                    new
                    {
                        statut = StatutDemandePaiement.EnValidationN1,
                        demande.MotifRetour,
                        etape = "VALIDATION_ENTITE_N2",
                        cible = idN1,
                    },
                    ct);
            }
            else
            {
                var cible = demande.FK_UtilisateurCreation;
                await AppliquerTransitionStatutAsync(
                    idDemande,
                    demande,
                    StatutDemandePaiement.ACorriger,
                    new DemandePaiementConditionalUpdatePatch
                    {
                        MotifRetour = motif,
                        CommentaireRetour = request.CommentaireRetour?.Trim(),
                        FK_UtilisateurRetour = userId,
                        DateRetour = now,
                        FK_UtilisateurModification = userId,
                        DateModification = now,
                        FK_UtilisateurAssigne = cible,
                        MettreAJourAssigne = true,
                    },
                    ct);

                var tracked = await _repository.GetTrackedAsync(idDemande, ct)
                    ?? throw new InvalidOperationException("Demande de paiement introuvable.");
                ValidationEntiteRules.ReinitialiserValidations(tracked);
                await _repository.SaveChangesAsync(ct);

                await EnregistrerTransmissionAsync(
                    idDemande,
                    userId,
                    cible,
                    statutAvant,
                    StatutDemandePaiement.ACorriger,
                    DemandePaiementRoutageAction.RejetValidationEntite,
                    now,
                    motif,
                    ct);

                demande.FK_UtilisateurAssigne = cible;

                await _repository.AddAuditAsync(userId, "RETOURNER", idDemande,
                    new { statut = statutAvant },
                    new
                    {
                        statut = StatutDemandePaiement.ACorriger,
                        demande.MotifRetour,
                        etape = "VALIDATION_ENTITE_N1",
                        cible,
                    },
                    ct);
            }

            return await MapperDetailApresMutationAsync(idDemande, ct);
        }, cancellationToken);

    private Task<DemandePaiementDetailDto> ValiderElectroniqueAsync(
        long idDemande,
        byte niveau,
        ValidationEntiteRequest? request,
        CancellationToken cancellationToken)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            ExigerValiderElectronique(niveau);
            var userId = _currentUser.RequireUserId();

            var demande = await _repository.GetDetailAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            await GarantirAccesDemandeAsync(
                demande,
                niveau == ValidationEntiteNiveau.N1
                    ? DemandePaiementAccesAction.ValiderN1
                    : DemandePaiementAccesAction.ValiderN2,
                ct);
            ExigerStatutValidation(demande, niveau);

            if (niveau == ValidationEntiteNiveau.N1)
            {
                var n1Existant = ValidationEntiteRules.ObtenirNiveau(demande, ValidationEntiteNiveau.N1);
                if (StatutValidationEntite.Normaliser(n1Existant.Statut) == StatutValidationEntite.Validee
                    && StatutDemandePaiement.Normaliser(demande.Statut) == StatutDemandePaiement.EnValidationN1
                    && demande.FK_UtilisateurAssigne == userId)
                {
                    var statutAvantRelance = demande.Statut;
                    var nowRelance = DateTime.Now;
                    await AppliquerTransitionStatutAsync(
                        idDemande,
                        demande,
                        StatutDemandePaiement.EnValidationN2,
                        new DemandePaiementConditionalUpdatePatch
                        {
                            FK_UtilisateurModification = userId,
                            DateModification = nowRelance,
                            FK_UtilisateurAssigne = null,
                            MettreAJourAssigne = true,
                        },
                        ct);

                    await _repository.DesactiverRoutagesActifsAsync(idDemande, ct);
                    demande.FK_UtilisateurAssigne = null;

                    await _repository.AddAuditAsync(userId, "RELANCER_VALIDATION_N2", idDemande,
                        new { statut = statutAvantRelance },
                        new { statut = StatutDemandePaiement.EnValidationN2 },
                        ct);

                    return await MapperDetailApresMutationAsync(idDemande, ct);
                }
            }

            if (niveau == ValidationEntiteNiveau.N2)
                ValidationEntiteRules.ExigerN1Validee(demande);

            var validation = ValidationEntiteRules.ObtenirNiveau(demande, niveau);
            ValidationEntiteRules.ExigerNiveauEnAttente(validation, niveau);

            var empreinte = DemandePaiementEmpreinte.Calculer(demande);
            var statutAvant = demande.Statut;
            var now = DateTime.Now;
            var nouveauStatut = niveau == ValidationEntiteNiveau.N1
                ? StatutDemandePaiement.EnValidationN2
                : StatutDemandePaiement.ValideeEntite;

            if (niveau == ValidationEntiteNiveau.N2)
                ValidationEntiteRules.ExigerDocumentSigneSiPhysique(demande);

            await AppliquerTransitionStatutAsync(
                idDemande,
                demande,
                nouveauStatut,
                new DemandePaiementConditionalUpdatePatch
                {
                    FK_UtilisateurModification = userId,
                    DateModification = now,
                },
                ct);

            var tracked = await _repository.GetTrackedAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");
            var trackedValidation = ValidationEntiteRules.ObtenirNiveau(tracked, niveau);
            trackedValidation.Statut = StatutValidationEntite.Validee;
            trackedValidation.ModeValidation = ModeValidationEntite.Electronique;
            trackedValidation.FK_UtilisateurValidateur = userId;
            trackedValidation.FK_UtilisateurDeclarant = null;
            trackedValidation.NomSignatairePhysique = null;
            trackedValidation.FonctionSignatairePhysique = null;
            trackedValidation.DateSignaturePhysique = null;
            trackedValidation.DateValidation = now;
            trackedValidation.Commentaire = request?.Commentaire?.Trim();
            trackedValidation.EmpreinteDonnees = empreinte;

            await _repository.SaveChangesAsync(ct);
            var op = niveau == ValidationEntiteNiveau.N1 ? "VALIDER_N1" : "VALIDER_N2";
            await _repository.AddAuditAsync(userId, op, idDemande,
                new { statut = statutAvant, validation = StatutValidationEntite.EnAttente },
                new
                {
                    statut = nouveauStatut,
                    mode = ModeValidationEntite.Electronique,
                    validateur = userId,
                    niveau,
                },
                ct);

            return await MapperDetailApresMutationAsync(idDemande, ct);
        }, cancellationToken);

    private Task<DemandePaiementDetailDto> DeclarerValidationPhysiqueAsync(
        long idDemande,
        byte niveau,
        DeclarationValidationPhysiqueRequest request,
        CancellationToken cancellationToken)
        => niveau == ValidationEntiteNiveau.N1
            ? DeclarerValidationPhysiqueN1CoreAsync(idDemande, request, cancellationToken)
            : DeclarerValidationPhysiqueN2CoreAsync(idDemande, request, cancellationToken);

    private Task<DemandePaiementDetailDto> DeclarerValidationPhysiqueN1CoreAsync(
        long idDemande,
        DeclarationValidationPhysiqueRequest request,
        CancellationToken cancellationToken)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            using var perf = MutationPerfScope.Begin(_logger, MutationPerfOperations.ValidationPhysiqueN1, idDemande);
            await perf.TrackAsync("Permission", () => { ExigerDeclarerValidationPhysique(); return Task.CompletedTask; });
            var userId = _currentUser.RequireUserId();

            ValidationEntiteRules.ValiderDeclarationPhysique(request.NomSignataire, request.DateSignature);

            var header = await perf.TrackAsync("LectureHeader",
                () => _repository.GetMutationHeaderAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            ExigerStatutValidation(header, ValidationEntiteNiveau.N1);
            await perf.TrackAsync("AccesDemande",
                () => GarantirAccesDemandeAsync(
                    header,
                    DemandePaiementAccesAction.DeclarerValidationPhysique,
                    ct));

            var validations = await perf.TrackAsync("LectureValidationsN1",
                () => _repository.GetValidationsEntiteMinimalAsync(idDemande, ct));
            var validationStub = new DemandePaiementEntity { ValidationsEntite = validations.ToList() };
            var validation = ValidationEntiteRules.ObtenirNiveau(validationStub, ValidationEntiteNiveau.N1);
            ValidationEntiteRules.ExigerNiveauEnAttente(validation, ValidationEntiteNiveau.N1);

            var demande = await perf.TrackAsync("LectureEmpreinte",
                () => _repository.GetEmpreinteReadAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            var empreinte = perf.Track("CalculEmpreinte", () => DemandePaiementEmpreinte.Calculer(demande));
            var statutAvant = header.Statut;
            var now = DateTime.Now;
            const string nouveauStatut = StatutDemandePaiement.EnValidationN2;

            var transitionStub = new DemandePaiementEntity { Statut = header.Statut };
            await perf.TrackAsync("TransitionStatut",
                () => AppliquerTransitionStatutAsync(
                    idDemande,
                    transitionStub,
                    nouveauStatut,
                    new DemandePaiementConditionalUpdatePatch
                    {
                        FK_UtilisateurModification = userId,
                        DateModification = now,
                    },
                    ct));

            var tracked = await perf.TrackAsync("GetTrackedAsync",
                () => _repository.GetTrackedAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");
            var trackedValidation = ValidationEntiteRules.ObtenirNiveau(tracked, ValidationEntiteNiveau.N1);
            trackedValidation.Statut = StatutValidationEntite.Validee;
            trackedValidation.ModeValidation = ModeValidationEntite.Physique;
            trackedValidation.FK_UtilisateurValidateur = null;
            trackedValidation.FK_UtilisateurDeclarant = userId;
            trackedValidation.NomSignatairePhysique = request.NomSignataire.Trim();
            trackedValidation.FonctionSignatairePhysique = request.FonctionSignataire?.Trim();
            trackedValidation.DateSignaturePhysique = request.DateSignature;
            trackedValidation.DateValidation = now;
            trackedValidation.Commentaire = request.Commentaire?.Trim();
            trackedValidation.EmpreinteDonnees = empreinte;

            await perf.TrackAsync("SaveChanges", () => _repository.SaveChangesAsync(ct));
            await perf.TrackAsync("Audit",
                () => _repository.AddAuditAsync(userId, "DECLARER_VALIDATION_PHYSIQUE_N1", idDemande,
                    new { statut = statutAvant },
                    new
                    {
                        statut = nouveauStatut,
                        mode = ModeValidationEntite.Physique,
                        declarant = userId,
                        signataire = trackedValidation.NomSignatairePhysique,
                        niveau = ValidationEntiteNiveau.N1,
                    },
                    ct));

            SynchroniserCollectionsDepuisTracked(demande, tracked);
            await perf.TrackAsync("EnrichMapDetailShell",
                () => _repository.EnrichDemandeMapDetailShellAsync(demande, ct));
            demande.Statut = nouveauStatut;
            SynchroniserValidationEntite(demande, tracked, ValidationEntiteNiveau.N1);
            var dto = perf.Track("Mapping", () => MapperDetailApresMutation(demande));
            perf.LogTotal();
            return dto;
        }, cancellationToken);

    private Task<DemandePaiementDetailDto> DeclarerValidationPhysiqueN2CoreAsync(
        long idDemande,
        DeclarationValidationPhysiqueRequest request,
        CancellationToken cancellationToken)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            using var perf = MutationPerfScope.Begin(_logger, MutationPerfOperations.ValidationPhysiqueN2, idDemande);
            await perf.TrackAsync("Permission", () => { ExigerDeclarerValidationPhysique(); return Task.CompletedTask; });
            var userId = _currentUser.RequireUserId();

            ValidationEntiteRules.ValiderDeclarationPhysique(request.NomSignataire, request.DateSignature);

            var header = await perf.TrackAsync("LectureHeader",
                () => _repository.GetMutationHeaderAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            ExigerStatutValidation(header, ValidationEntiteNiveau.N2);
            await perf.TrackAsync("AccesDemande",
                () => GarantirAccesDemandeAsync(
                    header,
                    DemandePaiementAccesAction.DeclarerValidationPhysique,
                    ct));

            var validations = await perf.TrackAsync("LectureValidationsN2",
                () => _repository.GetValidationsEntiteN2Async(idDemande, ct));
            var validationStub = new DemandePaiementEntity { ValidationsEntite = validations.ToList() };
            ValidationEntiteRules.ExigerN1Validee(validationStub);

            var validation = ValidationEntiteRules.ObtenirNiveau(validationStub, ValidationEntiteNiveau.N2);
            ValidationEntiteRules.ExigerNiveauEnAttente(validation, ValidationEntiteNiveau.N2);

            var demande = await perf.TrackAsync("LectureEmpreinte",
                () => _repository.GetEmpreinteReadAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            var empreinte = perf.Track("CalculEmpreinte", () => DemandePaiementEmpreinte.Calculer(demande));
            var statutAvant = header.Statut;
            var now = DateTime.Now;
            const string nouveauStatut = StatutDemandePaiement.ValideeEntite;

            demande.ValidationsEntite = validations.ToList();
            await perf.TrackAsync("ControleDocumentSigne", () =>
            {
                ValidationEntiteRules.ExigerDocumentSigneSiPhysique(demande);
                return Task.CompletedTask;
            });

            var transitionStub = new DemandePaiementEntity { Statut = header.Statut };
            await perf.TrackAsync("TransitionStatut",
                () => AppliquerTransitionStatutAsync(
                    idDemande,
                    transitionStub,
                    nouveauStatut,
                    new DemandePaiementConditionalUpdatePatch
                    {
                        FK_UtilisateurModification = userId,
                        DateModification = now,
                    },
                    ct));

            var tracked = await perf.TrackAsync("GetTrackedAsync",
                () => _repository.GetTrackedAsync(idDemande, ct))
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");
            var trackedValidation = ValidationEntiteRules.ObtenirNiveau(tracked, ValidationEntiteNiveau.N2);
            trackedValidation.Statut = StatutValidationEntite.Validee;
            trackedValidation.ModeValidation = ModeValidationEntite.Physique;
            trackedValidation.FK_UtilisateurValidateur = null;
            trackedValidation.FK_UtilisateurDeclarant = userId;
            trackedValidation.NomSignatairePhysique = request.NomSignataire.Trim();
            trackedValidation.FonctionSignatairePhysique = request.FonctionSignataire?.Trim();
            trackedValidation.DateSignaturePhysique = request.DateSignature;
            trackedValidation.DateValidation = now;
            trackedValidation.Commentaire = request.Commentaire?.Trim();
            trackedValidation.EmpreinteDonnees = empreinte;

            await perf.TrackAsync("SaveChanges", () => _repository.SaveChangesAsync(ct));
            await perf.TrackAsync("Audit",
                () => _repository.AddAuditAsync(userId, "DECLARER_VALIDATION_PHYSIQUE_N2", idDemande,
                    new { statut = statutAvant },
                    new
                    {
                        statut = nouveauStatut,
                        mode = ModeValidationEntite.Physique,
                        declarant = userId,
                        signataire = trackedValidation.NomSignatairePhysique,
                        niveau = ValidationEntiteNiveau.N2,
                    },
                    ct));

            SynchroniserCollectionsDepuisTracked(demande, tracked);
            await perf.TrackAsync("EnrichMapDetailShell",
                () => _repository.EnrichDemandeMapDetailShellAsync(demande, ct));
            demande.Statut = nouveauStatut;
            SynchroniserValidationEntite(demande, tracked, ValidationEntiteNiveau.N2);
            var dto = perf.Track("Mapping", () => MapperDetailApresMutation(demande));
            perf.LogTotal();
            return dto;
        }, cancellationToken);

    public Task<DemandePaiementDetailDto> ValiderN1ElectroniqueAsync(
        long idDemande,
        ValidationEntiteRequest? request = null,
        CancellationToken cancellationToken = default)
        => ValiderElectroniqueAsync(idDemande, ValidationEntiteNiveau.N1, request, cancellationToken);

    public Task<DemandePaiementDetailDto> ValiderN2ElectroniqueAsync(
        long idDemande,
        ValidationEntiteRequest? request = null,
        CancellationToken cancellationToken = default)
        => ValiderElectroniqueAsync(idDemande, ValidationEntiteNiveau.N2, request, cancellationToken);

    public Task<DemandePaiementDetailDto> DeclarerValidationPhysiqueN1Async(
        long idDemande,
        DeclarationValidationPhysiqueRequest request,
        CancellationToken cancellationToken = default)
        => DeclarerValidationPhysiqueAsync(idDemande, ValidationEntiteNiveau.N1, request, cancellationToken);

    public Task<DemandePaiementDetailDto> DeclarerValidationPhysiqueN2Async(
        long idDemande,
        DeclarationValidationPhysiqueRequest request,
        CancellationToken cancellationToken = default)
        => DeclarerValidationPhysiqueAsync(idDemande, ValidationEntiteNiveau.N2, request, cancellationToken);

    public async Task<DemandePaiementDocumentDto?> GetDocumentImpressionAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerImprimer();
        var perf = DocumentPerfScope.Current;
        var sw = Stopwatch.StartNew();
        var contexte = await _repository.GetDemandeAccesContextAsync(idDemande, cancellationToken);
        if (perf is not null)
            perf.GetDemandeAccessMs = sw.ElapsedMilliseconds;
        if (contexte is null)
            return null;

        sw.Restart();
        await GarantirAccesDemandeAsync(contexte, cancellationToken);
        if (perf is not null)
            perf.AccessMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var pdfData = await _repository.GetDemandePaiementPdfDataAsync(idDemande, cancellationToken);
        if (perf is not null)
            perf.GetPdfDataMs = sw.ElapsedMilliseconds;
        if (pdfData is null)
            return null;

        ValiderEnteteDemande(pdfData);

        sw.Restart();
        var dto = MapDocument(pdfData);
        if (perf is not null)
            perf.MappingMs = sw.ElapsedMilliseconds;
        return dto;
    }

    public async Task<byte[]> GenererDocumentPdfAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        using var perf = DocumentPerfScope.Begin(_logger, "DPM-PDF", idDemande);
        ExigerImprimer();
        var payload = await GetDocumentImpressionAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        var userId = _currentUser.RequireUserId();
        var sw = Stopwatch.StartNew();
        await _repository.AddAuditAsync(userId, "IMPRIMER", idDemande,
            null,
            new { payload.Reference },
            cancellationToken);
        perf.AuditMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var pdf = _dpmDocumentRenderer.Render(payload);
        perf.QuestPdfMs = sw.ElapsedMilliseconds;
        perf.LogDpmPdf(pdf.Length);
        return pdf;
    }

    private async Task AssurerValidationsInitialesAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
    {
        if (demande.ValidationsEntite.Count >= 2)
            return;

        var rows = ValidationEntiteRules.CreerValidationsInitiales(demande.IdDemandePaiement);
        foreach (var row in rows)
            demande.ValidationsEntite.Add(row);

        await _repository.SaveChangesAsync(cancellationToken);
    }

    private static void ExigerStatutValidation(DemandePaiementEntity demande, byte niveau)
    {
        var attendu = niveau switch
        {
            ValidationEntiteNiveau.N1 => StatutDemandePaiement.EnValidationN1,
            ValidationEntiteNiveau.N2 => StatutDemandePaiement.EnValidationN2,
            _ => throw new ArgumentOutOfRangeException(nameof(niveau)),
        };

        ExigerStatutMutation(demande, attendu, $"La demande doit être en {attendu}.");
    }

    private static void ExigerStatutValidation(DemandePaiementMutationHeaderReadModel header, byte niveau)
    {
        var attendu = niveau switch
        {
            ValidationEntiteNiveau.N1 => StatutDemandePaiement.EnValidationN1,
            ValidationEntiteNiveau.N2 => StatutDemandePaiement.EnValidationN2,
            _ => throw new ArgumentOutOfRangeException(nameof(niveau)),
        };

        ExigerStatut(header, attendu, $"La demande doit être en {attendu}.");
    }

    private void ExigerEnvoyerValidation() => ExigerPermission(AppPermissions.PaiementsEnvoyerValidation);

    private void ExigerValiderElectronique(byte niveau)
    {
        var perm = niveau switch
        {
            ValidationEntiteNiveau.N1 => AppPermissions.PaiementsValiderN1,
            ValidationEntiteNiveau.N2 => AppPermissions.PaiementsValiderN2,
            _ => throw new ArgumentOutOfRangeException(nameof(niveau)),
        };
        ExigerPermission(perm);
    }

    private void ExigerDeclarerValidationPhysique()
        => ExigerPermission(AppPermissions.PaiementsDeclarerValidationPhysique);

    private void ExigerRejeterValidationEntite()
        => ExigerPermission(AppPermissions.PaiementsRejeterValidationEntite);

    private void ExigerImprimer() => ExigerPermission(AppPermissions.PaiementsImprimer);

    private static ValidationEntiteDto MapValidation(DemandePaiementValidation v)
        => new(
            v.Niveau,
            v.Ordre,
            StatutValidationEntite.Normaliser(v.Statut),
            v.ModeValidation,
            v.FK_UtilisateurValidateur,
            v.UtilisateurValidateur?.Nom,
            v.FK_UtilisateurDeclarant,
            v.UtilisateurDeclarant?.Nom,
            v.NomSignatairePhysique,
            v.FonctionSignatairePhysique,
            v.DateSignaturePhysique,
            v.DateValidation,
            v.Commentaire);

    private static DemandePaiementDocumentDto MapDocument(DemandePaiementPdfData d)
    {
        var statut = StatutDemandePaiement.Normaliser(d.Statut);
        var dateImpression = DateTime.Now;
        return new DemandePaiementDocumentDto(
            d.IdDemandePaiement,
            d.Reference,
            d.DateEmission,
            d.LieuEmission,
            d.Objet,
            d.MontantBrut,
            d.Devise,
            DestinationBudgetaireSolliciteeRules.FormaterDestination(d.TypeBudgetSollicite, d.ItemSollicite),
            d.TypeBudgetSollicite,
            d.ItemSollicite,
            d.ModePaiementSollicite,
            d.CompteSection,
            statut,
            DemandePaiementStatutLabels.Libelle(statut),
            d.LibelleDemandeur,
            d.LibelleCasDossier,
            d.DateSoumission,
            dateImpression,
            $"{d.Reference} · DPM-{d.IdDemandePaiement:D6}",
            d.Beneficiaires.OrderBy(b => b.Ordre).Select(MapBeneficiairePdf).ToList(),
            d.Validations.OrderBy(v => v.Ordre).Select(MapValidationPdf).ToList(),
            [],
            d.DocumentSignePhysiquePresent);
    }

    private static BeneficiaireDto MapBeneficiairePdf(DemandePaiementPdfBeneficiaireRow b)
        => new(
            0,
            b.TypeBeneficiaire,
            b.NomComplet,
            b.Matricule,
            b.Fonction,
            b.RaisonSociale,
            b.Rccm,
            null,
            b.Banque,
            b.NumeroCompte,
            b.EstPrincipal,
            b.Ordre);

    private static ValidationEntiteDto MapValidationPdf(DemandePaiementPdfValidationRow v)
        => new(
            v.Niveau,
            v.Ordre,
            StatutValidationEntite.Normaliser(v.Statut),
            v.ModeValidation,
            null,
            v.NomUtilisateurValidateur,
            null,
            v.NomUtilisateurDeclarant,
            v.NomSignatairePhysique,
            v.FonctionSignatairePhysique,
            v.DateSignaturePhysique,
            v.DateValidation,
            v.Commentaire);

    private async Task<DemandePaiementDocumentDto> MapDocumentAsync(
        DemandePaiementEntity d,
        CancellationToken cancellationToken)
    {
        var perf = DocumentPerfScope.Current;
        var sw = Stopwatch.StartNew();
        var manquantes = await ListerPiecesManquantesInterneAsync(d, cancellationToken);
        if (perf is not null)
            perf.PiecesObligatoiresMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var statut = StatutDemandePaiement.Normaliser(d.Statut);
        var dateImpression = DateTime.Now;

        var dto = new DemandePaiementDocumentDto(
            d.IdDemandePaiement,
            d.Reference,
            d.DateEmission,
            d.LieuEmission,
            d.Objet,
            d.MontantBrut,
            d.Devise,
            DestinationBudgetaireSolliciteeRules.FormaterDestination(d.TypeBudgetSollicite, d.ItemSollicite),
            d.TypeBudgetSollicite,
            d.ItemSollicite,
            d.ModePaiementSollicite,
            d.CompteSection,
            statut,
            DemandePaiementStatutLabels.Libelle(statut),
            d.Demandeur?.Libelle,
            d.CasDossier?.Libelle,
            d.DateSoumission,
            dateImpression,
            $"{d.Reference} · DPM-{d.IdDemandePaiement:D6}",
            d.Beneficiaires.OrderBy(b => b.Ordre).Select(MapBeneficiaire).ToList(),
            d.ValidationsEntite.OrderBy(v => v.Ordre).Select(MapValidation).ToList(),
            ConstruirePiecesDocument(d, manquantes),
            d.PiecesJointes.Any(p =>
                string.Equals(p.CodeTypePiece, TypePieceJointeDpm.DocumentDpmSigne, StringComparison.OrdinalIgnoreCase)));
        if (perf is not null)
            perf.MappingMs = sw.ElapsedMilliseconds;
        return dto;
    }

    private static IReadOnlyList<DemandePaiementPieceDocumentDto> ConstruirePiecesDocument(
        DemandePaiementEntity d,
        IReadOnlyList<PieceManquanteDto> manquantes)
    {
        var rows = d.PiecesJointes
            .OrderBy(p => p.Libelle)
            .Select(p => new DemandePaiementPieceDocumentDto(
                p.Libelle,
                p.EstObligatoire,
                true,
                p.NomFichierOriginal))
            .ToList();

        foreach (var m in manquantes)
        {
            if (rows.Any(r => string.Equals(r.Libelle, m.Libelle, StringComparison.OrdinalIgnoreCase)))
                continue;
            rows.Add(new DemandePaiementPieceDocumentDto(m.Libelle, true, false, null));
        }

        return rows;
    }

    private static void InvaliderValidationsSiDonneesModifiees(DemandePaiementEntity demande)
        => ValidationEntiteRules.InvaliderValidationsSiEmpreinteObsolete(demande);

    private static string ResoudreCircuitEntiteStatut(string? statut)
    {
        return StatutDemandePaiement.Normaliser(statut) switch
        {
            StatutDemandePaiement.Brouillon => "BROUILLON",
            StatutDemandePaiement.EnValidationN1 => "EN ATTENTE N1",
            StatutDemandePaiement.EnValidationN2 => "EN ATTENTE N2",
            StatutDemandePaiement.ValideeEntite => "VALIDÉE ENTITÉ",
            StatutDemandePaiement.Soumise => "SOUMISE AU BUDGET",
            StatutDemandePaiement.ACorriger => "À CORRIGER",
            _ => StatutDemandePaiement.Normaliser(statut),
        };
    }
}
