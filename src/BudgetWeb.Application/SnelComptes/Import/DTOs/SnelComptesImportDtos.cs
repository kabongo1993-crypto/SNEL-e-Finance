namespace BudgetWeb.Application.SnelComptes.Import.DTOs;

public record SnelComptesExcelRow(
    int NumeroLigne,
    string Chapitre,
    string Groupe,
    string Section,
    string Code,
    string Libelle);

public record SnelComptesImportAnomalieDto(
    string Severite,
    string Code,
    string Message,
    int? NumeroLigne,
    string? CodeElement);

public record SnelComptesLigneIgnoreeDto(
    int NumeroLigne,
    string Code,
    string Libelle,
    string Motif);

public record SnelComptesRbPreviewItemDto(
    int NumeroLigne,
    string CodeExcel,
    string CodeImport,
    string Libelle,
    string? CodeParent,
    int Niveau,
    string Statut);

public record SnelComptesItemBiPreviewItemDto(
    int NumeroLigne,
    string CodeExcel,
    string CodeImport,
    string Libelle,
    string? CodeParent,
    int Niveau,
    string? Categorie,
    string Statut);

public record SnelComptesNoeudPreviewDto(
    string Code,
    string Libelle,
    int Niveau,
    string? Categorie,
    string Statut,
    IReadOnlyList<SnelComptesNoeudPreviewDto> Enfants);

public record SnelComptesCompteursDto(
    int TotalDetecte,
    int ACreer,
    int DejaExistant,
    int EnConflit,
    int Anomalies);

public record SnelComptesPreviewDto(
    string FichierSource,
    string Feuille,
    SnelComptesCompteursDto Rubriques,
    SnelComptesCompteursDto ItemsBI,
    IReadOnlyList<SnelComptesRbPreviewItemDto> RubriquesDetail,
    IReadOnlyList<SnelComptesItemBiPreviewItemDto> ItemsBIDetail,
    IReadOnlyList<SnelComptesNoeudPreviewDto> ArbreRubriques,
    IReadOnlyList<SnelComptesNoeudPreviewDto> ArbreItemsBI,
    IReadOnlyList<SnelComptesLigneIgnoreeDto> LignesIgnorees,
    IReadOnlyList<SnelComptesImportAnomalieDto> Anomalies,
    bool PeutImporter,
    string Message);

public record SnelComptesExecuteRequestDto(bool Confirm, string? FichierSource);

public record SnelComptesExecuteResultDto(
    bool Succes,
    string Message,
    int RubriquesInserees,
    int ItemsBIInserees,
    int LignesIgnorees,
    int Conflits,
    int Anomalies,
    IReadOnlyList<SnelComptesNoeudPreviewDto> ArbreRubriques,
    IReadOnlyList<SnelComptesNoeudPreviewDto> ArbreItemsBI);
