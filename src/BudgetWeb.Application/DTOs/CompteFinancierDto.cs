namespace BudgetWeb.Application.DTOs;

public record CompteFinancierDto(
    long IdCompte,
    string NumeroCompte,
    string LibelleCompte,
    string IdBanque,
    string BanqueLibelle,
    long IdDirection,
    string DirectionLibelle,
    string CodeTypeCompte,
    string TypeCompteLibelle,
    long IdDevise,
    string DeviseCode,
    string DeviseLibelle,
    string? IdProvince,
    string? ProvinceLibelle,
    long? IdUtilisateur,
    string? UtilisateurLibelle,
    DateTime DateCreation,
    DateOnly? DateCloture,
    DateTime? DateModification,
    bool Actif);

public record CreateCompteFinancierRequest(
    string NumeroCompte,
    string LibelleCompte,
    string IdBanque,
    long IdDirection,
    string CodeTypeCompte,
    long IdDevise,
    string? IdProvince,
    long? IdUtilisateur,
    bool Actif = true);

public record UpdateCompteFinancierRequest(
    string NumeroCompte,
    string LibelleCompte,
    string IdBanque,
    long IdDirection,
    string CodeTypeCompte,
    long IdDevise,
    string? IdProvince,
    long? IdUtilisateur,
    bool Actif);

public record ImportCompteRawRequest(
    int LigneExcel,
    string? IdCompte,
    string? NumeroCompte,
    string? LibelleCompte,
    string? Direction,
    string? Banque,
    string? TypeCompte,
    string? Devise,
    string? DateCreation,
    string? DateCloture,
    string? Etat,
    string? IdtProvince);

public record ImportComptesPreviewRequest(
    string? NomFichier,
    IReadOnlyList<ImportCompteRawRequest>? Lignes);

public record ImportCompteLigneDto(
    int LigneExcel,
    long? IdCompte,
    string NumeroCompte,
    string LibelleCompte,
    string Banque,
    string TypeCompte,
    string Devise,
    string Direction,
    string? Province,
    string Statut,
    string Resultat,
    string? Champ,
    string? ValeurRecue);

public record ImportComptesResumeDto(
    int Analysees,
    int AImporter,
    int Doublons,
    int DejaExistants,
    int Erreurs);

public record ImportComptesPreviewDto(
    string NomFichier,
    int LignesDetectees,
    IReadOnlyList<ImportCompteLigneDto> Lignes,
    ImportComptesResumeDto Resume);

public record ImportComptesRequest(
    string? NomFichier,
    IReadOnlyList<ImportCompteRawRequest>? Lignes);

public record ImportComptesResultDto(
    int Analysees,
    int Importes,
    int Ignores,
    int Erreurs,
    IReadOnlyList<ImportCompteLigneDto> Details);
