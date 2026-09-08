namespace BudgetWeb.Application.DTOs;

public record TypeCompteDto(
    string Code,
    string Libelle,
    long IdGroupeTypeCompte,
    string GroupeLibelle,
    bool Actif,
    DateTime DateCreation,
    DateTime? DateModification);

public record CreateTypeCompteRequest(
    string Code,
    string Libelle,
    long IdGroupeTypeCompte,
    bool Actif = true);

public record UpdateTypeCompteRequest(
    string Code,
    string Libelle,
    long IdGroupeTypeCompte,
    bool Actif);
