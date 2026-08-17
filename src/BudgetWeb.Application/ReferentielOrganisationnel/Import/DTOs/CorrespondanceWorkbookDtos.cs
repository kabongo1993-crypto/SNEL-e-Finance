namespace BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;

public record EntiteSheetRow(string Sigle, string Libelle, string TypeStructure);

public record DepartementSheetRow(string Sigle, string Libelle);

public record EntiteDepartementSheetRow(string SigleEntite, string LibelleEntite, string SigleDepartement, string LibelleDepartement);

public record UbImportSheetRow(
    string SigleEntite,
    string LibelleEntite,
    string SigleDepartement,
    string LibelleDepartement,
    string CodeUB,
    string LibelleUB,
    int LigneSource);

public record ElementRattacheSheetRow(
    string SigleEntite,
    string SigleDepartement,
    string CodeUB,
    string LibelleUB,
    string? Division,
    string? ContenuUB,
    int IdtGroupeSource,
    string TypeElementSource,
    int LigneSource);

public record SansCodeUbSheetRow(
    string SigleEntite,
    string SigleDepartement,
    string LibelleUB,
    string? Division,
    string? ContenuUB,
    string TypeElementSource,
    int LigneSource);

public record ArbreSourceSheetRow(
    int LigneSource,
    string SigleEntite,
    string LibelleEntite,
    string SigleDepartement,
    string LibelleDepartement,
    string TypeLigne,
    string TypeElementSource,
    int IdtGroupeSource,
    string LibelleUB,
    string? SigleUB,
    string? Division,
    string? CodeUB,
    string? ContenuUB);

public record CorrespondanceWorkbookData(
    string FichierSource,
    IReadOnlyList<EntiteSheetRow> Entites,
    IReadOnlyList<DepartementSheetRow> Departements,
    IReadOnlyList<EntiteDepartementSheetRow> RelationsEntiteDepartement,
    IReadOnlyList<UbImportSheetRow> UbAImporter,
    IReadOnlyList<ElementRattacheSheetRow> ElementsRattaches,
    IReadOnlyList<SansCodeUbSheetRow> SansCodeUb,
    IReadOnlyList<ArbreSourceSheetRow> ArbreSource);
