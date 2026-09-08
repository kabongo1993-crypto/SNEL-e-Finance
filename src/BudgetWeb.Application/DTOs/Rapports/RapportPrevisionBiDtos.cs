namespace BudgetWeb.Application.DTOs.Rapports;

public record RapportBiQuery(
    long IdVersion,
    string Niveau,
    long? IdEntite = null,
    long? IdDepartementStructure = null,
    long? IdDivision = null,
    long? IdUB = null,
    string? StatutConsultation = null);

public record RapportBiEnTeteDto(
    string Titre,
    string? TitreSynthese,
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
    int NbDepartements,
    decimal MontantTotalBi,
    string Devise,
    string StatutFiltre,
    DateTime DateImpression,
    string ReferenceDocument);

public record RapportBiUbIdentiteDto(
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

/// <summary>
/// TypeLigne: CATEGORIE | ITEM | DETAIL | TOTAL_UB
/// CodeMode métier ANNUEL/MENSUEL uniquement sur DETAIL ; null sur agrégats.
/// Mois null = tiret (ANNUEL ou sous-arbre 100 % ANNUEL).
/// </summary>
public record RapportBiLigneDto(
    string TypeLigne,
    int NiveauHierarchique,
    long? IdItemBI,
    string? CodeItem,
    string Libelle,
    string? CodeAffichage,
    string? DetailBI,
    string? CodeMode,
    bool EstAgrege,
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
    decimal? M12,
    long? IdPrevision);

public record RapportBiUbBlocDto(
    RapportBiUbIdentiteDto Identite,
    string LayoutColonnes,
    IReadOnlyList<RapportBiLigneDto> Lignes,
    decimal TotalUb);

public record RapportBiDepartementBlocDto(
    long? IdDepartementStructure,
    string CodeDepartement,
    string LibelleDepartement,
    int Ordre,
    IReadOnlyList<RapportBiUbBlocDto> Ubs,
    decimal TotalDepartement);

public record RapportBiSyntheseColonneDto(
    long? IdDepartementStructure,
    string CodeDepartement,
    string LibelleDepartement);

public record RapportBiSyntheseLigneDto(
    string TypeLigne,
    string? CodeAffichage,
    string Libelle,
    long? IdItemBI,
    IReadOnlyList<decimal> MontantsParDepartement,
    decimal TotalLigne);

public record RapportBiSyntheseEntiteDto(
    string Titre,
    IReadOnlyList<RapportBiSyntheseColonneDto> ColonnesDepartement,
    IReadOnlyList<RapportBiSyntheseLigneDto> Lignes,
    IReadOnlyList<decimal> TotauxParDepartement,
    decimal TotalGeneral);

public record RapportBiDto(
    RapportBiEnTeteDto EnTete,
    string LayoutColonnes,
    IReadOnlyList<RapportBiDepartementBlocDto> Departements,
    RapportBiSyntheseEntiteDto? SyntheseEntite);
