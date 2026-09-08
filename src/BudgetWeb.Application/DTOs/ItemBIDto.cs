namespace BudgetWeb.Application.DTOs;

public record ItemBIDto(
    long IdItemBI,
    string CodeItem,
    string Libelle,
    long? ParentId,
    string? ParentCode,
    string? ParentLibelle,
    int Niveau,
    string? Categorie,
    bool Actif,
    DateTime DateCreation,
    int NombreEnfants,
    int NombrePrevisions);

public record ItemBINoeudDto(
    long IdItemBI,
    string CodeItem,
    string Libelle,
    int Niveau,
    string? Categorie,
    bool Actif,
    IReadOnlyList<ItemBINoeudDto> Enfants);

public record CreateItemBIRequest(
    string CodeItem,
    string Libelle,
    long? ParentId,
    string? Categorie,
    bool? Actif);

public record UpdateItemBIRequest(
    string CodeItem,
    string Libelle,
    long? ParentId,
    string? Categorie,
    bool? Actif);
