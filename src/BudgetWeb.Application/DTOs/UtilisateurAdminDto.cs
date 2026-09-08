namespace BudgetWeb.Application.DTOs;

public record UtilisateurAdminListItemDto(
    long IdUtilisateur,
    string Matricule,
    string Nom,
    string? Postnom,
    string? Prenom,
    string NomComplet,
    string NomUtilisateur,
    string? Email,
    bool Actif,
    DateTime? DateDerniereConnexion,
    long? IdDepartementPrincipal,
    string? CodeDepartementPrincipal,
    string? LibelleDepartementPrincipal,
    IReadOnlyList<string> Profils);

public record UtilisateurAdminDetailDto(
    long IdUtilisateur,
    string Matricule,
    string Nom,
    string? Postnom,
    string? Prenom,
    string NomUtilisateur,
    string? Email,
    bool Actif,
    DateTime DateCreation,
    DateTime? DateDerniereConnexion,
    long? IdStructureOrganisationnelle,
    string? LibelleStructure,
    long? IdDepartementPrincipal,
    string? CodeDepartementPrincipal,
    string? LibelleDepartementPrincipal,
    long? IdStructureService,
    string? LibelleService,
    IReadOnlyList<string> Profils,
    IReadOnlyList<string> PermissionsProfils,
    IReadOnlyList<string> PermissionsIndividuelles,
    IReadOnlyList<string> PermissionsEffectives,
    IReadOnlyList<PermissionEtatDto> PermissionsComplementairesEtat,
    PerimetreUtilisateurDto Perimetre);

public record PermissionEtatDto(
    string Code,
    string? Description,
    bool HeriteeProfil,
    bool AccordeeIndividuellement,
    bool Effective);

public record PerimetreUtilisateurDto(
    bool TousDepartements,
    bool ToutesUnitesBudgetaires,
    IReadOnlyList<long> IdDepartements,
    IReadOnlyList<long> IdUnitesBudgetaires);

public record CreateUtilisateurAdminRequest(
    string Matricule,
    string Nom,
    string? Postnom,
    string? Prenom,
    string NomUtilisateur,
    string? Email,
    string MotDePasseInitial,
    bool Actif,
    long? IdStructureOrganisationnelle,
    long? IdDepartementPrincipal,
    long? IdStructureService,
    IReadOnlyList<string>? Profils,
    IReadOnlyList<string>? PermissionsIndividuelles,
    PerimetreUtilisateurDto? Perimetre);

public record UpdateUtilisateurAdminRequest(
    string Matricule,
    string Nom,
    string? Postnom,
    string? Prenom,
    string NomUtilisateur,
    string? Email,
    bool Actif,
    long? IdStructureOrganisationnelle,
    long? IdDepartementPrincipal,
    long? IdStructureService,
    IReadOnlyList<string>? Profils,
    IReadOnlyList<string>? PermissionsIndividuelles,
    PerimetreUtilisateurDto? Perimetre);

public record ResetMotDePasseAdminRequest(string NouveauMotDePasse);

public record SetActifUtilisateurRequest(bool Actif);

public record ProfilCatalogueItemDto(
    string Code,
    string Libelle,
    bool Historique,
    IReadOnlyList<string> Permissions);

public record PermissionCatalogueItemDto(
    string Code,
    string? Description,
    bool Complementaire);

public record ProfilsCatalogueDto(
    IReadOnlyList<ProfilCatalogueItemDto> Profils,
    IReadOnlyList<PermissionCatalogueItemDto> Permissions);
