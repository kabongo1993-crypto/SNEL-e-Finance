using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Diagnostics;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using System.Diagnostics;
using BudgetWeb.Domain.Referentiels;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public async Task<BilletConversionDto?> GetBilletConversionAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var demande = await _repository.GetDetailAsync(idDemande, cancellationToken)
            ?? throw new InvalidOperationException("Demande de paiement introuvable.");

        await GarantirAccesDemandeAsync(demande, cancellationToken);

        if (!BilletConversionRules.NecessiteBillet(demande.ModePaiementSollicite, demande.Devise))
            return null;

        var billet = demande.BilletConversion
            ?? await _repository.GetBilletConversionByDemandeAsync(idDemande, cancellationToken);

        return billet is null ? null : MapBilletConversion(demande, billet);
    }

    public Task<BilletConversionDto> EtablirBilletConversionAsync(
        long idDemande,
        EtablirBilletConversionRequest request,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteInTransactionAsync(async ct =>
        {
            using var perfScope = EtablissementPerfScope.Begin(_logger, "BILLET_CONVERSION", idDemande);
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
                "La demande doit être en traitement DPM pour établir le billet de conversion.");
            perfScope.StatutCoherenceMs = sw.ElapsedMilliseconds;

            if (!BilletConversionRules.NecessiteBillet(demande.ModePaiementSollicite, demande.Devise))
            {
                var mode = ModePaiementDpm.Normaliser(demande.ModePaiementSollicite);
                if (string.Equals(mode, ModePaiementDpm.Banque, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Le billet de conversion n'est pas applicable en mode BANQUE.");
                }

                throw new InvalidOperationException(
                    "Aucun billet de conversion n'est requis pour cette demande.");
            }

            sw.Restart();
            var existing = await _repository.GetBilletConversionByDemandeAsync(idDemande, ct);
            perfScope.CheckExistingMs = sw.ElapsedMilliseconds;
            perfScope.SqlOperations++;

            if (existing is not null)
            {
                var dto = MapperApresEtablissement(() => MapBilletConversion(demande, existing));
                perfScope.LogTotal();
                return dto;
            }

            var dateConversion = DateTraitementDpm();
            var deviseOrigine = DemandePaiementMontants.NormaliserCodeDevise(demande.Devise);
            var montantOrigine = demande.MontantBrut;

            sw.Restart();
            var conversion = await _tauxChange.ConvertirAsync(
                montantOrigine,
                deviseOrigine,
                TauxChangeConventions.DeviseCdf,
                dateConversion,
                ct);
            perfScope.TauxConversionMs = sw.ElapsedMilliseconds;
            perfScope.SqlOperations++;

            var now = DateTime.Now;
            var billet = new BilletConversion
            {
                FK_DemandePaiement = idDemande,
                DateConversion = dateConversion,
                DeviseOrigine = deviseOrigine,
                MontantDeviseOrigine = montantOrigine,
                TauxApplique = conversion.TauxApplique,
                MontantCdf = conversion.MontantCible,
                FK_TauxChange = conversion.IdTauxChange,
                DemandeChequeNumero = string.IsNullOrWhiteSpace(request.DemandeChequeNumero)
                    ? null
                    : request.DemandeChequeNumero.Trim(),
                CoursEchangeBanque = string.IsNullOrWhiteSpace(request.CoursEchangeBanque)
                    ? null
                    : request.CoursEchangeBanque.Trim(),
                SoldeAPayerDevise = request.SoldeAPayerDevise,
                Statut = StatutBilletConversion.Etabli,
                FK_UtilisateurEtabli = userId,
                DateEtabli = now,
            };

            sw.Restart();
            await _repository.AddBilletConversionAsync(billet, ct);
            perfScope.SaveInstrumentMs = sw.ElapsedMilliseconds;
            perfScope.SqlOperations++;

            sw.Restart();
            await _repository.AddAuditAsync(userId, "ETABLIR_BILLET_CONVERSION", idDemande,
                null,
                new
                {
                    billet.DateConversion,
                    billet.DeviseOrigine,
                    billet.MontantDeviseOrigine,
                    billet.TauxApplique,
                    billet.MontantCdf,
                    billet.FK_TauxChange,
                },
                ct);
            perfScope.AuditMs = sw.ElapsedMilliseconds;
            perfScope.SqlOperations++;

            demande.BilletConversion = billet;
            var result = MapperApresEtablissement(() => MapBilletConversion(
                demande,
                billet,
                nomUtilisateurEtabliOverride: NomUtilisateurCourantPourMapping()));
            perfScope.LogTotal();
            return result;
        }, cancellationToken);

    public async Task<byte[]> GenererBilletConversionPdfAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        using var perf = DocumentPerfScope.Begin(_logger, "BILLET-CONVERSION-PDF", idDemande);
        ExigerChargeDpm();
        var payload = await BuildBilletConversionDocumentAsync(idDemande, cancellationToken);
        var sw = Stopwatch.StartNew();
        var pdf = _billetDocumentRenderer.Render(payload);
        perf.QuestPdfMs = sw.ElapsedMilliseconds;
        perf.LogBilletPdf(pdf.Length);
        return pdf;
    }

    private async Task<BilletConversionDocumentDto> BuildBilletConversionDocumentAsync(
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

        if (!BilletConversionRules.NecessiteBillet(contexte.ModePaiementSollicite, contexte.Devise))
        {
            throw new InvalidOperationException(
                "Aucun billet de conversion n'est disponible pour cette demande.");
        }

        sw.Restart();
        var pdfData = await _repository.GetBilletConversionPdfDataAsync(idDemande, cancellationToken);
        if (perf is not null)
            perf.GetPdfDataMs = sw.ElapsedMilliseconds;

        if (pdfData is null)
        {
            throw new InvalidOperationException(
                "Le billet de conversion n'a pas encore été établi pour cette demande.");
        }

        sw.Restart();
        var dto = MapBilletDocument(pdfData);
        if (perf is not null)
            perf.MappingMs = sw.ElapsedMilliseconds;
        return dto;
    }

    private static BilletConversionDto MapBilletConversion(
        DemandePaiementEntity demande,
        BilletConversion billet,
        string? nomUtilisateurEtabliOverride = null)
        => new(
            billet.IdBilletConversion,
            demande.IdDemandePaiement,
            StatutBilletConversion.Normaliser(billet.Statut),
            billet.DateConversion,
            billet.DeviseOrigine,
            billet.MontantDeviseOrigine,
            billet.TauxApplique,
            billet.MontantCdf,
            billet.FK_TauxChange,
            billet.DemandeChequeNumero,
            billet.CoursEchangeBanque,
            billet.SoldeAPayerDevise,
            billet.FK_UtilisateurEtabli,
            nomUtilisateurEtabliOverride ?? FormatNomUtilisateur(billet.UtilisateurEtabli),
            billet.DateEtabli,
            FormatNomUtilisateur(billet.UtilisateurApprouve),
            FormatNomUtilisateur(billet.UtilisateurVisa),
            demande.Reference,
            ResoudreBeneficiaireAffichage(demande));

    private static BilletConversionDocumentDto MapBilletDocument(
        DemandePaiementEntity demande,
        BilletConversion billet)
        => MapBilletDocument(MapBilletConversionToPdfData(demande, billet));

    private static BilletConversionDocumentDto MapBilletDocument(BilletConversionPdfData data)
        => new(
            data.IdDemandePaiement,
            data.ReferenceDemande,
            PdfBeneficiairePrincipalFormatting.ResoudreAffichage(data.BeneficiairePrincipal),
            data.MontantDeviseOrigine,
            data.DeviseOrigine,
            data.TauxApplique,
            data.DateConversion,
            data.DemandeChequeNumero,
            data.CoursEchangeBanque,
            data.MontantCdf,
            data.SoldeAPayerDevise,
            PdfUserDisplayNameFormatting.Format(data.UtilisateurEtabli),
            PdfUserDisplayNameFormatting.Format(data.UtilisateurApprouve),
            PdfUserDisplayNameFormatting.Format(data.UtilisateurVisa),
            DateTime.Now);

    private static BilletConversionPdfData MapBilletConversionToPdfData(
        DemandePaiementEntity demande,
        BilletConversion billet)
    {
        var principal = demande.Beneficiaires
            .FirstOrDefault(b => b.EstPrincipal)
            ?? demande.Beneficiaires.OrderBy(b => b.Ordre).FirstOrDefault();

        PdfBeneficiairePrincipal? beneficiaire = principal is null
            ? null
            : new PdfBeneficiairePrincipal(
                principal.RaisonSociale,
                principal.NomComplet,
                principal.EstPrincipal,
                principal.Ordre);

        return new BilletConversionPdfData(
            demande.IdDemandePaiement,
            demande.Reference,
            beneficiaire,
            billet.MontantDeviseOrigine,
            billet.DeviseOrigine,
            billet.TauxApplique,
            billet.DateConversion,
            billet.DemandeChequeNumero,
            billet.CoursEchangeBanque,
            billet.MontantCdf,
            billet.SoldeAPayerDevise,
            billet.UtilisateurEtabli is null
                ? null
                : new PdfUserDisplayName(
                    billet.UtilisateurEtabli.Nom,
                    billet.UtilisateurEtabli.Prenom,
                    billet.UtilisateurEtabli.NomUtilisateur),
            billet.UtilisateurApprouve is null
                ? null
                : new PdfUserDisplayName(
                    billet.UtilisateurApprouve.Nom,
                    billet.UtilisateurApprouve.Prenom,
                    billet.UtilisateurApprouve.NomUtilisateur),
            billet.UtilisateurVisa is null
                ? null
                : new PdfUserDisplayName(
                    billet.UtilisateurVisa.Nom,
                    billet.UtilisateurVisa.Prenom,
                    billet.UtilisateurVisa.NomUtilisateur));
    }

    private static string ResoudreBeneficiaireAffichage(DemandePaiementEntity demande)
    {
        var principal = demande.Beneficiaires
            .FirstOrDefault(b => b.EstPrincipal)
            ?? demande.Beneficiaires.OrderBy(b => b.Ordre).FirstOrDefault();

        if (principal is null)
            return "—";

        if (!string.IsNullOrWhiteSpace(principal.RaisonSociale))
            return principal.RaisonSociale.Trim();

        return principal.NomComplet.Trim();
    }

    private static string? FormatNomUtilisateur(Utilisateur? user)
    {
        if (user is null)
            return null;

        var parts = new[] { user.Prenom, user.Nom }.Where(s => !string.IsNullOrWhiteSpace(s));
        var joined = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(joined) ? user.NomUtilisateur : joined;
    }
}
