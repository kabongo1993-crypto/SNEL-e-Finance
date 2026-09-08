namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Actions DPM soumises à évaluation d'accès unifiée (Lot 3.2).</summary>
public enum DemandePaiementAccesAction
{
    Lire,
    Modifier,
    Traiter,
    ValiderN1,
    ValiderN2,
    Receptionner,
    Orienter,
    ControlerBudget,
    ViserBudget,
    Retourner,
    PrendreEnCharge,
    Imputer,
    Soumettre,
    EnvoyerValidation,
    DeclarerValidationPhysique,
    JoindreDocumentSigne,
}
