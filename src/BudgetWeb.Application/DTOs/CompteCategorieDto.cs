namespace BudgetWeb.Application.DTOs;

public record CompteCategorieDto(
    long IdCompteCategorie,
    long IdCompte,
    string NumeroCompte,
    string LibelleCompte,
    long IdCategorieCompte,
    string CategorieLibelle,
    bool CategorieActif,
    DateOnly DateDebut,
    DateOnly? DateFin,
    bool Active);

public record UpsertCompteCategorieRequest(
    long CategorieId,
    DateOnly DateDebut,
    DateOnly? DateFin);

public record CloturerCompteCategorieRequest(DateOnly DateFin);

public record ImportCompteCategorieRawRequest(
    int LigneExcel,
    string? IdCompteCategorie,
    string? DateDebut,
    string? DateFin,
    string? IdCategorieCompte,
    string? IdCompte);

public record ImportCompteCategoriesPreviewRequest(
    string? NomFichier,
    IReadOnlyList<ImportCompteCategorieRawRequest>? Lignes);

public record ImportCompteCategorieLigneDto(
    int LigneExcel,
    long? IdCompteCategorie,
    long? IdCompte,
    string Compte,
    long? IdCategorieCompte,
    string Categorie,
    string DateDebut,
    string DateFin,
    string Statut,
    string Resultat,
    string? Champ,
    string? ValeurRecue);

public record ImportCompteCategoriesResumeDto(
    int Analysees,
    int AImporter,
    int Doublons,
    int DejaExistants,
    int Erreurs,
    int ConflitsPeriode,
    int DatesSentinelleConverties);

public record ImportCompteCategoriesPreviewDto(
    string NomFichier,
    int LignesDetectees,
    IReadOnlyList<ImportCompteCategorieLigneDto> Lignes,
    ImportCompteCategoriesResumeDto Resume);

public record ImportCompteCategoriesRequest(
    string? NomFichier,
    IReadOnlyList<ImportCompteCategorieRawRequest>? Lignes);

public record ImportCompteCategoriesResultDto(
    int Analysees,
    int Importes,
    int Ignores,
    int Erreurs,
    IReadOnlyList<ImportCompteCategorieLigneDto> Details,
    ImportCompteCategoriesResumeDto Resume);
