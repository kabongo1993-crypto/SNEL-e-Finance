namespace BudgetWeb.Application.DTOs;

/// <summary>Champs d'en-tête modifiables en brouillon (sans le statut).</summary>
public sealed record DemandePaiementBrouillonHeaderPatch(
    DateOnly DateEmission,
    string? LieuEmission,
    string Objet,
    string? CompteSection,
    decimal MontantBrut,
    long? FK_Devise,
    string Devise,
    string? TypeBudgetSollicite,
    string? ItemSollicite,
    string? ModePaiementSollicite,
    long FK_UtilisateurModification,
    DateTime DateModification);
