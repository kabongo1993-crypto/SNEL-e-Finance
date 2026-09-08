namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Types de documents de paiement déjà établis (liste / impression).</summary>
public static class TypeDocumentEtabli
{
    public const string BilletConversion = "BILLET_CONVERSION";
    public const string PieceCaisse = "PIECE_CAISSE";
    public const string BonProvisoire = "BON_PROVISOIRE";
    public const string MinuteCheque = "MINUTE_CHEQUE";

    public static readonly IReadOnlyList<string> Valeurs =
    [
        BilletConversion,
        PieceCaisse,
        BonProvisoire,
        MinuteCheque,
    ];

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? value)
        => Valeurs.Contains(Normaliser(value), StringComparer.Ordinal);

    public static string Libelle(string? value)
        => Normaliser(value) switch
        {
            BilletConversion => "Billet de conversion",
            PieceCaisse => "Pièce de caisse",
            BonProvisoire => "Bon provisoire",
            MinuteCheque => "Minute de chèque",
            _ => "Document",
        };

    public static string PdfRouteSegment(string? value)
        => Normaliser(value) switch
        {
            BilletConversion => "billet-conversion",
            PieceCaisse => "piece-caisse",
            BonProvisoire => "bon-provisoire",
            MinuteCheque => "minute-cheque",
            _ => string.Empty,
        };
}
