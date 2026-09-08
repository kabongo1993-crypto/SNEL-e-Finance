using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public Task<DemandePaiementAccesContext?> GetDemandeAccesContextAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _context.DemandesPaiement.AsNoTracking()
            .Where(d => d.IdDemandePaiement == idDemande)
            .Select(d => new DemandePaiementAccesContext(
                d.IdDemandePaiement,
                d.Statut,
                d.FK_UniteBudgetaire,
                d.FK_UtilisateurCreation,
                d.FK_UtilisateurAssigne,
                d.TypeBudgetSollicite,
                d.TypeBudget != null ? d.TypeBudget.CodeType : null,
                d.ModePaiementSollicite ?? string.Empty,
                d.Devise))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PieceCaissePdfData?> GetPieceCaissePdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        var statutEtabli = StatutDocumentInstrumentPaiement.Etabli;
        return _context.PiecesCaisse.AsNoTracking()
            .Where(p => p.FK_DemandePaiement == idDemande && p.Statut == statutEtabli)
            .Select(p => new PieceCaissePdfData(
                p.NumeroPiece,
                p.DatePiece,
                p.ReferenceDemande,
                p.Motif,
                p.PieceJustificative,
                p.BeneficiaireAffichage,
                p.BeneficiaireMatricule,
                p.BeneficiaireIdentite,
                p.MontantFc,
                p.MontantEnLettres,
                p.RecuSnel,
                p.Sr,
                p.ComptabiliteGenerale,
                p.Cp,
                p.Cpa,
                p.NumeroAppariement,
                p.IdentifiantVerification,
                new PdfUserDisplayName(
                    p.UtilisateurEtabli.Nom,
                    p.UtilisateurEtabli.Prenom,
                    p.UtilisateurEtabli.NomUtilisateur)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<BonProvisoirePdfData?> GetBonProvisoirePdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        var statutEtabli = StatutDocumentInstrumentPaiement.Etabli;
        return _context.BonsProvisoire.AsNoTracking()
            .Where(b => b.FK_DemandePaiement == idDemande && b.Statut == statutEtabli)
            .Select(b => new BonProvisoirePdfData(
                b.NumeroBon,
                b.DateBon,
                b.ReferenceDemande,
                b.Motif,
                b.MentionJustificationRetrait,
                b.BeneficiaireAffichage,
                b.BeneficiaireMatricule,
                b.BeneficiaireIdentite,
                b.DirectionBeneficiaire,
                b.MontantFc,
                b.MontantEnLettres,
                b.RecuCaisseCentrale,
                b.CompteGeneral,
                b.CompteParticulier,
                b.NumeroAppariement,
                b.IdentifiantVerification,
                new PdfUserDisplayName(
                    b.UtilisateurEtabli.Nom,
                    b.UtilisateurEtabli.Prenom,
                    b.UtilisateurEtabli.NomUtilisateur)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<MinuteChequePdfData?> GetMinuteChequePdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        var statutEtabli = StatutDocumentInstrumentPaiement.Etabli;
        return _context.MinutesCheque.AsNoTracking()
            .Where(m => m.FK_DemandePaiement == idDemande && m.Statut == statutEtabli)
            .Select(m => new MinuteChequePdfData(
                m.NumeroOp,
                m.DateDocument,
                m.ReferenceDemande,
                m.Motif,
                m.BeneficiaireAffichage,
                m.BeneficiaireAdresse,
                m.BeneficiaireBanque,
                m.BeneficiaireNumeroCompte,
                m.MontantPaiement,
                m.DevisePaiement,
                m.MontantEnLettres,
                m.CompteGeneral,
                m.CpCa,
                m.Ls,
                m.SuiviExtraComptable,
                m.NumeroAppariement,
                m.MontantSuiviExtraComptable,
                m.IdentifiantVerification,
                new PdfUserDisplayName(
                    m.UtilisateurEtabli.Nom,
                    m.UtilisateurEtabli.Prenom,
                    m.UtilisateurEtabli.NomUtilisateur)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BilletConversionPdfData?> GetBilletConversionPdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        var statutEtabli = StatutBilletConversion.Etabli;
        var billet = await _context.BilletsConversion.AsNoTracking()
            .Where(b => b.FK_DemandePaiement == idDemande && b.Statut == statutEtabli)
            .Select(b => new
            {
                b.MontantDeviseOrigine,
                b.DeviseOrigine,
                b.TauxApplique,
                b.DateConversion,
                b.DemandeChequeNumero,
                b.CoursEchangeBanque,
                b.MontantCdf,
                b.SoldeAPayerDevise,
                EtabliNom = b.UtilisateurEtabli.Nom,
                EtabliPrenom = b.UtilisateurEtabli.Prenom,
                EtabliNomUtilisateur = b.UtilisateurEtabli.NomUtilisateur,
                ApprouveNom = b.UtilisateurApprouve != null ? b.UtilisateurApprouve.Nom : null,
                ApprouvePrenom = b.UtilisateurApprouve != null ? b.UtilisateurApprouve.Prenom : null,
                ApprouveNomUtilisateur = b.UtilisateurApprouve != null ? b.UtilisateurApprouve.NomUtilisateur : null,
                VisaNom = b.UtilisateurVisa != null ? b.UtilisateurVisa.Nom : null,
                VisaPrenom = b.UtilisateurVisa != null ? b.UtilisateurVisa.Prenom : null,
                VisaNomUtilisateur = b.UtilisateurVisa != null ? b.UtilisateurVisa.NomUtilisateur : null,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (billet is null)
            return null;

        var entete = await _context.DemandesPaiement.AsNoTracking()
            .Where(d => d.IdDemandePaiement == idDemande)
            .Select(d => new { d.IdDemandePaiement, d.Reference })
            .FirstOrDefaultAsync(cancellationToken);

        if (entete is null)
            return null;

        var beneficiaire = await _context.DemandePaiementBeneficiaires.AsNoTracking()
            .Where(b => b.FK_DemandePaiement == idDemande)
            .OrderByDescending(b => b.EstPrincipal)
            .ThenBy(b => b.Ordre)
            .Select(b => new PdfBeneficiairePrincipal(
                b.RaisonSociale,
                b.NomComplet,
                b.EstPrincipal,
                b.Ordre))
            .FirstOrDefaultAsync(cancellationToken);

        return new BilletConversionPdfData(
            entete.IdDemandePaiement,
            entete.Reference,
            beneficiaire,
            billet.MontantDeviseOrigine,
            billet.DeviseOrigine,
            billet.TauxApplique,
            billet.DateConversion,
            billet.DemandeChequeNumero,
            billet.CoursEchangeBanque,
            billet.MontantCdf,
            billet.SoldeAPayerDevise,
            new PdfUserDisplayName(billet.EtabliNom, billet.EtabliPrenom, billet.EtabliNomUtilisateur),
            billet.ApprouveNom is null && billet.ApprouvePrenom is null && billet.ApprouveNomUtilisateur is null
                ? null
                : new PdfUserDisplayName(billet.ApprouveNom, billet.ApprouvePrenom, billet.ApprouveNomUtilisateur),
            billet.VisaNom is null && billet.VisaPrenom is null && billet.VisaNomUtilisateur is null
                ? null
                : new PdfUserDisplayName(billet.VisaNom, billet.VisaPrenom, billet.VisaNomUtilisateur));
    }

    public async Task<DemandePaiementPdfData?> GetDemandePaiementPdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        var header = await _context.DemandesPaiement.AsNoTracking()
            .Where(d => d.IdDemandePaiement == idDemande)
            .Select(d => new
            {
                d.IdDemandePaiement,
                d.Reference,
                d.DateEmission,
                d.LieuEmission,
                d.Objet,
                d.MontantBrut,
                d.Devise,
                d.TypeBudgetSollicite,
                d.ItemSollicite,
                d.ModePaiementSollicite,
                d.CompteSection,
                d.Statut,
                d.DateSoumission,
                FK_Demandeur = d.FK_Demandeur ?? 0L,
                d.FK_CasDossier,
                LibelleDemandeur = d.Demandeur != null ? d.Demandeur.Libelle : null,
                LibelleCasDossier = d.CasDossier != null ? d.CasDossier.Libelle : null,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
            return null;

        var documentSigne = await _context.PiecesJointes.AsNoTracking()
            .AnyAsync(
                p => p.FK_DemandePaiement == idDemande
                     && p.CodeTypePiece == TypePieceJointeDpm.DocumentDpmSigne,
                cancellationToken);

        var beneficiaires = await _context.DemandePaiementBeneficiaires.AsNoTracking()
            .Where(b => b.FK_DemandePaiement == idDemande)
            .OrderBy(b => b.Ordre)
            .Select(b => new DemandePaiementPdfBeneficiaireRow(
                b.TypeBeneficiaire,
                b.NomComplet,
                b.Matricule,
                b.Fonction,
                b.RaisonSociale,
                b.Rccm,
                b.Banque,
                b.NumeroCompte,
                b.EstPrincipal,
                b.Ordre))
            .ToListAsync(cancellationToken);

        var validations = await _context.DemandePaiementValidations.AsNoTracking()
            .Where(v => v.FK_DemandePaiement == idDemande)
            .OrderBy(v => v.Ordre)
            .Select(v => new DemandePaiementPdfValidationRow(
                v.Niveau,
                v.Ordre,
                v.Statut,
                v.ModeValidation,
                v.UtilisateurValidateur != null ? v.UtilisateurValidateur.Nom : null,
                v.UtilisateurDeclarant != null ? v.UtilisateurDeclarant.Nom : null,
                v.NomSignatairePhysique,
                v.FonctionSignatairePhysique,
                v.DateSignaturePhysique,
                v.DateValidation,
                v.Commentaire))
            .ToListAsync(cancellationToken);

        return new DemandePaiementPdfData(
            header.IdDemandePaiement,
            header.Reference,
            header.DateEmission,
            header.LieuEmission,
            header.Objet,
            header.MontantBrut,
            header.Devise,
            header.TypeBudgetSollicite,
            header.ItemSollicite,
            header.ModePaiementSollicite,
            header.CompteSection,
            header.Statut,
            header.DateSoumission,
            header.FK_Demandeur,
            header.FK_CasDossier,
            header.LibelleDemandeur,
            header.LibelleCasDossier,
            documentSigne,
            beneficiaires,
            validations);
    }
}
