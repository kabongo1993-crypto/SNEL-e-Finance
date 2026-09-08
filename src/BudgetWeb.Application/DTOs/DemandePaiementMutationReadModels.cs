namespace BudgetWeb.Application.DTOs;

/// <summary>
/// Projection minimale pour les mutations DPM (Vague 2b) — contrôles d'accès, statut, en-tête, transition.
/// </summary>
public sealed record DemandePaiementMutationHeaderReadModel(
    long IdDemandePaiement,
    string Statut,
    long FK_UniteBudgetaire,
    long FK_UtilisateurCreation,
    long? FK_UtilisateurAssigne,
    string? TypeBudgetSollicite,
    string? CodeTypeBudget,
    long? FK_Demandeur,
    string Objet,
    decimal MontantBrut,
    long FK_CasDossier,
    string Devise,
    string? ItemSollicite,
    string ModePaiementSollicite);
