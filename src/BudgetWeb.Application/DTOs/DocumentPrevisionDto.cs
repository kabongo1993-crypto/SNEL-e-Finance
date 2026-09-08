namespace BudgetWeb.Application.DTOs;

public record DocumentPrevisionUbLigneDto(
    long IdUB,
    string CodeUB,
    string LibelleUB,
    decimal MontantDC,
    decimal MontantAE,
    decimal MontantBI,
    decimal MontantTotal,
    string? StatutAvant = null,
    string? StatutApres = null);

/// <summary>Ligne de détail RB / AE / BI pour le tableau documentaire.</summary>
public record DocumentPrevisionLigneDetailDto(
    bool EstGroupe,
    string Code,
    string Designation,
    string? CodeTypeBudget,
    decimal MontantAnnuel,
    bool EstMensuel,
    decimal M01 = 0,
    decimal M02 = 0,
    decimal M03 = 0,
    decimal M04 = 0,
    decimal M05 = 0,
    decimal M06 = 0,
    decimal M07 = 0,
    decimal M08 = 0,
    decimal M09 = 0,
    decimal M10 = 0,
    decimal M11 = 0,
    decimal M12 = 0);

public record DocumentPrevisionPayloadDto(
    string Titre,
    string Reference,
    string TypeDocument,
    string Portee,
    int NbUbConcernees,
    short AnneeExercice,
    long IdVersion,
    int NumeroVersion,
    string? LibelleVersion,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    long? IdUB,
    string? CodeUB,
    string? LibelleUB,
    DateTime DateEvenement,
    long IdUtilisateurAuteur,
    string NomUtilisateurAuteur,
    string? StatutAvant,
    string? StatutApres,
    string? Motif,
    string? Observations,
    decimal MontantDC,
    decimal MontantAE,
    decimal MontantBI,
    decimal MontantTotal,
    IReadOnlyList<DocumentPrevisionUbLigneDto> UbConcernees)
{
    public string Devise { get; init; } = "USD";
    public string? CodeMode { get; init; }
    public bool EstMensuel { get; init; }
    public string? RoleActeur { get; init; }
    public string ActionLibelle { get; init; } = string.Empty;
    public string StatutAffiche { get; init; } = string.Empty;
    public string SignatureLibelle { get; init; } = string.Empty;
    public IReadOnlyList<DocumentPrevisionLigneDetailDto> LignesDetail { get; init; } = [];
}

public record DocumentPrevisionDto(
    long IdDocument,
    string Reference,
    string TypeDocument,
    string Titre,
    long? IdAudit,
    long IdVersion,
    short AnneeExercice,
    int NumeroVersion,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    long? IdUB,
    string? CodeUB,
    string? LibelleUB,
    string Portee,
    int NbUbConcernees,
    long IdUtilisateurAuteur,
    string NomUtilisateurAuteur,
    DateTime DateEvenement,
    string? StatutAvant,
    string? StatutApres,
    string? Motif,
    decimal MontantDC,
    decimal MontantAE,
    decimal MontantBI,
    decimal MontantTotal,
    DateTime DateGeneration,
    long TailleOctets);

public record DocumentPrevisionCreateCommand(
    string TypeDocument,
    long? IdAudit,
    long IdVersion,
    short AnneeExercice,
    int NumeroVersion,
    string? LibelleVersion,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    long? IdUB,
    string? CodeUB,
    string? LibelleUB,
    string Portee,
    int NbUbConcernees,
    long IdUtilisateurAuteur,
    string NomUtilisateurAuteur,
    DateTime DateEvenement,
    string? StatutAvant,
    string? StatutApres,
    string? Motif,
    string? Observations,
    decimal MontantDC,
    decimal MontantAE,
    decimal MontantBI,
    IReadOnlyList<DocumentPrevisionUbLigneDto> UbConcernees)
{
    public string? CodeMode { get; init; }
    public bool EstMensuel { get; init; }
    public string? RoleActeur { get; init; }
    public IReadOnlyList<DocumentPrevisionLigneDetailDto> LignesDetail { get; init; } = [];
}
