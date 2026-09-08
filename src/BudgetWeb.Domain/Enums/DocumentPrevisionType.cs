namespace BudgetWeb.Domain.Enums;

public static class DocumentPrevisionType
{
    public const string Soumission = "SUB";
    public const string Rejet = "REJ";
    public const string Controle = "CTL";
    public const string Validation = "VAL";

    public static string Titre(string type) => TitreCourt(type);

    public static string TitreCourt(string type) => (type ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        Soumission => "PRÉVISION BUDGÉTAIRE — SOUMISSION",
        Rejet => "PRÉVISION BUDGÉTAIRE — REJET",
        Controle => "PRÉVISION BUDGÉTAIRE — CONTRÔLE",
        Validation => "PRÉVISION BUDGÉTAIRE — VALIDATION",
        _ => "PRÉVISION BUDGÉTAIRE",
    };

    public static string ActionLibelle(string type) => (type ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        Soumission => "SOUMISSION DE PRÉVISION BUDGÉTAIRE",
        Rejet => "REJET DE PRÉVISION BUDGÉTAIRE",
        Controle => "CONTRÔLE DE PRÉVISION BUDGÉTAIRE",
        Validation => "VALIDATION DE PRÉVISION BUDGÉTAIRE",
        _ => "OPÉRATION SUR PRÉVISION BUDGÉTAIRE",
    };

    public static string StatutAffiche(string type) => (type ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        Soumission => "SOUMISE",
        Rejet => "REJETÉE",
        Controle => "CONTRÔLÉE",
        Validation => "VALIDÉE",
        _ => "—",
    };

    public static string SignatureRole(string type) => (type ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        Soumission => "ÉTABLI / SOUMIS PAR",
        Rejet => "REJETÉ PAR",
        Controle => "CONTRÔLÉ PAR",
        Validation => "VALIDÉ PAR",
        _ => "ÉTABLI PAR",
    };
}

public static class DocumentPrevisionPortee
{
    public const string Ub = "UB";
    public const string Departement = "DEPARTEMENT";
}
