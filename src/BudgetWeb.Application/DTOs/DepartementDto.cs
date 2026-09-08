namespace BudgetWeb.Application.DTOs;

public record DepartementDto(
    long IdDepartement,
    string Code,
    string Libelle,
    bool Actif,
    DateTime DateCreation,
    int NombreUnitesBudgetaires);

public record CreateDepartementRequest(
    string Code,
    string Libelle,
    bool? Actif);

public record UpdateDepartementRequest(
    string Code,
    string Libelle,
    bool? Actif);
