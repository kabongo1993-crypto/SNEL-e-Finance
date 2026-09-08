namespace BudgetWeb.Application.DTOs;

public record LoginRequest(string NomUtilisateur, string MotDePasse);

public record AuthUserDto(
    long IdUtilisateur,
    string NomUtilisateur,
    string Nom,
    string? Prenom,
    string? Postnom,
    string? Email,
    string? Matricule,
    bool Actif,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public record LoginResponseDto(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    AuthUserDto Utilisateur);

public record AuthMeDto(AuthUserDto Utilisateur);

public record ChangePasswordRequest(
    string MotDePasseActuel,
    string NouveauMotDePasse,
    string ConfirmationMotDePasse);
