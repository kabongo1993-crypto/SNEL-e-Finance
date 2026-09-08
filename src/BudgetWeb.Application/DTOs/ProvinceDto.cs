namespace BudgetWeb.Application.DTOs;

public record ProvinceDto(
    string IdProvince,
    string Libelle,
    bool Actif,
    DateTime DateCreation,
    DateTime? DateModification);

public record CreateProvinceRequest(
    string IdProvince,
    string Libelle,
    bool Actif = true);

public record UpdateProvinceRequest(
    string IdProvince,
    string Libelle,
    bool Actif);
