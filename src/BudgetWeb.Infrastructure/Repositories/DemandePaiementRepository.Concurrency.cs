using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public async Task<int> UpdateBrouillonHeaderIfModifiableAsync(
        long idDemande,
        DemandePaiementBrouillonHeaderPatch patch,
        CancellationToken cancellationToken = default)
    {
        return await _context.DemandesPaiement
            .Where(d => d.IdDemandePaiement == idDemande
                        && (d.Statut == StatutDemandePaiement.Brouillon
                            || d.Statut == StatutDemandePaiement.ACorriger))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(d => d.DateEmission, patch.DateEmission)
                    .SetProperty(d => d.LieuEmission, patch.LieuEmission)
                    .SetProperty(d => d.Objet, patch.Objet)
                    .SetProperty(d => d.CompteSection, patch.CompteSection)
                    .SetProperty(d => d.MontantBrut, patch.MontantBrut)
                    .SetProperty(d => d.FK_Devise, patch.FK_Devise)
                    .SetProperty(d => d.Devise, patch.Devise)
                    .SetProperty(d => d.TypeBudgetSollicite, patch.TypeBudgetSollicite)
                    .SetProperty(d => d.ItemSollicite, patch.ItemSollicite)
                    .SetProperty(d => d.ModePaiementSollicite, patch.ModePaiementSollicite)
                    .SetProperty(d => d.FK_UtilisateurModification, patch.FK_UtilisateurModification)
                    .SetProperty(d => d.DateModification, patch.DateModification),
                cancellationToken);
    }

    public Task<int> UpdateIfStatutMatchesAsync(
        long idDemande,
        string statutAttendu,
        DemandePaiementConditionalUpdatePatch patch,
        CancellationToken cancellationToken = default)
    {
        var attendu = StatutDemandePaiement.Normaliser(statutAttendu);
        var query = _context.DemandesPaiement
            .Where(d => d.IdDemandePaiement == idDemande && d.Statut == attendu);

        // Locals capturés : EF Core exige une chaîne SetProperty pure (pas d'appel de méthode).
        // null dans le patch = ne pas modifier (équivalent de l'ancien ApplyConditionalPatch).
        var nouveauStatut = patch.NouveauStatut;
        var fkModification = patch.FK_UtilisateurModification;
        var dateModification = patch.DateModification;
        var fkSoumission = patch.FK_UtilisateurSoumission;
        var dateSoumission = patch.DateSoumission;
        var fkReception = patch.FK_UtilisateurReception;
        var dateReception = patch.DateReception;
        var fkControle = patch.FK_UtilisateurControle;
        var dateControle = patch.DateControle;
        var fkVisa = patch.FK_UtilisateurVisa;
        var dateVisa = patch.DateVisa;
        var fkRetour = patch.FK_UtilisateurRetour;
        var dateRetour = patch.DateRetour;
        var motifRetour = patch.MotifRetour;
        var commentaireRetour = patch.CommentaireRetour;
        var fkTypeBudget = patch.FK_TypeBudget;
        var typeInstrument = patch.TypeInstrumentPaiement;
        var devisePaiement = patch.DevisePaiement;
        var montantPaiement = patch.MontantPaiement;
        var tauxPaiement = patch.TauxPaiement;
        var fkTauxChangePaiement = patch.FK_TauxChangePaiement;
        var tauxConversion = patch.TauxConversion;
        var montantUsd = patch.MontantUsd;
        var fkTauxChange = patch.FK_TauxChange;
        var modePaiementSollicite = patch.ModePaiementSollicite;
        var typeBudgetSollicite = patch.TypeBudgetSollicite;
        var itemSollicite = patch.ItemSollicite;
        var fkAssigne = patch.FK_UtilisateurAssigne;
        var mettreAJourAssigne = patch.MettreAJourAssigne;
        var effacerSoumission = patch.EffacerSoumission;

        return query.ExecuteUpdateAsync(
            s => s
                .SetProperty(d => d.Statut, d => nouveauStatut != null ? nouveauStatut : d.Statut)
                .SetProperty(d => d.FK_UtilisateurModification, d => fkModification ?? d.FK_UtilisateurModification)
                .SetProperty(d => d.DateModification, d => dateModification ?? d.DateModification)
                .SetProperty(
                    d => d.FK_UtilisateurSoumission,
                    d => effacerSoumission ? null : fkSoumission ?? d.FK_UtilisateurSoumission)
                .SetProperty(
                    d => d.DateSoumission,
                    d => effacerSoumission ? null : dateSoumission ?? d.DateSoumission)
                .SetProperty(d => d.FK_UtilisateurReception, d => fkReception ?? d.FK_UtilisateurReception)
                .SetProperty(d => d.DateReception, d => dateReception ?? d.DateReception)
                .SetProperty(d => d.FK_UtilisateurControle, d => fkControle ?? d.FK_UtilisateurControle)
                .SetProperty(d => d.DateControle, d => dateControle ?? d.DateControle)
                .SetProperty(d => d.FK_UtilisateurVisa, d => fkVisa ?? d.FK_UtilisateurVisa)
                .SetProperty(d => d.DateVisa, d => dateVisa ?? d.DateVisa)
                .SetProperty(d => d.FK_UtilisateurRetour, d => fkRetour ?? d.FK_UtilisateurRetour)
                .SetProperty(d => d.DateRetour, d => dateRetour ?? d.DateRetour)
                .SetProperty(d => d.MotifRetour, d => motifRetour != null ? motifRetour : d.MotifRetour)
                .SetProperty(d => d.CommentaireRetour, d => commentaireRetour != null ? commentaireRetour : d.CommentaireRetour)
                .SetProperty(d => d.FK_TypeBudget, d => fkTypeBudget ?? d.FK_TypeBudget)
                .SetProperty(d => d.FK_UtilisateurAssigne, d => mettreAJourAssigne ? fkAssigne : d.FK_UtilisateurAssigne)
                .SetProperty(d => d.TypeInstrumentPaiement, d => typeInstrument != null ? typeInstrument : d.TypeInstrumentPaiement)
                .SetProperty(d => d.DevisePaiement, d => devisePaiement != null ? devisePaiement : d.DevisePaiement)
                .SetProperty(d => d.MontantPaiement, d => montantPaiement ?? d.MontantPaiement)
                .SetProperty(d => d.TauxPaiement, d => tauxPaiement ?? d.TauxPaiement)
                .SetProperty(d => d.FK_TauxChangePaiement, d => fkTauxChangePaiement ?? d.FK_TauxChangePaiement)
                .SetProperty(d => d.TauxConversion, d => tauxConversion ?? d.TauxConversion)
                .SetProperty(d => d.MontantUsd, d => montantUsd ?? d.MontantUsd)
                .SetProperty(d => d.FK_TauxChange, d => fkTauxChange ?? d.FK_TauxChange)
                .SetProperty(d => d.ModePaiementSollicite, d => modePaiementSollicite != null ? modePaiementSollicite : d.ModePaiementSollicite)
                .SetProperty(d => d.TypeBudgetSollicite, d => typeBudgetSollicite != null ? typeBudgetSollicite : d.TypeBudgetSollicite)
                .SetProperty(d => d.ItemSollicite, d => itemSollicite != null ? itemSollicite : d.ItemSollicite),
            cancellationToken);
    }
}
