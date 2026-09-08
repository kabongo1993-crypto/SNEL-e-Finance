namespace BudgetWeb.Domain.Enums;

/// <summary>Niveaux de validation interne entité émettrice.</summary>
public static class ValidationEntiteNiveau
{
    public const byte N1 = 1;
    public const byte N2 = 2;

    public static readonly IReadOnlyList<byte> Valeurs = [N1, N2];

    public static bool IsValid(byte niveau) => niveau is N1 or N2;
}

/// <summary>Mode de validation entité (électronique vs physique déclarée).</summary>
public static class ModeValidationEntite
{
    public const string Electronique = "ELECTRONIQUE";
    public const string Physique = "PHYSIQUE";

    public static readonly IReadOnlyList<string> Valeurs = [Electronique, Physique];

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? value)
        => Valeurs.Contains(Normaliser(value), StringComparer.Ordinal);
}

/// <summary>Statut d'une étape de validation entité.</summary>
public static class StatutValidationEntite
{
    public const string EnAttente = "EN_ATTENTE";
    public const string Validee = "VALIDEE";
    public const string Rejetee = "REJETEE";

    public static readonly IReadOnlyList<string> Valeurs = [EnAttente, Validee, Rejetee];

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? value)
        => Valeurs.Contains(Normaliser(value), StringComparer.Ordinal);
}

/// <summary>Code type pièce — document DPM signé physiquement (distinct des annexes IGCF).</summary>
public static class TypePieceJointeDpm
{
    public const string DocumentDpmSigne = "DOCUMENT_DPM_SIGNE";
}
