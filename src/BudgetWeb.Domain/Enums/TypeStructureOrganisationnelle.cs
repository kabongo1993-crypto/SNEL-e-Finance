namespace BudgetWeb.Domain.Enums;

public static class TypeStructureOrganisationnelle
{
    public const string Entite = "ENTITE";
    public const string Departement = "DEPARTEMENT";
    public const string Direction = "DIRECTION";
    public const string Division = "DIVISION";
    public const string Service = "SERVICE";
    public const string Section = "SECTION";
    public const string Autre = "AUTRE";

    public static readonly IReadOnlyList<string> OrderedHierarchy =
    [
        Entite,
        Departement,
        Direction,
        Division,
        Service,
        Section,
        Autre
    ];

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && OrderedHierarchy.Contains(value, StringComparer.OrdinalIgnoreCase);
}
