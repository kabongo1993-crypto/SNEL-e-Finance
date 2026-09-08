namespace BudgetWeb.Application.DTOs.Rapports;

public static class RapportAeMoisCodes
{
    public const string Tous = "TOUS";

    public static readonly string[] Libelles =
    [
        "JANVIER", "FÉVRIER", "MARS", "AVRIL", "MAI", "JUIN",
        "JUILLET", "AOÛT", "SEPTEMBRE", "OCTOBRE", "NOVEMBRE", "DÉCEMBRE"
    ];

    public static readonly string[] LibellesCourts =
    [
        "JAN", "FÉV", "MAR", "AVR", "MAI", "JUN",
        "JUL", "AOÛ", "SEP", "OCT", "NOV", "DÉC"
    ];

    /// <summary>Retourne 1..12 ou null si « tous ». Lance ArgumentException si invalide.</summary>
    public static int? Parse(string? mois)
    {
        if (string.IsNullOrWhiteSpace(mois))
        {
            throw new ArgumentException("Le paramètre « mois » est obligatoire (1..12 ou « tous »).");
        }

        var raw = mois.Trim();
        if (string.Equals(raw, "tous", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, Tous, StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (int.TryParse(raw, out var n) && n is >= 1 and <= 12)
        {
            return n;
        }

        throw new ArgumentException("Le paramètre « mois » est invalide. Utilisez 1..12 ou « tous ».");
    }
}

public record RapportAeQuery(
    long IdVersion,
    string Niveau,
    string Mois,
    long? IdEntite = null,
    long? IdDepartementStructure = null,
    long? IdDivision = null,
    long? IdUB = null,
    string? StatutConsultation = null);

public record RapportAeEnTeteDto(
    string Titre,
    short Annee,
    long IdVersion,
    int NumeroVersion,
    string? LibelleVersion,
    string Niveau,
    string ModeMois,
    string? LibelleMois,
    long? IdEntite,
    string? CodeEntite,
    string? LibelleEntite,
    long? IdDepartementStructure,
    string? CodeDepartementStructure,
    string? LibelleDepartementStructure,
    long? IdDivision,
    string? CodeDivision,
    string? LibelleDivision,
    int NbUB,
    decimal MontantTotalAe,
    string Devise,
    string StatutFiltre,
    DateTime DateImpression,
    string ReferenceDocument);

public record RapportAeUbIdentiteDto(
    long? IdEntite,
    string? CodeEntite,
    string? LibelleEntite,
    long? IdDepartementStructure,
    string? CodeDepartementStructure,
    string? LibelleDepartementStructure,
    string CodeDepartementTable,
    string LibelleDepartementTable,
    long? IdDivision,
    string? CodeDivision,
    string? LibelleDivision,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    string TypeBudget,
    string Statut,
    string Devise,
    short Annee,
    int NumeroVersion,
    string? LibelleVersion);

public record RapportAeColonneRbDto(
    long IdRB,
    string CodeRB,
    string LibelleRB,
    long? IdGroupeRB,
    int OrdreAffichage);

/// <summary>
/// Ligne détail Item×RB. Pour ITEM, MontantsParRb[i] est null (= tiret) si contribution annuelle absente ;
/// 0 si mensuel à zéro ; valeur si montant mensuel.
/// </summary>
public record RapportAeLigneDetailDto(
    string TypeLigne,
    int OrdreAffichage,
    long? IdGroupeItemAE,
    string? LibelleGroupe,
    string? Numero,
    string? LibelleItemAE,
    IReadOnlyList<decimal?> MontantsParRb,
    decimal? Total);

public record RapportAeDetailMensuelDto(
    int Mois,
    string LibelleMois,
    IReadOnlyList<RapportAeColonneRbDto> ColonnesRb,
    IReadOnlyList<RapportAeLigneDetailDto> Lignes,
    IReadOnlyList<decimal> TotauxParRb,
    decimal TotalGeneral);

public record RapportAeUbBlocDto(
    RapportAeUbIdentiteDto Identite,
    /// <summary>Un seul élément si mois unique ; 12 si tous les mois.</summary>
    IReadOnlyList<RapportAeDetailMensuelDto> DetailsMensuels);

public record RapportAeSyntheseItemLigneDto(
    string TypeLigne,
    int OrdreAffichage,
    long? IdGroupeItemAE,
    string? LibelleGroupe,
    string? Numero,
    string? LibelleItemAE,
    decimal? M01,
    decimal? M02,
    decimal? M03,
    decimal? M04,
    decimal? M05,
    decimal? M06,
    decimal? M07,
    decimal? M08,
    decimal? M09,
    decimal? M10,
    decimal? M11,
    decimal? M12,
    /// <summary>Σ MontantAnnuel des prévisions de l'item (inclut ANNUEL).</summary>
    decimal? Total);

public record RapportAeSyntheseItemDto(
    string Titre,
    IReadOnlyList<RapportAeSyntheseItemLigneDto> Lignes,
    decimal TotalGeneral);

public record RapportAeSyntheseRbLigneDto(
    long IdRB,
    string CodeRB,
    string Libelle,
    string CodeMode,
    decimal MontantCumul,
    decimal? M01,
    decimal? M02,
    decimal? M03,
    decimal? M04,
    decimal? M05,
    decimal? M06,
    decimal? M07,
    decimal? M08,
    decimal? M09,
    decimal? M10,
    decimal? M11,
    decimal? M12);

public record RapportAeSyntheseRbGroupeDto(
    long? IdGroupeRB,
    string CodeGroupe,
    string Libelle,
    int OrdreAffichage,
    decimal SousTotal,
    IReadOnlyList<RapportAeSyntheseRbLigneDto> Lignes);

public record RapportAeSyntheseRbDto(
    string Titre,
    IReadOnlyList<RapportAeSyntheseRbGroupeDto> Groupes,
    decimal TotalGeneral);

public record RapportAeDto(
    RapportAeEnTeteDto EnTete,
    bool EstTousLesMois,
    IReadOnlyList<RapportAeUbBlocDto> BlocsUb,
    RapportAeSyntheseItemDto? SyntheseItem,
    RapportAeSyntheseRbDto? SyntheseRb);
