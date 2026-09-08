using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public async Task<DemandePaiement?> GetDetailConsultationAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        var demande = await _context.DemandesPaiement.AsNoTracking()
            .Where(d => d.IdDemandePaiement == idDemande)
            .Select(d => new DemandePaiement
            {
                IdDemandePaiement = d.IdDemandePaiement,
                Reference = d.Reference,
                DateEmission = d.DateEmission,
                LieuEmission = d.LieuEmission,
                FK_ExerciceBudgetaire = d.FK_ExerciceBudgetaire,
                FK_VersionBudgetaire = d.FK_VersionBudgetaire,
                FK_UniteBudgetaire = d.FK_UniteBudgetaire,
                FK_Demandeur = d.FK_Demandeur,
                FK_CasDossier = d.FK_CasDossier,
                FK_TypeBudget = d.FK_TypeBudget,
                TypeBudgetSollicite = d.TypeBudgetSollicite,
                ItemSollicite = d.ItemSollicite,
                Objet = d.Objet,
                CompteSection = d.CompteSection,
                MontantBrut = d.MontantBrut,
                Devise = d.Devise,
                FK_Devise = d.FK_Devise,
                TauxConversion = d.TauxConversion,
                MontantUsd = d.MontantUsd,
                FK_TauxChange = d.FK_TauxChange,
                ModePaiementSollicite = d.ModePaiementSollicite,
                TypeInstrumentPaiement = d.TypeInstrumentPaiement,
                DevisePaiement = d.DevisePaiement,
                MontantPaiement = d.MontantPaiement,
                TauxPaiement = d.TauxPaiement,
                FK_TauxChangePaiement = d.FK_TauxChangePaiement,
                Statut = d.Statut,
                MotifRetour = d.MotifRetour,
                CommentaireRetour = d.CommentaireRetour,
                FK_UtilisateurCreation = d.FK_UtilisateurCreation,
                DateCreation = d.DateCreation,
                DateSoumission = d.DateSoumission,
                DateReception = d.DateReception,
                DateControle = d.DateControle,
                DateVisa = d.DateVisa,
                DateRetour = d.DateRetour,
                CasDossier = new CasDossier
                {
                    IdCasDossier = d.CasDossier.IdCasDossier,
                    Code = d.CasDossier.Code,
                    Libelle = d.CasDossier.Libelle,
                },
                ExerciceBudgetaire = new ExerciceBudgetaire
                {
                    IdExercice = d.ExerciceBudgetaire.IdExercice,
                    Annee = d.ExerciceBudgetaire.Annee,
                },
                UniteBudgetaire = new UniteBudgetaire
                {
                    IdUB = d.UniteBudgetaire.IdUB,
                    CodeUB = d.UniteBudgetaire.CodeUB,
                    Libelle = d.UniteBudgetaire.Libelle,
                    FK_Departement = d.UniteBudgetaire.FK_Departement,
                    Departement = new Departement
                    {
                        IdDepartement = d.UniteBudgetaire.Departement.IdDepartement,
                        Code = d.UniteBudgetaire.Departement.Code,
                        Libelle = d.UniteBudgetaire.Departement.Libelle,
                    },
                },
                Demandeur = d.Demandeur == null
                    ? null
                    : new Demandeur
                    {
                        IdDemandeur = d.Demandeur.IdDemandeur,
                        Code = d.Demandeur.Code,
                        Libelle = d.Demandeur.Libelle,
                    },
                TypeBudget = d.TypeBudget == null
                    ? null
                    : new TypeBudget
                    {
                        IdTypeBudget = d.TypeBudget.IdTypeBudget,
                        CodeType = d.TypeBudget.CodeType,
                    },
                VersionBudgetaire = d.VersionBudgetaire == null
                    ? null
                    : new VersionBudgetaire
                    {
                        IdVersion = d.VersionBudgetaire.IdVersion,
                        NumeroVersion = d.VersionBudgetaire.NumeroVersion,
                    },
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (demande is null)
            return null;

        demande.Beneficiaires = await _context.DemandePaiementBeneficiaires.AsNoTracking()
            .Where(b => b.FK_DemandePaiement == idDemande)
            .OrderBy(b => b.Ordre)
            .ToListAsync(cancellationToken);

        demande.Imputations = await _context.DemandePaiementImputations.AsNoTracking()
            .Where(i => i.FK_DemandePaiement == idDemande)
            .OrderBy(i => i.Ordre)
            .Select(i => new DemandePaiementImputation
            {
                IdImputation = i.IdImputation,
                Ordre = i.Ordre,
                FK_TypeBudget = i.FK_TypeBudget,
                FK_UniteBudgetaire = i.FK_UniteBudgetaire,
                FK_ExerciceBudgetaire = i.FK_ExerciceBudgetaire,
                FK_RubriqueBudgetaire = i.FK_RubriqueBudgetaire,
                Mois = i.Mois,
                LibelleItemAE = i.LibelleItemAE,
                FK_GroupeItemAE = i.FK_GroupeItemAE,
                FK_ItemBI = i.FK_ItemBI,
                DetailBI = i.DetailBI,
                FK_BudgetLigne = i.FK_BudgetLigne,
                MontantBrut = i.MontantBrut,
                Devise = i.Devise,
                TauxConversion = i.TauxConversion,
                MontantUsd = i.MontantUsd,
                NumeroFicheSuivi = i.NumeroFicheSuivi,
                TypeBudget = new TypeBudget
                {
                    IdTypeBudget = i.TypeBudget.IdTypeBudget,
                    CodeType = i.TypeBudget.CodeType,
                },
            })
            .ToListAsync(cancellationToken);

        demande.PiecesJointes = await _context.PiecesJointes.AsNoTracking()
            .Where(p => p.FK_DemandePaiement == idDemande)
            .OrderBy(p => p.DateUpload)
            .ToListAsync(cancellationToken);

        demande.ValidationsEntite = await _context.DemandePaiementValidations.AsNoTracking()
            .Where(v => v.FK_DemandePaiement == idDemande)
            .OrderBy(v => v.Ordre)
            .Select(v => new DemandePaiementValidation
            {
                Niveau = v.Niveau,
                Ordre = v.Ordre,
                Statut = v.Statut,
                ModeValidation = v.ModeValidation,
                FK_UtilisateurValidateur = v.FK_UtilisateurValidateur,
                FK_UtilisateurDeclarant = v.FK_UtilisateurDeclarant,
                NomSignatairePhysique = v.NomSignatairePhysique,
                FonctionSignatairePhysique = v.FonctionSignatairePhysique,
                DateSignaturePhysique = v.DateSignaturePhysique,
                DateValidation = v.DateValidation,
                Commentaire = v.Commentaire,
                UtilisateurValidateur = v.FK_UtilisateurValidateur == null
                    ? null
                    : new Utilisateur
                    {
                        IdUtilisateur = v.UtilisateurValidateur!.IdUtilisateur,
                        Nom = v.UtilisateurValidateur.Nom,
                    },
                UtilisateurDeclarant = v.FK_UtilisateurDeclarant == null
                    ? null
                    : new Utilisateur
                    {
                        IdUtilisateur = v.UtilisateurDeclarant!.IdUtilisateur,
                        Nom = v.UtilisateurDeclarant.Nom,
                    },
            })
            .ToListAsync(cancellationToken);

        await ChargerStatutsInstrumentsConsultationAsync(demande, idDemande, cancellationToken);

        return demande;
    }

    public async Task<IReadOnlyList<JournalAudit>> GetHistoriqueConsultationAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => await _context.JournalAudits.AsNoTracking()
            .Where(j => j.Entite == "DEMANDE_PAIEMENT" && j.IdEntite == idDemande)
            .OrderByDescending(j => j.DateHeure)
            .ToListAsync(cancellationToken);

    public Task EnrichStatutsInstrumentsMapDetailAsync(
        DemandePaiement demande,
        CancellationToken cancellationToken = default)
        => TrackRepoAsync(
            "EnrichStatutsInstrumentsMapDetailAsync",
            () => ChargerStatutsInstrumentsConsultationAsync(
                demande,
                demande.IdDemandePaiement,
                cancellationToken));

    private async Task ChargerStatutsInstrumentsConsultationAsync(
        DemandePaiement demande,
        long idDemande,
        CancellationToken cancellationToken)
    {
        var billetStatut = await _context.BilletsConversion.AsNoTracking()
            .Where(b => b.FK_DemandePaiement == idDemande)
            .Select(b => b.Statut)
            .FirstOrDefaultAsync(cancellationToken);
        if (billetStatut is not null)
            demande.BilletConversion = new BilletConversion { Statut = billetStatut };

        var pieceStatut = await _context.PiecesCaisse.AsNoTracking()
            .Where(p => p.FK_DemandePaiement == idDemande)
            .Select(p => p.Statut)
            .FirstOrDefaultAsync(cancellationToken);
        if (pieceStatut is not null)
            demande.PieceCaisse = new PieceCaisse { Statut = pieceStatut };

        var bonStatut = await _context.BonsProvisoire.AsNoTracking()
            .Where(b => b.FK_DemandePaiement == idDemande)
            .Select(b => b.Statut)
            .FirstOrDefaultAsync(cancellationToken);
        if (bonStatut is not null)
            demande.BonProvisoire = new BonProvisoire { Statut = bonStatut };

        var minuteStatut = await _context.MinutesCheque.AsNoTracking()
            .Where(m => m.FK_DemandePaiement == idDemande)
            .Select(m => m.Statut)
            .FirstOrDefaultAsync(cancellationToken);
        if (minuteStatut is not null)
            demande.MinuteCheque = new MinuteCheque { Statut = minuteStatut };
    }
}
