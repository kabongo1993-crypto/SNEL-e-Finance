namespace BudgetWeb.Application.DTOs;

public record AuthStatusDto(bool NeedsBootstrap, int UtilisateurCount);

public record BootstrapAdminRequest(
    string NomUtilisateur,
    string MotDePasse,
    string Nom,
    string? Prenom,
    string? Postnom,
    string? Email);
