using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Microsoft.AspNetCore.Identity;

namespace BudgetWeb.Application.Services;

public class UtilisateurAdminService : IUtilisateurAdminService
{
    private readonly IUtilisateurAdminRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly PasswordHasher<Utilisateur> _passwordHasher = new();

    public UtilisateurAdminService(IUtilisateurAdminRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<UtilisateurAdminListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        ExigerAdminUtilisateurs();
        return await _repository.ListAsync(cancellationToken);
    }

    public async Task<UtilisateurAdminDetailDto?> GetByIdAsync(long idUtilisateur, CancellationToken cancellationToken = default)
    {
        ExigerAdminUtilisateurs();
        return await _repository.GetDetailAsync(idUtilisateur, cancellationToken);
    }

    public async Task<UtilisateurAdminDetailDto> CreateAsync(
        CreateUtilisateurAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerAdminUtilisateurs();

        var (matricule, nom, postnom, prenom, nomUtilisateur, email, actif) =
            ValiderIdentite(request.Matricule, request.Nom, request.Postnom, request.Prenom,
                request.NomUtilisateur, request.Email, request.Actif);

        var motDePasse = request.MotDePasseInitial ?? string.Empty;
        if (motDePasse.Length < 8)
            throw new InvalidOperationException("Le mot de passe initial doit contenir au moins 8 caractères.");

        if (await _repository.ExistsNomUtilisateurAsync(nomUtilisateur, null, cancellationToken))
            throw new InvalidOperationException($"Le nom d'utilisateur « {nomUtilisateur} » existe déjà.");

        if (await _repository.ExistsMatriculeAsync(matricule, null, cancellationToken))
            throw new InvalidOperationException($"Le matricule « {matricule} » existe déjà.");

        var profils = NormaliserProfils(request.Profils);
        var permissions = NormaliserPermissionsIndividuelles(request.PermissionsIndividuelles);
        var perimetre = await NormaliserPerimetreAsync(request.Perimetre, profils, cancellationToken);
        await ValiderAffectationAsync(
            request.IdStructureOrganisationnelle,
            request.IdDepartementPrincipal,
            request.IdStructureService,
            cancellationToken);

        var utilisateur = new Utilisateur
        {
            Matricule = matricule,
            Nom = nom,
            Postnom = postnom,
            Prenom = prenom,
            NomUtilisateur = nomUtilisateur,
            Email = email,
            Actif = actif,
            DateCreation = DateTime.UtcNow,
            FK_StructureOrganisationnelle = request.IdStructureOrganisationnelle,
            FK_DepartementPrincipal = request.IdDepartementPrincipal,
            FK_StructureService = request.IdStructureService
        };
        utilisateur.MotDePasseHash = _passwordHasher.HashPassword(utilisateur, motDePasse);

        var id = await _repository.CreateAsync(utilisateur, profils, permissions, perimetre, cancellationToken);
        return (await _repository.GetDetailAsync(id, cancellationToken))!;
    }

    public async Task<UtilisateurAdminDetailDto?> UpdateAsync(
        long idUtilisateur,
        UpdateUtilisateurAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerAdminUtilisateurs();
        InterdireAutoElevationCritique(idUtilisateur, request.Profils, request.PermissionsIndividuelles);

        var (matricule, nom, postnom, prenom, nomUtilisateur, email, actif) =
            ValiderIdentite(request.Matricule, request.Nom, request.Postnom, request.Prenom,
                request.NomUtilisateur, request.Email, request.Actif);

        if (await _repository.ExistsNomUtilisateurAsync(nomUtilisateur, idUtilisateur, cancellationToken))
            throw new InvalidOperationException($"Le nom d'utilisateur « {nomUtilisateur} » existe déjà.");

        if (await _repository.ExistsMatriculeAsync(matricule, idUtilisateur, cancellationToken))
            throw new InvalidOperationException($"Le matricule « {matricule} » existe déjà.");

        var profils = NormaliserProfils(request.Profils);
        var permissions = NormaliserPermissionsIndividuelles(request.PermissionsIndividuelles);
        var perimetre = await NormaliserPerimetreAsync(request.Perimetre, profils, cancellationToken);
        await ValiderAffectationAsync(
            request.IdStructureOrganisationnelle,
            request.IdDepartementPrincipal,
            request.IdStructureService,
            cancellationToken);

        var ok = await _repository.UpdateAsync(
            idUtilisateur,
            u =>
            {
                u.Matricule = matricule;
                u.Nom = nom;
                u.Postnom = postnom;
                u.Prenom = prenom;
                u.NomUtilisateur = nomUtilisateur;
                u.Email = email;
                u.Actif = actif;
                u.FK_StructureOrganisationnelle = request.IdStructureOrganisationnelle;
                u.FK_DepartementPrincipal = request.IdDepartementPrincipal;
                u.FK_StructureService = request.IdStructureService;
            },
            profils,
            permissions,
            perimetre,
            cancellationToken);

        if (!ok)
            return null;

        return await _repository.GetDetailAsync(idUtilisateur, cancellationToken);
    }

