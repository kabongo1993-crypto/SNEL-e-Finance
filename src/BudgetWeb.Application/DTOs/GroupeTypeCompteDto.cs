namespace BudgetWeb.Application.DTOs;

public record GroupeTypeCompteDto(
    long IdGroupeTypeCompte,
    string Libelle,
    bool Actif,
    DateTime DateCreation,
    DateTime? DateModification);

public record CreateGroupeTypeCompteRequest(
    string Libelle,
    bool Actif = true);

public record UpdateGroupeTypeCompteRequest(
    string Libelle,
    bool Actif);
