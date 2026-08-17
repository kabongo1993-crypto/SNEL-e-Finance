namespace BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;

public record LigneExcelBruteDto(
    int NumeroLigne,
    string Feuille,
    IReadOnlyDictionary<string, string?> Colonnes);

public record LigneExcelNormaliseeDto(
    int NumeroLigne,
    string Feuille,
    string? EntiteCode,
    string? EntiteLibelle,
    string? DepartementSigle,
    string? DepartementLibelle,
    string? DirectionCode,
    string? DirectionLibelle,
    string? DivisionCode,
    string? DivisionLibelle,
    string? ServiceCode,
    string? ServiceLibelle,
    string? SectionCode,
    string? SectionLibelle,
    string? CodeUB,
    string? LibelleUB,
    bool EstLigneSecondaire,
    bool SansCodeUB);

public record ImportValidationIssueDto(
    Import.Enums.ImportIssueSeverity Severite,
    string Code,
    string Message,
    int? NumeroLigne,
    string? Feuille,
    string? CodeUB,
    string? EntiteCode);

public record ImportCompteursDto(
    int Entites,
    int Departements,
    int RelationsEntiteDepartement,
    int Structures,
    int UnitesBudgetaires,
    int LignesSansCodeUB,
    int LignesSource,
    int Anomalies,
    int AnomaliesCritiques);

public record EntitePreviewItemDto(string Code, string Libelle, int LignesSource);

public record DepartementPreviewItemDto(string Sigle, string Libelle, int LignesSource);

public record RelationEntiteDepartementPreviewDto(string EntiteCode, string DepartementSigle);

public record StructurePreviewItemDto(
    string CleMetier,
    string TypeStructure,
    string Code,
    string Libelle,
    string? CleMetierParent,
    int LignesSource,
    bool SansUbRattachee);

public record UniteBudgetairePreviewItemDto(
    string CodeUB,
    string Libelle,
    string DepartementSigle,
    string CleMetierStructure,
    int LignesSource);

public record ImportAnalyseResultDto(
    string FichierSource,
    IReadOnlyList<string> FeuillesAnalysees,
    ImportCompteursDto Compteurs,
    IReadOnlyList<ImportValidationIssueDto> Anomalies);

public record ImportPreviewDto(
    ImportAnalyseResultDto Analyse,
    IReadOnlyList<EntitePreviewItemDto> Entites,
    IReadOnlyList<DepartementPreviewItemDto> Departements,
    IReadOnlyList<RelationEntiteDepartementPreviewDto> RelationsEntiteDepartement,
    IReadOnlyList<StructurePreviewItemDto> Structures,
    IReadOnlyList<UniteBudgetairePreviewItemDto> UnitesBudgetaires,
    bool PeutImporter,
    string MessageConfirmation);

public record ImportExecuteRequestDto(
    string? FichierSource,
    bool Confirm);

public record ImportExecuteResultDto(
    bool Succes,
    string Message,
    ImportCompteursDto CompteursInseres,
    IReadOnlyList<ImportValidationIssueDto> Erreurs,
    TimeSpan Duree);
