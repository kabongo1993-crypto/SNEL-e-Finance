namespace BudgetWeb.Application.DTOs;

public record DirectionDto(
    long IdDirection,
    string Libelle,
    bool Actif,
    DateTime DateCreation,
    DateTime? DateModification);

public record CreateDirectionRequest(
    string Libelle,
    bool Actif = true);

public record UpdateDirectionRequest(
    string Libelle,
    bool Actif);
