using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Diagnostics;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Referentiels;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;
using System.Diagnostics;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public async Task<PieceCaisseDto?> GetPieceCaisseAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, cancellationToken);

        if (!EstInstrumentCaisseApplicable(demande))
            return null;

        var piece = demande.PieceCaisse
            ?? await _repository.GetPieceCaisseByDemandeAsync(idDemande, cancellationToken);

        return piece is null ? null : MapPieceCaisse(demande, piece);
    }

    public Task<PieceCaisseDto> EtablirPieceCaisseAsync(
        long idDemande,
        EtablirPieceCaisseRequest request,
        CancellationToken cancellationToken = default)
        => EtablirDocumentInstrumentAsync(
            idDemande,
            TypeInstrumentPaiement.PieceCaisse,
            async (demande, userId, ct) =>
            {
                var perf = EtablissementPerfScope.Current;
                var sw = Stopwatch.StartNew();
                var existing = await _repository.GetPieceCaisseByDemandeAsync(idDemande, ct);
                if (perf is not null)
                {
                    perf.CheckExistingMs = sw.ElapsedMilliseconds;
                    perf.SqlOperations++;
                }

                if (existing is not null)
                {
                    return MapperApresEtablissement(() => MapPieceCaisse(demande, existing));
                }

                sw.Restart();
                DocumentInstrumentPaiementRules.ExigerModeCaisse(
                    demande.ModePaiementSollicite,
                    "Pièce de caisse");
                DocumentInstrumentPaiementRules.ExigerBilletSiRequis(demande);
                DocumentInstrumentPaiementRules.ExigerAucunDocumentInstrumentConflit(
                    demande,
                    TypeInstrumentPaiement.PieceCaisse);
                if (perf is not null)
                    perf.ValidationMetierMs = sw.ElapsedMilliseconds;

                sw.Restart();
                var parametres = await ResoudreParametresAsync(TypeInstrumentPaiement.PieceCaisse, ct);
                if (perf is not null)
                {
                    perf.ParametresMs = sw.ElapsedMilliseconds;
                    perf.SqlOperations++;
                }

                var beneficiaire = ResoudreBeneficiairePrincipal(demande);
                var montantFc = DocumentInstrumentPaiementRules.ResoudreMontantFc(demande);
                var datePiece = request.DatePiece ?? demande.DateEmission;

                sw.Restart();
                var numero = await _repository.GenererNumeroPieceCaisseAsync(
                    (short)datePiece.Year,
                    ct);
                if (perf is not null)
                {
                    perf.GenererNumeroMs = sw.ElapsedMilliseconds;
                    perf.SqlOperations++;
                }

                var now = DateTime.Now;

                var piece = new PieceCaisse
                {
                    FK_DemandePaiement = idDemande,
                    NumeroPiece = numero,
                    DatePiece = datePiece,
                    Statut = StatutDocumentInstrumentPaiement.Etabli,
                    MontantFc = montantFc,
                    MontantEnLettres = MontantFrancaisEnLettres.Convertir(montantFc),
                    ReferenceDemande = demande.Reference,
                    Motif = demande.Objet.Trim(),
                    PieceJustificative = ResoudrePieceJustificative(demande),
                    BeneficiaireAffichage = beneficiaire.Affichage,
                    BeneficiaireMatricule = beneficiaire.Matricule,
                    BeneficiaireIdentite = beneficiaire.Identite,
                    RecuSnel = parametres.RecuInstitutionnel,
                    Sr = parametres.Sr,
                    ComptabiliteGenerale = parametres.ComptabiliteGenerale,
                    Cp = parametres.Cp,
                    Cpa = parametres.Cpa,
                    NumeroAppariement = parametres.NumeroAppariement,
                    IdentifiantVerification = ConstruireIdentifiantVerification(numero, idDemande),
                    FK_UtilisateurEtabli = userId,
                    DateEtabli = now,
                };

                sw.Restart();
                await _repository.AddPieceCaisseAsync(piece, ct);
                if (perf is not null)
                {
                    perf.SaveInstrumentMs = sw.ElapsedMilliseconds;
                    perf.SqlOperations++;
                }

                sw.Restart();
                await _repository.AddAuditAsync(userId, "ETABLIR_PIECE_CAISSE", idDemande,
                    null,
                    new { piece.NumeroPiece, piece.MontantFc, piece.DatePiece },
                    ct);
                if (perf is not null)
                {
                    perf.AuditMs = sw.ElapsedMilliseconds;
                    perf.SqlOperations++;
                }

                demande.PieceCaisse = piece;
                return MapperApresEtablissement(() => MapPieceCaisse(
                    demande,
                    piece,
                    nomUtilisateurEtabliOverride: NomUtilisateurCourantPourMapping()));
            },
            cancellationToken);

    public async Task<byte[]> GenererPieceCaissePdfAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        using var perf = DocumentPerfScope.Begin(_logger, "PIECE-CAISSE-PDF", idDemande);
        ExigerChargeDpm();
        var payload = await BuildPieceCaisseDocumentAsync(idDemande, cancellationToken);

        DocumentInstrumentPerf.Reset();
        var sw = Stopwatch.StartNew();
        var pdf = _pieceCaisseDocumentRenderer.Render(payload);
        perf.QuestPdfMs = sw.ElapsedMilliseconds;
        var (compose, qr) = DocumentInstrumentPerf.Snapshot();
        perf.LogPieceCaissePdf(qr, compose, pdf.Length);
        return pdf;
    }

    public async Task<BonProvisoireDto?> GetBonProvisoireAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, cancellationToken);

        if (!EstInstrumentCaisseApplicable(demande))
            return null;

        var bon = demande.BonProvisoire
            ?? await _repository.GetBonProvisoireByDemandeAsync(idDemande, cancellationToken);

        return bon is null ? null : MapBonProvisoire(demande, bon);
    }

    public Task<BonProvisoireDto> EtablirBonProvisoireAsync(
        long idDemande,
        EtablirBonProvisoireRequest request,
        CancellationToken cancellationToken = default)
        => EtablirDocumentInstrumentAsync(
            idDemande,
            TypeInstrumentPaiement.BonProvisoire,
            async (demande, userId, ct) =>
            {
                var existing = await _repository.GetBonProvisoireByDemandeAsync(idDemande, ct);
                if (existing is not null)
                {
                    return MapperApresEtablissement(() => MapBonProvisoire(demande, existing));
                }

                DocumentInstrumentPaiementRules.ExigerModeCaisse(
                    demande.ModePaiementSollicite,
                    "Bon provisoire");
                DocumentInstrumentPaiementRules.ExigerBilletSiRequis(demande);
                DocumentInstrumentPaiementRules.ExigerAucunDocumentInstrumentConflit(
                    demande,
                    TypeInstrumentPaiement.BonProvisoire);

                var parametres = await ResoudreParametresAsync(TypeInstrumentPaiement.BonProvisoire, ct);
                var beneficiaire = ResoudreBeneficiairePrincipal(demande);
                var montantFc = DocumentInstrumentPaiementRules.ResoudreMontantFc(demande);
                var dateBon = request.DateBon ?? demande.DateEmission;
                var numero = await _repository.GenererNumeroBonProvisoireAsync(
                    (short)dateBon.Year,
                    ct);
                var now = DateTime.Now;

                var bon = new BonProvisoire
                {
                    FK_DemandePaiement = idDemande,
                    NumeroBon = numero,
                    DateBon = dateBon,
                    Statut = StatutDocumentInstrumentPaiement.Etabli,
                    MontantFc = montantFc,
                    MontantEnLettres = MontantFrancaisEnLettres.Convertir(montantFc),
                    ReferenceDemande = demande.Reference,
                    Motif = demande.Objet.Trim(),
                    BeneficiaireAffichage = beneficiaire.Affichage,
                    BeneficiaireMatricule = beneficiaire.Matricule,
                    BeneficiaireIdentite = beneficiaire.Identite,
                    DirectionBeneficiaire = beneficiaire.Fonction,
                    RecuCaisseCentrale = parametres.RecuInstitutionnel,
                    CompteGeneral = parametres.CompteGeneral,
                    CompteParticulier = parametres.CompteParticulier,
                    NumeroAppariement = parametres.NumeroAppariement,
                    IdentifiantVerification = ConstruireIdentifiantVerification(numero, idDemande),
                    FK_UtilisateurEtabli = userId,
                    DateEtabli = now,
                };

                await _repository.AddBonProvisoireAsync(bon, ct);
                await _repository.AddAuditAsync(userId, "ETABLIR_BON_PROVISOIRE", idDemande,
                    null,
                    new { bon.NumeroBon, bon.MontantFc, bon.DateBon },
                    ct);

                demande.BonProvisoire = bon;
                return MapperApresEtablissement(() => MapBonProvisoire(
                    demande,
                    bon,
                    nomUtilisateurEtabliOverride: NomUtilisateurCourantPourMapping()));
            },
            cancellationToken);

    public async Task<byte[]> GenererBonProvisoirePdfAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        using var perf = DocumentPerfScope.Begin(_logger, "BON-PROVISOIRE-PDF", idDemande);
        ExigerChargeDpm();
        var payload = await BuildBonProvisoireDocumentAsync(idDemande, cancellationToken);

        DocumentInstrumentPerf.Reset();
        var sw = Stopwatch.StartNew();
        var pdf = _bonProvisoireDocumentRenderer.Render(payload);
        perf.QuestPdfMs = sw.ElapsedMilliseconds;
        var (compose, qr) = DocumentInstrumentPerf.Snapshot();
        perf.LogPdfPipeline("BON-PROVISOIRE-PDF", qr, compose, pdf.Length, "GetBonProvisoirePdfData");
        return pdf;
    }

    public async Task<MinuteChequeDto?> GetMinuteChequeAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, cancellationToken);

        if (!EstInstrumentBanqueApplicable(demande))
            return null;

        var minute = demande.MinuteCheque
            ?? await _repository.GetMinuteChequeByDemandeAsync(idDemande, cancellationToken);

        return minute is null ? null : MapMinuteCheque(demande, minute);
    }

    public Task<MinuteChequeDto> EtablirMinuteChequeAsync(
        long idDemande,
        EtablirMinuteChequeRequest request,
        CancellationToken cancellationToken = default)
        => EtablirDocumentInstrumentAsync(
            idDemande,
            TypeInstrumentPaiement.MinuteCheque,
            async (demande, userId, ct) =>
            {
                var existing = await _repository.GetMinuteChequeByDemandeAsync(idDemande, ct);
                if (existing is not null)
                {
                    return MapperApresEtablissement(() => MapMinuteCheque(demande, existing));
                }

                DocumentInstrumentPaiementRules.ExigerModeBanque(
                    demande.ModePaiementSollicite,
                    "Minute de chèque");
                DocumentInstrumentPaiementRules.ExigerAucunDocumentInstrumentConflit(
                    demande,
                    TypeInstrumentPaiement.MinuteCheque);

                var parametres = await ResoudreParametresAsync(TypeInstrumentPaiement.MinuteCheque, ct);
                var beneficiaire = ResoudreBeneficiairePrincipal(demande);
                var (montant, devise) = DocumentInstrumentPaiementRules.ResoudreMontantPaiementBanque(demande);
                var dateDocument = request.DateDocument ?? demande.DateEmission;
                var numero = await _repository.GenererNumeroMinuteChequeAsync(
                    (short)dateDocument.Year,
                    ct);
                var now = DateTime.Now;
                var deviseLibelle = LibelleDeviseEnLettres(devise);

                var minute = new MinuteCheque
                {
                    FK_DemandePaiement = idDemande,
                    NumeroOp = numero,
                    DateDocument = dateDocument,
                    Statut = StatutDocumentInstrumentPaiement.Etabli,
                    MontantPaiement = montant,
                    DevisePaiement = devise,
                    MontantEnLettres = MontantFrancaisEnLettres.Convertir(montant, deviseLibelle),
                    ReferenceDemande = demande.Reference,
                    Motif = demande.Objet.Trim(),
                    BeneficiaireAffichage = beneficiaire.Affichage,
                    BeneficiaireAdresse = beneficiaire.Adresse,
                    BeneficiaireBanque = beneficiaire.Banque,
                    BeneficiaireNumeroCompte = beneficiaire.NumeroCompte,
                    CompteGeneral = parametres.CompteGeneral,
                    CpCa = parametres.CpCa,
                    Ls = parametres.Ls,
                    SuiviExtraComptable = parametres.SuiviExtraComptable,
                    NumeroAppariement = parametres.NumeroAppariement,
                    MontantSuiviExtraComptable = parametres.MontantSuiviExtraComptable,
                    IdentifiantVerification = ConstruireIdentifiantVerification(numero, idDemande),
                    FK_UtilisateurEtabli = userId,
                    DateEtabli = now,
                };

                await _repository.AddMinuteChequeAsync(minute, ct);
                await _repository.AddAuditAsync(userId, "ETABLIR_MINUTE_CHEQUE", idDemande,
                    null,
                    new { minute.NumeroOp, minute.MontantPaiement, minute.DevisePaiement, minute.DateDocument },
                    ct);

                demande.MinuteCheque = minute;
                return MapperApresEtablissement(() => MapMinuteCheque(
                    demande,
                    minute,
                    nomUtilisateurEtabliOverride: NomUtilisateurCourantPourMapping()));
            },
            cancellationToken);

    public async Task<byte[]> GenererMinuteChequePdfAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        using var perf = DocumentPerfScope.Begin(_logger, "MINUTE-CHEQUE-PDF", idDemande);
        ExigerChargeDpm();
        var payload = await BuildMinuteChequeDocumentAsync(idDemande, cancellationToken);

        DocumentInstrumentPerf.Reset();
        var sw = Stopwatch.StartNew();
        var pdf = _minuteChequeDocumentRenderer.Render(payload);
        perf.QuestPdfMs = sw.ElapsedMilliseconds;
        var (compose, qr) = DocumentInstrumentPerf.Snapshot();
        perf.LogPdfPipeline("MINUTE-CHEQUE-PDF", qr, compose, pdf.Length, "GetMinuteChequePdfData");
        return pdf;
    }

    private Task<TDto> EtablirDocumentInstrumentAsync<TDto>(
        long idDemande,
        string typeInstrument,
        Func<DemandePaiementEntity, long, CancellationToken, Task<TDto>> etablir,
        CancellationToken cancellationToken)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            using var perfScope = EtablissementPerfScope.Begin(_logger, typeInstrument, idDemande);
            var sw = Stopwatch.StartNew();
            ExigerChargeDpm();
            perfScope.PermissionMs = sw.ElapsedMilliseconds;

            var userId = _currentUser.RequireUserId();

            sw.Restart();
            var demande = await _repository.GetTrackedAsync(idDemande, ct)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");
            perfScope.GetTrackedMs = sw.ElapsedMilliseconds;
            perfScope.SqlOperations++;

            sw.Restart();
            await GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Traiter, ct);
            perfScope.AccessMs = sw.ElapsedMilliseconds;

            sw.Restart();
            ExigerStatut(
                demande,
                StatutDemandePaiement.EnTraitementDpm,
                $"La demande doit être en traitement DPM pour établir le document {typeInstrument}.");

            var mode = ModePaiementDpm.Normaliser(demande.ModePaiementSollicite);
            TypeInstrumentPaiement.ExigerCoherence(mode, typeInstrument);
            perfScope.StatutCoherenceMs = sw.ElapsedMilliseconds;

            if (demande.BilletConversion is null && BilletConversionRules.NecessiteBillet(mode, demande.Devise))
            {
                sw.Restart();
                demande.BilletConversion = await _repository.GetBilletConversionByDemandeAsync(idDemande, ct);
                perfScope.BilletConversionLoadMs = sw.ElapsedMilliseconds;
                perfScope.SqlOperations++;
            }

            var result = await etablir(demande, userId, ct);
            perfScope.LogTotal();
            return result;
        }, cancellationToken);

    private string? NomUtilisateurCourantPourMapping()
        => !string.IsNullOrWhiteSpace(_currentUser.DisplayName)
            ? _currentUser.DisplayName.Trim()
            : string.IsNullOrWhiteSpace(_currentUser.Username)
                ? null
                : _currentUser.Username.Trim();

    private static T MapperApresEtablissement<T>(Func<T> map)
    {
        var perf = EtablissementPerfScope.Current;
        var sw = Stopwatch.StartNew();
        var result = map();
        if (perf is not null)
            perf.MappingMs = sw.ElapsedMilliseconds;
        return result;
    }

    private async Task ExigerDocumentInstrumentEtabliAsync(
        long idDemande,
        string instrument,
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
    {
        if (!DocumentInstrumentPaiementRules.NecessiteDocumentInstrument(demande.ModePaiementSollicite))
            return;

        var inst = TypeInstrumentPaiement.Normaliser(instrument);
        var etabli = inst switch
        {
            TypeInstrumentPaiement.PieceCaisse =>
                EstDocumentEtabli(demande.PieceCaisse)
                || EstDocumentEtabli(await _repository.GetPieceCaisseByDemandeAsync(idDemande, cancellationToken)),
            TypeInstrumentPaiement.BonProvisoire =>
                EstDocumentEtabli(demande.BonProvisoire)
                || EstDocumentEtabli(await _repository.GetBonProvisoireByDemandeAsync(idDemande, cancellationToken)),
            TypeInstrumentPaiement.MinuteCheque =>
                EstDocumentEtabli(demande.MinuteCheque)
                || EstDocumentEtabli(await _repository.GetMinuteChequeByDemandeAsync(idDemande, cancellationToken)),
            _ => false,
        };

        if (!etabli)
        {
            throw new InvalidOperationException(
                "Le document instrument de paiement doit être établi avant de poursuivre le traitement.");
        }
    }

    private static bool EstDocumentEtabli(PieceCaisse? doc)
        => doc is not null
           && string.Equals(
               StatutDocumentInstrumentPaiement.Normaliser(doc.Statut),
               StatutDocumentInstrumentPaiement.Etabli,
               StringComparison.Ordinal);

    private static bool EstDocumentEtabli(BonProvisoire? doc)
        => doc is not null
           && string.Equals(
               StatutDocumentInstrumentPaiement.Normaliser(doc.Statut),
               StatutDocumentInstrumentPaiement.Etabli,
               StringComparison.Ordinal);

    private static bool EstDocumentEtabli(MinuteCheque? doc)
        => doc is not null
           && string.Equals(
               StatutDocumentInstrumentPaiement.Normaliser(doc.Statut),
               StatutDocumentInstrumentPaiement.Etabli,
               StringComparison.Ordinal);

    private static bool EstInstrumentCaisseApplicable(DemandePaiementEntity demande)
        => EstInstrumentCaisseApplicable(demande.ModePaiementSollicite);

    private static bool EstInstrumentCaisseApplicable(string? modePaiementSollicite)
        => string.Equals(
            ModePaiementDpm.Normaliser(modePaiementSollicite),
            ModePaiementDpm.Caisse,
            StringComparison.Ordinal);

    private static bool EstInstrumentBanqueApplicable(DemandePaiementEntity demande)
        => EstInstrumentBanqueApplicable(demande.ModePaiementSollicite);

    private static bool EstInstrumentBanqueApplicable(string? modePaiementSollicite)
        => string.Equals(
            ModePaiementDpm.Normaliser(modePaiementSollicite),
            ModePaiementDpm.Banque,
            StringComparison.Ordinal);

    private async Task<ParametreInstrumentPaiement> ResoudreParametresAsync(
        string typeInstrument,
        CancellationToken cancellationToken)
    {
        var type = TypeInstrumentPaiement.Normaliser(typeInstrument);
        var db = await _repository.GetParametreInstrumentAsync(type, cancellationToken);
        return ParametreInstrumentPaiementRules.ExigerParametreComplet(db, type);
    }

    private static BeneficiaireInstrumentSnapshot ResoudreBeneficiairePrincipal(DemandePaiementEntity demande)
    {
        var principal = demande.Beneficiaires
            .FirstOrDefault(b => b.EstPrincipal)
            ?? demande.Beneficiaires.OrderBy(b => b.Ordre).FirstOrDefault();

        if (principal is null)
        {
            return new BeneficiaireInstrumentSnapshot("—", null, null, null, null, null, null);
        }

        var affichage = !string.IsNullOrWhiteSpace(principal.RaisonSociale)
            ? principal.RaisonSociale.Trim()
            : principal.NomComplet.Trim();

        return new BeneficiaireInstrumentSnapshot(
            affichage,
            principal.Matricule?.Trim(),
            principal.NomComplet.Trim(),
            principal.Fonction?.Trim(),
            principal.Adresse?.Trim(),
            principal.Banque?.Trim(),
            principal.NumeroCompte?.Trim());
    }

    private static string? ResoudrePieceJustificative(DemandePaiementEntity demande)
    {
        var pieces = demande.PiecesJointes
            .OrderBy(p => p.DateUpload)
            .Select(p => string.IsNullOrWhiteSpace(p.Libelle) ? p.NomFichierOriginal : p.Libelle.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return pieces.Count == 0 ? null : string.Join(" ; ", pieces);
    }

    private static string ConstruireIdentifiantVerification(string numeroDocument, long idDemande)
        => $"{numeroDocument} · DPM-{idDemande:D6}";

    private static string LibelleDeviseEnLettres(string devise)
    {
        var code = DemandePaiementMontants.NormaliserCodeDevise(devise);
        return string.Equals(code, TauxChangeConventions.DeviseCdf, StringComparison.OrdinalIgnoreCase)
            ? "francs congolais"
            : code.ToLowerInvariant();
    }

    private async Task<PieceCaisseDocumentDto> BuildPieceCaisseDocumentAsync(
        long idDemande,
        CancellationToken cancellationToken)
    {
        var perf = DocumentPerfScope.Current;
        var sw = Stopwatch.StartNew();
        var contexte = await _repository.GetDemandeAccesContextAsync(idDemande, cancellationToken);
        if (perf is not null)
            perf.GetDemandeAccessMs = sw.ElapsedMilliseconds;

        if (contexte is null)
            throw new KeyNotFoundException("Demande de paiement introuvable.");

        sw.Restart();
        await GarantirAccesDemandeAsync(contexte, cancellationToken);
        if (perf is not null)
            perf.AccessMs = sw.ElapsedMilliseconds;

        if (!EstInstrumentCaisseApplicable(contexte.ModePaiementSollicite))
        {
            throw new InvalidOperationException(
                "Aucune pièce de caisse n'est disponible pour cette demande.");
        }

        sw.Restart();
        var pdfData = await _repository.GetPieceCaissePdfDataAsync(idDemande, cancellationToken);
        if (perf is not null)
        {
            perf.GetPieceMs = sw.ElapsedMilliseconds;
            perf.GetPdfDataMs = sw.ElapsedMilliseconds;
        }

        if (pdfData is null)
        {
            throw new InvalidOperationException(
                "La pièce de caisse n'a pas encore été établie pour cette demande.");
        }

        sw.Restart();
        var dto = MapPieceCaisseDocument(pdfData);
        if (perf is not null)
            perf.MappingMs = sw.ElapsedMilliseconds;
        return dto;
    }

    private async Task<BonProvisoireDocumentDto> BuildBonProvisoireDocumentAsync(
        long idDemande,
        CancellationToken cancellationToken)
    {
        var perf = DocumentPerfScope.Current;
        var sw = Stopwatch.StartNew();
        var contexte = await _repository.GetDemandeAccesContextAsync(idDemande, cancellationToken);
        if (perf is not null)
            perf.GetDemandeAccessMs = sw.ElapsedMilliseconds;

        if (contexte is null)
            throw new KeyNotFoundException("Demande de paiement introuvable.");

        sw.Restart();
        await GarantirAccesDemandeAsync(contexte, cancellationToken);
        if (perf is not null)
            perf.AccessMs = sw.ElapsedMilliseconds;

        if (!EstInstrumentCaisseApplicable(contexte.ModePaiementSollicite))
        {
            throw new InvalidOperationException(
                "Aucun bon provisoire n'est disponible pour cette demande.");
        }

        sw.Restart();
        var pdfData = await _repository.GetBonProvisoirePdfDataAsync(idDemande, cancellationToken);
        if (perf is not null)
            perf.GetPdfDataMs = sw.ElapsedMilliseconds;

        if (pdfData is null)
        {
            throw new InvalidOperationException(
                "Le bon provisoire n'a pas encore été établi pour cette demande.");
        }

        sw.Restart();
        var dto = MapBonProvisoireDocument(pdfData);
        if (perf is not null)
            perf.MappingMs = sw.ElapsedMilliseconds;
        return dto;
    }

    private async Task<MinuteChequeDocumentDto> BuildMinuteChequeDocumentAsync(
        long idDemande,
        CancellationToken cancellationToken)
    {
        var perf = DocumentPerfScope.Current;
        var sw = Stopwatch.StartNew();
        var contexte = await _repository.GetDemandeAccesContextAsync(idDemande, cancellationToken);
        if (perf is not null)
            perf.GetDemandeAccessMs = sw.ElapsedMilliseconds;

        if (contexte is null)
            throw new KeyNotFoundException("Demande de paiement introuvable.");

        sw.Restart();
        await GarantirAccesDemandeAsync(contexte, cancellationToken);
        if (perf is not null)
            perf.AccessMs = sw.ElapsedMilliseconds;

        if (!EstInstrumentBanqueApplicable(contexte.ModePaiementSollicite))
        {
            throw new InvalidOperationException(
                "Aucune minute de chèque n'est disponible pour cette demande.");
        }

        sw.Restart();
        var pdfData = await _repository.GetMinuteChequePdfDataAsync(idDemande, cancellationToken);
        if (perf is not null)
            perf.GetPdfDataMs = sw.ElapsedMilliseconds;

        if (pdfData is null)
        {
            throw new InvalidOperationException(
                "La minute de chèque n'a pas encore été établie pour cette demande.");
        }

        sw.Restart();
        var dto = MapMinuteChequeDocument(pdfData);
        if (perf is not null)
            perf.MappingMs = sw.ElapsedMilliseconds;
        return dto;
    }

    private static byte[] RenderInstrumentPdf<T>(
        DocumentPerfScope perf,
        string tag,
        T payload,
        Func<T, byte[]> render)
    {
        DocumentInstrumentPerf.Reset();
        var sw = Stopwatch.StartNew();
        var pdf = render(payload);
        perf.QuestPdfMs = sw.ElapsedMilliseconds;
        var (compose, qr) = DocumentInstrumentPerf.Snapshot();
        perf.LogInstrumentPdf(tag, qr, compose, pdf.Length);
        return pdf;
    }

    private static PieceCaisseDto MapPieceCaisse(
        DemandePaiementEntity demande,
        PieceCaisse piece,
        string? nomUtilisateurEtabliOverride = null)
        => new(
            piece.IdPieceCaisse,
            demande.IdDemandePaiement,
            StatutDocumentInstrumentPaiement.Normaliser(piece.Statut),
            piece.NumeroPiece,
            piece.DatePiece,
            piece.MontantFc,
            piece.MontantEnLettres,
            piece.ReferenceDemande,
            piece.Motif,
            piece.PieceJustificative,
            piece.BeneficiaireAffichage,
            piece.BeneficiaireMatricule,
            piece.BeneficiaireIdentite,
            piece.RecuSnel,
            piece.Sr,
            piece.ComptabiliteGenerale,
            piece.Cp,
            piece.Cpa,
            piece.NumeroAppariement,
            piece.IdentifiantVerification,
            piece.FK_UtilisateurEtabli,
            nomUtilisateurEtabliOverride ?? FormatNomUtilisateur(piece.UtilisateurEtabli),
            piece.DateEtabli);

    private static BonProvisoireDto MapBonProvisoire(
        DemandePaiementEntity demande,
        BonProvisoire bon,
        string? nomUtilisateurEtabliOverride = null)
        => new(
            bon.IdBonProvisoire,
            demande.IdDemandePaiement,
            StatutDocumentInstrumentPaiement.Normaliser(bon.Statut),
            bon.NumeroBon,
            bon.DateBon,
            bon.MontantFc,
            bon.MontantEnLettres,
            bon.ReferenceDemande,
            bon.Motif,
            bon.MentionJustificationRetrait,
            bon.BeneficiaireAffichage,
            bon.BeneficiaireMatricule,
            bon.BeneficiaireIdentite,
            bon.DirectionBeneficiaire,
            bon.RecuCaisseCentrale,
            bon.CompteGeneral,
            bon.CompteParticulier,
            bon.NumeroAppariement,
            bon.IdentifiantVerification,
            bon.FK_UtilisateurEtabli,
            nomUtilisateurEtabliOverride ?? FormatNomUtilisateur(bon.UtilisateurEtabli),
            bon.DateEtabli);

    private static MinuteChequeDto MapMinuteCheque(
        DemandePaiementEntity demande,
        MinuteCheque minute,
        string? nomUtilisateurEtabliOverride = null)
        => new(
            minute.IdMinuteCheque,
            demande.IdDemandePaiement,
            StatutDocumentInstrumentPaiement.Normaliser(minute.Statut),
            minute.NumeroOp,
            minute.DateDocument,
            minute.MontantPaiement,
            minute.DevisePaiement,
            minute.MontantEnLettres,
            minute.ReferenceDemande,
            minute.Motif,
            minute.BeneficiaireAffichage,
            minute.BeneficiaireAdresse,
            minute.BeneficiaireBanque,
            minute.BeneficiaireNumeroCompte,
            minute.CompteGeneral,
            minute.CpCa,
            minute.Ls,
            minute.SuiviExtraComptable,
            minute.NumeroAppariement,
            minute.MontantSuiviExtraComptable,
            minute.IdentifiantVerification,
            minute.FK_UtilisateurEtabli,
            nomUtilisateurEtabliOverride ?? FormatNomUtilisateur(minute.UtilisateurEtabli),
            minute.DateEtabli);

    private static PieceCaisseDocumentDto MapPieceCaisseDocument(PieceCaisse piece)
        => MapPieceCaisseDocument(MapPieceCaisseToPdfData(piece));

    private static PieceCaisseDocumentDto MapPieceCaisseDocument(PieceCaissePdfData data)
        => new(
            "PIÈCE DE CAISSE",
            data.NumeroPiece,
            data.DatePiece,
            data.ReferenceDemande,
            data.Motif,
            data.PieceJustificative,
            data.BeneficiaireAffichage,
            data.BeneficiaireMatricule,
            data.BeneficiaireIdentite,
            data.MontantFc,
            data.MontantEnLettres,
            data.RecuSnel,
            data.Sr,
            data.ComptabiliteGenerale,
            data.Cp,
            data.Cpa,
            data.NumeroAppariement,
            data.IdentifiantVerification,
            PdfUserDisplayNameFormatting.Format(data.UtilisateurEtabli),
            DateTime.Now);

    private static PieceCaissePdfData MapPieceCaisseToPdfData(PieceCaisse piece)
        => new(
            piece.NumeroPiece,
            piece.DatePiece,
            piece.ReferenceDemande,
            piece.Motif,
            piece.PieceJustificative,
            piece.BeneficiaireAffichage,
            piece.BeneficiaireMatricule,
            piece.BeneficiaireIdentite,
            piece.MontantFc,
            piece.MontantEnLettres,
            piece.RecuSnel,
            piece.Sr,
            piece.ComptabiliteGenerale,
            piece.Cp,
            piece.Cpa,
            piece.NumeroAppariement,
            piece.IdentifiantVerification,
            piece.UtilisateurEtabli is null
                ? null
                : new PdfUserDisplayName(
                    piece.UtilisateurEtabli.Nom,
                    piece.UtilisateurEtabli.Prenom,
                    piece.UtilisateurEtabli.NomUtilisateur));

    private static BonProvisoireDocumentDto MapBonProvisoireDocument(BonProvisoire bon)
        => MapBonProvisoireDocument(MapBonProvisoireToPdfData(bon));

    private static BonProvisoireDocumentDto MapBonProvisoireDocument(BonProvisoirePdfData data)
        => new(
            "BON PROVISOIRE",
            data.NumeroBon,
            data.DateBon,
            data.ReferenceDemande,
            data.Motif,
            data.MentionJustificationRetrait,
            data.BeneficiaireAffichage,
            data.BeneficiaireMatricule,
            data.BeneficiaireIdentite,
            data.DirectionBeneficiaire,
            data.MontantFc,
            data.MontantEnLettres,
            data.RecuCaisseCentrale,
            data.CompteGeneral,
            data.CompteParticulier,
            data.NumeroAppariement,
            data.IdentifiantVerification,
            PdfUserDisplayNameFormatting.Format(data.UtilisateurEtabli),
            DateTime.Now);

    private static BonProvisoirePdfData MapBonProvisoireToPdfData(BonProvisoire bon)
        => new(
            bon.NumeroBon,
            bon.DateBon,
            bon.ReferenceDemande,
            bon.Motif,
            bon.MentionJustificationRetrait,
            bon.BeneficiaireAffichage,
            bon.BeneficiaireMatricule,
            bon.BeneficiaireIdentite,
            bon.DirectionBeneficiaire,
            bon.MontantFc,
            bon.MontantEnLettres,
            bon.RecuCaisseCentrale,
            bon.CompteGeneral,
            bon.CompteParticulier,
            bon.NumeroAppariement,
            bon.IdentifiantVerification,
            bon.UtilisateurEtabli is null
                ? null
                : new PdfUserDisplayName(
                    bon.UtilisateurEtabli.Nom,
                    bon.UtilisateurEtabli.Prenom,
                    bon.UtilisateurEtabli.NomUtilisateur));

    private static MinuteChequeDocumentDto MapMinuteChequeDocument(MinuteCheque minute)
        => MapMinuteChequeDocument(MapMinuteChequeToPdfData(minute));

    private static MinuteChequeDocumentDto MapMinuteChequeDocument(MinuteChequePdfData data)
        => new(
            "MINUTE DE CHÈQUE",
            data.NumeroOp,
            data.DateDocument,
            data.ReferenceDemande,
            data.Motif,
            data.BeneficiaireAffichage,
            data.BeneficiaireAdresse,
            data.BeneficiaireBanque,
            data.BeneficiaireNumeroCompte,
            data.MontantPaiement,
            data.DevisePaiement,
            data.MontantEnLettres,
            data.CompteGeneral,
            data.CpCa,
            data.Ls,
            data.SuiviExtraComptable,
            data.NumeroAppariement,
            data.MontantSuiviExtraComptable,
            data.IdentifiantVerification,
            PdfUserDisplayNameFormatting.Format(data.UtilisateurEtabli),
            DateTime.Now);

    private static MinuteChequePdfData MapMinuteChequeToPdfData(MinuteCheque minute)
        => new(
            minute.NumeroOp,
            minute.DateDocument,
            minute.ReferenceDemande,
            minute.Motif,
            minute.BeneficiaireAffichage,
            minute.BeneficiaireAdresse,
            minute.BeneficiaireBanque,
            minute.BeneficiaireNumeroCompte,
            minute.MontantPaiement,
            minute.DevisePaiement,
            minute.MontantEnLettres,
            minute.CompteGeneral,
            minute.CpCa,
            minute.Ls,
            minute.SuiviExtraComptable,
            minute.NumeroAppariement,
            minute.MontantSuiviExtraComptable,
            minute.IdentifiantVerification,
            minute.UtilisateurEtabli is null
                ? null
                : new PdfUserDisplayName(
                    minute.UtilisateurEtabli.Nom,
                    minute.UtilisateurEtabli.Prenom,
                    minute.UtilisateurEtabli.NomUtilisateur));

    private sealed record BeneficiaireInstrumentSnapshot(
        string Affichage,
        string? Matricule,
        string? Identite,
        string? Fonction,
        string? Adresse,
        string? Banque,
        string? NumeroCompte);
}
