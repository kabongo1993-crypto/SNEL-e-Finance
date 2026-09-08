namespace BudgetWeb.Application.DTOs;

public record UniteBudgetaireDto(
    long IdUB,
    string CodeUB,
    string Libelle,
    long IdDepartement,
    string DepartementCode,
    string DepartementLibelle,
    long IdStructure,
    string StructureCode,
    string StructureLibelle,
    string StructureType,
    bool Actif,
    DateTime DateCreation,
    int NombrePrevisions);

public record CreateUniteBudgetaireRequest(
    string CodeUB,
    string Libelle,
    long IdDepartement,
    long IdStructure,
    bool? Actif);

public record UpdateUniteBudgetaireRequest(
    string CodeUB,
    string Libelle,
    long IdDepartement,
    long IdStructure,
    bool? Actif);
