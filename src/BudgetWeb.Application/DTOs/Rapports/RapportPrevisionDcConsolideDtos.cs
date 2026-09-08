namespace BudgetWeb.Application.DTOs.Rapports;

public record RapportDcConsolideQuery(
    long IdVersion,
    long? IdEntite = null,
    long? IdDepartementStructure = null,
    long? IdDivision = null,
    string? StatutConsultation = null);

public record RapportDcConsolideEnTeteDto(
    string Titre,
    short Annee,
    long IdVersion,
    int NumeroVersion,
    string? LibelleVersion,
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
    decimal MontantTotalDc,
    string Devise,
    string StatutFiltre,
    DateTime DateImpression,
    string ReferenceDocument);

/// <summary>
/// Ligne consolidée. Mode MENSUEL / ANNUEL / MIXTE au niveau de la ligne affichée.
/// Pour MIXTE cross-UB : deux lignes distinctes (une MENSUEL, une ANNUEL) pour la même RB.
/// </summary>
public record RapportDcConsolideLigneDto(
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

public record RapportDcConsolideGroupeDto(
    long? IdGroupeRB,
    string CodeGroupe,
    string Libelle,
    int OrdreAffichage,
    decimal SousTotalDc,
    IReadOnlyList<RapportDcConsolideLigneDto> Lignes);

public record RapportDcConsolideDto(
    RapportDcConsolideEnTeteDto EnTete,
    IReadOnlyList<RapportDcConsolideGroupeDto> Groupes);