    public async Task<UtilisateurAdminDetailDto?> SetActifAsync(
        long idUtilisateur,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        ExigerAdminUtilisateurs();
        if (_currentUser.UserId == idUtilisateur && !actif)
            throw new InvalidOperationException("Vous ne pouvez pas désactiver votre propre compte.");

        var ok = await _repository.SetActifAsync(idUtilisateur, actif, cancellationToken);
        if (!ok)
            return null;
        return await _repository.GetDetailAsync(idUtilisateur, cancellationToken);
    }

    public async Task ResetMotDePasseAsync(
        long idUtilisateur,
        ResetMotDePasseAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerAdminUtilisateurs();
        var nouveau = request.NouveauMotDePasse ?? string.Empty;
        if (nouveau.Length < 8)
            throw new InvalidOperationException("Le nouveau mot de passe doit contenir au moins 8 caractères.");

        var detail = await _repository.GetDetailAsync(idUtilisateur, cancellationToken)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        var probe = new Utilisateur { IdUtilisateur = idUtilisateur, NomUtilisateur = detail.NomUtilisateur };
        var hash = _passwordHasher.HashPassword(probe, nouveau);
        var ok = await _repository.UpdateMotDePasseHashAsync(idUtilisateur, hash, cancellationToken);
        if (!ok)
            throw new InvalidOperationException("Utilisateur introuvable.");
    }

    public Task<ProfilsCatalogueDto> GetCatalogueAsync(CancellationToken cancellationToken = default)
    {
        ExigerAdminProfilsOuUtilisateurs();

        var historiques = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppRoles.Demandeur,
            AppRoles.ChargeDpm,
            AppRoles.GestionnaireJuniorDc,
            AppRoles.GestionnaireJuniorAe,
            AppRoles.GestionnaireJuniorBi,
            AppRoles.ControleBudget,
            AppRoles.Admin
        };

        var profils = AppRoles.TousLesProfils
            .Select(code => new ProfilCatalogueItemDto(
                code,
                AppPermissions.LibelleProfil(code) ?? code,
                historiques.Contains(code),
                AppPermissions.PermissionsPourProfil(code)))
            .ToList();

        var permissions = AppPermissions.Toutes
            .Select(code => new PermissionCatalogueItemDto(
                code,
                AppPermissions.DescriptionPermission(code),
                AppPermissions.EstPermissionComplementaire(code)))
            .ToList();

