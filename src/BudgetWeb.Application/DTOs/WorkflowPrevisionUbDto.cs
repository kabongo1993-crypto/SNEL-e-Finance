namespace BudgetWeb.Application.DTOs;

public record WorkflowPrevisionUbDto(
    long IdWorkflowPrevisionUB,
    long IdVersion,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    string Statut,
    DateTime? DateSoumission,
    string? NomUtilisateurSoumission,
    DateTime? DateControle,
    string? NomUtilisateurControle,
    DateTime? DateValidation,
    string? NomUtilisateurValidation,
    DateTime? DateRejet,
    string? NomUtilisateurRejet,
    string? MotifRejet)
{
    /// <summary>Document généré après transition réussie (métadonnées ; PDF via API documents).</summary>
    public DocumentPrevisionDto? Document { get; init; }
}

public record WorkflowUbActionRequest(long IdVersion, long IdUB);

public record WorkflowUbRejetRequest(long IdVersion, long IdUB, string Motif);

public record WorkflowDepartementRejetRequest(long IdVersion, long IdDepartement, string Motif);

public record WorkflowUbRejetDetailDto(
    long IdUB,
    string CodeUB,
    string StatutAvant,
    string? StatutApres,
    string Outcome);

/// <summary>Résultat d'une action de masse Département (contrôler / valider / rejeter).</summary>
public record WorkflowDepartementBulkResultDto(
    int Traitees,
    int Ignorees,
    IReadOnlyList<WorkflowUbRejetDetailDto> Details)
{
    /// <summary>Document unique de portée département (si au moins une UB traitée).</summary>
    public DocumentPrevisionDto? Document { get; init; }
}

/// <summary>Alias historique — même contrat que <see cref="WorkflowDepartementBulkResultDto"/>.</summary>
public record WorkflowDepartementRejetResultDto(
    int Rejetees,
    int Ignorees,
    IReadOnlyList<WorkflowUbRejetDetailDto> Details);

public record WorkflowDepartementActionRequest(long IdVersion, long IdDepartement);
