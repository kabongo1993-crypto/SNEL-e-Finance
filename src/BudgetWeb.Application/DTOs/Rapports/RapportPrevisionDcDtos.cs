namespace BudgetWeb.Application.DTOs.Rapports;

public record RapportDcQuery(
    long IdVersion,
    string Niveau,
    long? IdEntite = null,
    long? IdDepartementStructure = null,
    long? IdDivision = null,
    long? IdUB = null,
    string? StatutConsultation = null);

public record RapportDcEnTeteDto(
    string Titre,
    short Annee,
    long IdVersion,
    int NumeroVersion,
    string? LibelleVersion,
    string Niveau,
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

public record RapportDcUbIdentiteDto(
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

public record RapportDcLigneRbDto(
    string CodeRB,
    string Libelle,
    string CodeMode,
    decimal MontantAnnuel,
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

public record RapportDcGroupeDto(
    long? IdGroupeRB,
    string CodeGroupe,
    string Libelle,
    int OrdreAffichage,
    decimal SousTotalDc,
    IReadOnlyList<RapportDcLigneRbDto> Lignes);

public record RapportDcUbBlocDto(
    RapportDcUbIdentiteDto Identite,
    decimal TotalDc,
    IReadOnlyList<RapportDcGroupeDto> Groupes);

public record RapportDcDto(
    RapportDcEnTeteDto EnTete,
    string LayoutColonnes,
    IReadOnlyList<RapportDcUbBlocDto> BlocsUb,
    /// <summary>Tableau consolidé du périmètre — toujours en fin de rapport.</summary>
    IReadOnlyList<RapportDcConsolideGroupeDto> GroupesConsolides);
