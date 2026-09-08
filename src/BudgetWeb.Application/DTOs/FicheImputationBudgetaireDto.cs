namespace BudgetWeb.Application.DTOs;

/// <summary>État de la fiche d'imputation budgétaire.</summary>
public enum FicheImputationMode
{
    /// <summary>Fiche de travail Junior — engagement en cours uniquement.</summary>
    Travail = 0,

    /// <summary>Fiche définitive figée au visa — toutes colonnes budgétaires.</summary>
    Definitive = 1,
}

public record FicheImputationSignataireDto(
    string? NomComplet,
    string? Fonction,
    DateTime? Date);

public record FicheImputationLigneDto(
    int Item,
    string CodeUB,
    string? NumeroSuiviBudgetaire,
    string? RubriqueBudgetaire,
    decimal? BudgetMensuel,
    decimal? CreditEngageMensuel,
    decimal? EngagementEnCoursMensuel,
    decimal? CreditDisponibleMensuel,
    decimal? BudgetAnnuel,
    decimal? CreditEngageAnnuel,
    decimal? EngagementEnCoursAnnuel,
    decimal? CreditDisponibleAnnuel);

public record FicheImputationTotauxDto(
    decimal? TotalBudgetMensuel,
    decimal? TotalCreditEngageMensuel,
    decimal? TotalEngagementEnCoursMensuel,
    decimal? TotalCreditDisponibleMensuel,
    decimal? TotalBudgetAnnuel,
    decimal? TotalCreditEngageAnnuel,
    decimal? TotalEngagementEnCoursAnnuel,
    decimal? TotalCreditDisponibleAnnuel);

/// <summary>Modèle unifié de la fiche d'imputation budgétaire (travail ou définitive).</summary>
public record FicheImputationBudgetaireDto(
    FicheImputationMode Mode,
    long IdDemandePaiement,
    string Reference,
    short AnneeExercice,
    string? CodeTypeDepenses,
    DateTime? DateEngagement,
    DateTime DateGeneration,
    IReadOnlyList<FicheImputationLigneDto> Lignes,
    FicheImputationTotauxDto Totaux,
    FicheImputationSignataireDto GestionnaireJunior,
    FicheImputationSignataireDto GestionnaireSenior,
    FicheImputationSignataireDto ChefDivision);