        return Task.FromResult(new ProfilsCatalogueDto(profils, permissions));
    }

    private void ExigerAdminUtilisateurs()
    {
        if (_currentUser.HasPermission(AppPermissions.AdminUtilisateurs)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;
        throw new UnauthorizedAccessException("Permission requise : admin.utilisateurs.");
    }

    private void ExigerAdminProfilsOuUtilisateurs()
    {
        if (_currentUser.HasPermission(AppPermissions.AdminProfils)
            || _currentUser.HasPermission(AppPermissions.AdminUtilisateurs)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;
        throw new UnauthorizedAccessException("Permission requise : admin.profils.");
    }

    private void InterdireAutoElevationCritique(
        long idUtilisateur,
        IReadOnlyList<string>? profils,
        IReadOnlyList<string>? permissions)
    {
        if (_currentUser.UserId != idUtilisateur)
            return;

        // Un admin peut éditer sa fiche, mais on refuse d'enlever son propre accès admin via cette API
        // si c'est le seul moyen — simplification : interdit de modifier ses propres profils/permissions.
        if ((profils is not null && profils.Count > 0) || (permissions is not null && permissions.Count > 0))
        {
            // Toujours autorisé pour admin.all ; la règle métier : pas d'auto-modification des droits
            // sauf si on a admin.all (administrateur système).
            if (!_currentUser.HasPermission(AppPermissions.AdminAll))
            {
                throw new InvalidOperationException(
                    "Vous ne pouvez pas modifier vos propres rôles ou permissions complémentaires.");
            }
        }
    }

    private static (string Matricule, string Nom, string? Postnom, string? Prenom, string NomUtilisateur, string? Email, bool Actif)
        ValiderIdentite(
            string? matriculeBrut,
            string? nomBrut,
            string? postnomBrut,
            string? prenomBrut,
            string? nomUtilisateurBrut,
            string? emailBrut,
            bool? actif)
    {
        var matricule = (matriculeBrut ?? string.Empty).Trim();
        var nom = (nomBrut ?? string.Empty).Trim();
        var nomUtilisateur = (nomUtilisateurBrut ?? string.Empty).Trim();
        var postnom = string.IsNullOrWhiteSpace(postnomBrut) ? null : postnomBrut.Trim();
        var prenom = string.IsNullOrWhiteSpace(prenomBrut) ? null : prenomBrut.Trim();
        var email = string.IsNullOrWhiteSpace(emailBrut) ? null : emailBrut.Trim();

        if (string.IsNullOrWhiteSpace(matricule))
            throw new InvalidOperationException("Le matricule est obligatoire.");
        if (matricule.Length > 50)
            throw new InvalidOperationException("Le matricule ne peut pas dépasser 50 caractères.");
        if (string.IsNullOrWhiteSpace(nom))
            throw new InvalidOperationException("Le nom est obligatoire.");
        if (nomUtilisateur.Length < 3)
            throw new InvalidOperationException("Le nom d'utilisateur doit contenir au moins 3 caractères.");

        return (matricule, nom, postnom, prenom, nomUtilisateur, email, actif ?? true);
    }

    private static IReadOnlyList<string> NormaliserProfils(IReadOnlyList<string>? profils)
    {
        var list = new List<string>();
        foreach (var raw in profils ?? [])
        {
            var code = (raw ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (!AppPermissions.EstProfilConnu(code))
                throw new InvalidOperationException($"Profil inconnu : {code}.");
            if (!list.Contains(code, StringComparer.OrdinalIgnoreCase))
                list.Add(code);
        }

        return list;
    }

    private static IReadOnlyList<string> NormaliserPermissionsIndividuelles(IReadOnlyList<string>? permissions)
    {
        var list = new List<string>();
        foreach (var raw in permissions ?? [])
        {
            var code = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (!AppPermissions.EstPermissionComplementaire(code))
                throw new InvalidOperationException(
                    $"La permission « {code} » ne peut pas être attribuée individuellement.");
            if (string.Equals(code, AppPermissions.AdminAll, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("admin.all ne peut pas être attribué individuellement.");
            if (!list.Contains(code, StringComparer.OrdinalIgnoreCase))
                list.Add(code);
        }

        return list;
    }

    private async Task ValiderAffectationAsync(
        long? idStructure,
        long? idDepartement,
        long? idService,
        CancellationToken cancellationToken)
    {
        if (idStructure is long s && !await _repository.StructureExistsAsync(s, cancellationToken))
            throw new InvalidOperationException("Structure / entité introuvable.");
        if (idDepartement is long d && !await _repository.DepartementExistsAsync(d, cancellationToken))
            throw new InvalidOperationException("Département principal introuvable.");
        if (idService is long svc && !await _repository.StructureExistsAsync(svc, cancellationToken))
            throw new InvalidOperationException("Service introuvable.");
    }

    private async Task<PerimetreUtilisateurDto> NormaliserPerimetreAsync(
        PerimetreUtilisateurDto? request,
        IReadOnlyList<string> profils,
        CancellationToken cancellationToken)
    {
        var dto = request ?? new PerimetreUtilisateurDto(false, false, [], []);
        dto = AppliquerPerimetreParDefautDirection(profils, dto);
        var depts = new List<long>();
        var ubs = new List<long>();

        if (dto.TousDepartements)
        {
            // Pas de lignes département.
        }
        else
        {
            foreach (var id in dto.IdDepartements.Distinct())
            {
                if (!await _repository.DepartementExistsAsync(id, cancellationToken))
                    throw new InvalidOperationException($"Département de périmètre introuvable : {id}.");
                depts.Add(id);
            }
        }

        if (dto.ToutesUnitesBudgetaires)
        {
            // Pas de lignes UB.
        }
        else
        {
            foreach (var idUb in dto.IdUnitesBudgetaires.Distinct())
            {
                var ub = await _repository.GetUbDepartementAsync(idUb, cancellationToken)
                    ?? throw new InvalidOperationException($"UB de périmètre introuvable : {idUb}.");

                if (!dto.TousDepartements
                    && !PerimetreAccess.PeutAccederDepartement(false, depts, ub.IdDepartement))
                {
                    throw new InvalidOperationException(
                        $"L'UB {idUb} n'appartient pas à un département autorisé du périmètre.");
                }

                ubs.Add(idUb);
            }
        }

        return new PerimetreUtilisateurDto(dto.TousDepartements, dto.ToutesUnitesBudgetaires, depts, ubs);
    }

    /// <summary>
    /// Rôle Direction des Budgets sans périmètre explicite → Tous départements + Toutes UB
    /// (évite le fallback historique « proxy prévision » qui masque les DPM).
    /// </summary>
    private static PerimetreUtilisateurDto AppliquerPerimetreParDefautDirection(
        IReadOnlyList<string> profils,
        PerimetreUtilisateurDto dto)
    {
        if (PerimetreAccess.EstConfigure(ToPerimetreSnapshot(dto)))
            return dto;

        if (!profils.Any(ProfilUtilisateurCodes.EstDirectionBudgets))
            return dto;

        var seed = ProfilUtilisateurCodes.PerimetreSeedDirectionBudgets();
        return new PerimetreUtilisateurDto(
            seed.TousDepartements,
            seed.ToutesUnitesBudgetaires,
            [],
            []);
    }

    private static PerimetreUtilisateurSnapshot ToPerimetreSnapshot(PerimetreUtilisateurDto dto)
        => new(
            dto.TousDepartements,
            dto.ToutesUnitesBudgetaires,
            dto.IdDepartements,
            dto.IdUnitesBudgetaires);
}
