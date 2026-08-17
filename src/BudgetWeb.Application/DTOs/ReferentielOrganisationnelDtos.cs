namespace BudgetWeb.Application.DTOs;

public record ReferentielOrganisationnelCompteursDto(
    int Entites,
    int Departements,
    int Structures,
    int UnitesBudgetaires);

public record StructureOrganisationnelleDto(
    long IdStructure,
    long? ParentId,
    string TypeStructure,
    string Code,
    string CodeTechnique,
    string Libelle,
    string? ParentCode,
    string? ParentLibelle,
    string? DepartementCode,
    string? DepartementLibelle,
    int NombreEnfants,
    int NombreUnitesBudgetaires);

public record UniteBudgetaireOrganisationDto(
    long IdUB,
    string CodeUB,
    string Libelle,
    string DepartementCode,
    string DepartementLibelle,
    long StructureId,
    string StructureCode,
    string StructureLibelle,
    string StructureType);

public record ReferentielOrganisationnelSnapshotDto(
    ReferentielOrganisationnelCompteursDto Compteurs,
    IReadOnlyList<StructureOrganisationnelleDto> Structures,
    IReadOnlyList<UniteBudgetaireOrganisationDto> UnitesBudgetaires);
