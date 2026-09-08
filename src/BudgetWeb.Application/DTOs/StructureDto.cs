namespace BudgetWeb.Application.DTOs;

public record StructureDto(
    long IdStructure,
    long? ParentId,
    string TypeStructure,
    string Code,
    string Libelle,
    bool Actif,
    DateTime DateCreation,
    string? ParentCode,
    string? ParentLibelle,
    int NombreEnfants,
    int NombreUnitesBudgetaires);

public record CreateStructureRequest(
    string TypeStructure,
    string Code,
    string Libelle,
    long? ParentId,
    bool? Actif);

public record UpdateStructureRequest(
    string TypeStructure,
    string Code,
    string Libelle,
    long? ParentId,
    bool? Actif);
