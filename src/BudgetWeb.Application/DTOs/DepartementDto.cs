namespace BudgetWeb.Application.DTOs;

public record DepartementDto(
    long IdDepartement,
    string Code,
    string Libelle,
    bool Actif,
    DateTime DateCreation);

public record CreateDepartementRequest(
    string Code,
    string Libelle,
    bool? Actif);
