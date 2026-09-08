namespace BudgetWeb.Application.DTOs;

public record BanqueDto(
    string IdBanque,
    string LibelleBanque,
    string? Pays,
    bool Actif,
    DateTime DateCreation,
    DateTime? DateModification);

public record CreateBanqueRequest(
    string IdBanque,
    string LibelleBanque,
    string? Pays,
    bool Actif = true);

public record UpdateBanqueRequest(
    string LibelleBanque,
    string? Pays,
    bool Actif);

public record ImportBanqueItemRequest(
    string IdBanque,
    string LibelleBanque,
    string? Pays);

public record ImportBanquesRequest(IReadOnlyList<ImportBanqueItemRequest>? Banques);

public record ImportBanqueDetailDto(string IdBanque, string Motif);

public record ImportBanquesResultDto(
    int Total,
    int Crees,
    int DejaExistantes,
    int Erreurs,
    IReadOnlyList<ImportBanqueDetailDto> Details);
