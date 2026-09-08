using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using BudgetWeb.UnitTests.DemandePaiement;
using Xunit;

namespace BudgetWeb.UnitTests.Auth;

/// <summary>
/// Validation de bout en bout du CRUD Utilisateur :
/// Rôle(s) + permissions individuelles + périmètre → détail → MapUser → effet DPM.
/// </summary>
public class UtilisateurAdminCrudE2ETests
{
    [Fact]
    public async Task E2E_Entite_Initiatrice_Service_Perimetre_Cible()
    {
        var admin = CreateAdmin(out var repo);
        repo.Departements.Add(1);
        repo.Ubs[10] = 1;
        repo.Ubs[11] = 1;

        var created = await admin.CreateAsync(new CreateUtilisateurAdminRequest(
            "EI-001", "Mbala", null, "Alice", "a.mbala", "a.mbala@snel.cd",
            "Secret123!", true, null, null, null,
            [AppRoles.ServiceDemandeur],
            [],
            new PerimetreUtilisateurDto(false, false, [1], [10])));

        Assert.Contains(AppRoles.ServiceDemandeur, created.Profils);
        Assert.False(created.Perimetre.TousDepartements);
        Assert.Equal([1], created.Perimetre.IdDepartements.ToList());
        Assert.Equal([10], created.Perimetre.IdUnitesBudgetaires.ToList());
        Assert.Contains(AppPermissions.PaiementsEcrire, created.PermissionsEffectives);
        Assert.Contains(AppPermissions.PaiementsSoumettre, created.PermissionsEffectives);
        Assert.DoesNotContain(AppPermissions.PaiementsChargeDpm, created.PermissionsEffectives);
        Assert.True(PerimetreAccess.EstConfigure(ToSnapshot(created.Perimetre)));

        var got = await admin.GetByIdAsync(created.IdUtilisateur);
        Assert.NotNull(got);
        Assert.Equal(created.Profils, got!.Profils);
        Assert.Equal(created.Perimetre.IdUnitesBudgetaires, got.Perimetre.IdUnitesBudgetaires);

        var auth = AuthService.MapUser(
            new Utilisateur
            {
                IdUtilisateur = created.IdUtilisateur,
                NomUtilisateur = created.NomUtilisateur,
                Nom = created.Nom,
                Matricule = created.Matricule,
                Actif = true
            },
            got.Profils,
            got.PermissionsIndividuelles);
        Assert.Contains(AppRoles.ServiceDemandeur, auth.Roles);
        Assert.Contains(AppPermissions.PaiementsSoumettre, auth.Permissions);

        // Effet DPM : périmètre cible autorise UB 10, refuseuse UB 11
        var dpmRepo = new FakeDemandePaiementRepo
        {
            Perimetre = ToSnapshot(got.Perimetre)
        };
        dpmRepo.UbDepartements[10] = 1;
        dpmRepo.UbDepartements[11] = 1;
        dpmRepo.UbProxyPrevision.Clear();
        Assert.True(await dpmRepo.UtilisateurPeutAccederUbAsync(created.IdUtilisateur, 10));
        Assert.False(await dpmRepo.UtilisateurPeutAccederUbAsync(created.IdUtilisateur, 11));
    }

    [Fact]
    public async Task E2E_Direction_Junior_Imputer_Et_Perimetre_Tous()
    {
        var admin = CreateAdmin(out _);

        var created = await admin.CreateAsync(new CreateUtilisateurAdminRequest(
            "DB-JUN-01", "Ilunga", null, "Bob", "b.ilunga", null,
            "Secret123!", true, null, null, null,
            [AppRoles.GestionnaireJunior],
            [AppPermissions.PaiementsImputerDc],
            new PerimetreUtilisateurDto(true, true, [], [])));

        Assert.Contains(AppRoles.GestionnaireJunior, created.Profils);
        Assert.True(created.Perimetre.TousDepartements);
        Assert.True(created.Perimetre.ToutesUnitesBudgetaires);
        Assert.Empty(created.Perimetre.IdDepartements);
        Assert.Contains(AppPermissions.PaiementsImputerDc, created.PermissionsIndividuelles);
        Assert.Contains(AppPermissions.PaiementsImputerDc, created.PermissionsEffectives);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, created.PermissionsEffectives);
        Assert.DoesNotContain(AppPermissions.AdminAll, created.PermissionsEffectives);

        var auth = AuthService.MapUser(
            new Utilisateur
            {
                IdUtilisateur = created.IdUtilisateur,
                NomUtilisateur = created.NomUtilisateur,
                Nom = created.Nom,
                Matricule = created.Matricule,
                Actif = true
            },
            created.Profils,
            created.PermissionsIndividuelles);
        Assert.Contains(AppPermissions.PaiementsImputerDc, auth.Permissions);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, auth.Permissions);
    }

    [Fact]
    public async Task E2E_Update_Cycle_Role_Perm_Perimetre()
    {
        var admin = CreateAdmin(out var repo);
        repo.Departements.Add(1);
        repo.Departements.Add(2);
        repo.Ubs[10] = 1;
        repo.Ubs[20] = 2;

        var created = await admin.CreateAsync(new CreateUtilisateurAdminRequest(
            "UP-001", "Kalala", null, "Claire", "c.kalala", null,
            "Secret123!", true, null, null, null,
            [AppRoles.Demandeur],
            [],
            new PerimetreUtilisateurDto(false, false, [], [])));

        Assert.False(PerimetreAccess.EstConfigure(ToSnapshot(created.Perimetre)));

        // 1) Migration rôle historique → cible + périmètre
        var step1 = await admin.UpdateAsync(created.IdUtilisateur, new UpdateUtilisateurAdminRequest(
            created.Matricule, created.Nom, null, created.Prenom, created.NomUtilisateur, null, true,
            null, null, null,
            [AppRoles.ServiceDemandeur],
            [],
            new PerimetreUtilisateurDto(false, false, [1], [10])));

        Assert.NotNull(step1);
        Assert.DoesNotContain(AppRoles.Demandeur, step1!.Profils);
        Assert.Contains(AppRoles.ServiceDemandeur, step1.Profils);
        Assert.True(PerimetreAccess.EstConfigure(ToSnapshot(step1.Perimetre)));

        // 2) Promotion responsable + multi-UB
        var step2 = await admin.UpdateAsync(created.IdUtilisateur, new UpdateUtilisateurAdminRequest(
            created.Matricule, created.Nom, null, created.Prenom, created.NomUtilisateur, null, true,
            null, null, null,
            [AppRoles.ResponsableServiceDemandeur],
            [],
            new PerimetreUtilisateurDto(false, false, [1], [10])));

        Assert.Contains(AppRoles.ResponsableServiceDemandeur, step2!.Profils);
        Assert.Contains(AppPermissions.PaiementsValiderN1, step2.PermissionsEffectives);
        Assert.DoesNotContain(AppPermissions.PaiementsSoumettre, step2.PermissionsEffectives);

        // 3) Passage Direction Budgets + imputer + Tous*
        var step3 = await admin.UpdateAsync(created.IdUtilisateur, new UpdateUtilisateurAdminRequest(
            created.Matricule, created.Nom, null, created.Prenom, created.NomUtilisateur, null, true,
            null, null, null,
            [AppRoles.ChargeDp],
            [AppPermissions.PaiementsReprendreEntite],
            new PerimetreUtilisateurDto(true, true, [1], [10])));

        Assert.Contains(AppRoles.ChargeDp, step3!.Profils);
        Assert.True(step3.Perimetre.TousDepartements);
        Assert.Empty(step3.Perimetre.IdDepartements);
        Assert.Contains(AppPermissions.PaiementsReprendreEntite, step3.PermissionsIndividuelles);
        Assert.Contains(AppPermissions.PaiementsChargeDpm, step3.PermissionsEffectives);

        var reloaded = await admin.GetByIdAsync(created.IdUtilisateur);
        Assert.Equal(step3.Profils, reloaded!.Profils);
        Assert.Equal(step3.PermissionsIndividuelles, reloaded.PermissionsIndividuelles);
        Assert.True(reloaded.Perimetre.ToutesUnitesBudgetaires);
    }

    [Fact]
    public async Task E2E_Directeur_Et_Senior_Chef_Sans_Admin()
    {
        var admin = CreateAdmin(out _);

        var directeur = await admin.CreateAsync(new CreateUtilisateurAdminRequest(
            "DIR-01", "Directeur", null, "Budgets", "dir.budgets", null,
            "Secret123!", true, null, null, null,
            [AppRoles.DirecteurBudgets],
            [],
            new PerimetreUtilisateurDto(true, true, [], [])));

        Assert.DoesNotContain(AppPermissions.AdminAll, directeur.PermissionsEffectives);
        Assert.Contains(AppPermissions.VersionsValider, directeur.PermissionsEffectives);
        Assert.Contains(AppPermissions.PaiementsViserBudget, directeur.PermissionsEffectives);

        var controle = await admin.CreateAsync(new CreateUtilisateurAdminRequest(
            "CTRL-01", "Controle", null, "Split", "ctrl.split", null,
            "Secret123!", true, null, null, null,
            [AppRoles.GestionnaireSenior, AppRoles.ChefDivision],
            [],
            new PerimetreUtilisateurDto(true, true, [], [])));

        Assert.Equal(2, controle.Profils.Count);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, controle.PermissionsEffectives);
        Assert.Contains(AppPermissions.PaiementsViserBudget, controle.PermissionsEffectives);
        Assert.DoesNotContain(AppPermissions.AdminAll, controle.PermissionsEffectives);
    }

    [Fact]
    public async Task E2E_Refuse_Profil_Inconnu_Et_Permission_Non_Complementaire()
    {
        var admin = CreateAdmin(out _);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateAsync(new CreateUtilisateurAdminRequest(
                "BAD-P", "X", null, null, "bad.profil", null,
                "Secret123!", true, null, null, null,
                ["PROFIL_INEXISTANT"],
                [],
                new PerimetreUtilisateurDto(false, false, [], []))));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateAsync(new CreateUtilisateurAdminRequest(
                "BAD-A", "Y", null, null, "bad.adminall", null,
                "Secret123!", true, null, null, null,
                [AppRoles.ChargeDp],
                [AppPermissions.AdminAll],
                new PerimetreUtilisateurDto(true, true, [], []))));
    }

    [Fact]
    public async Task E2E_Catalogue_Couvre_Huit_Roles_Metier_Cibles()
    {
        var admin = CreateAdmin(out _, [AppPermissions.AdminProfils]);
        var cat = await admin.GetCatalogueAsync();

        foreach (var code in new[]
                 {
                     AppRoles.ServiceDemandeur,
                     AppRoles.ResponsableServiceDemandeur,
                     AppRoles.ResponsableEntiteInitiatrice,
                     AppRoles.ChargeDp,
                     AppRoles.GestionnaireJunior,
                     AppRoles.GestionnaireSenior,
                     AppRoles.ChefDivision,
                     AppRoles.DirecteurBudgets
                 })
        {
            Assert.Contains(cat.Profils, p => p.Code == code && !p.Historique);
        }

        foreach (var code in new[]
                 {
                     AppRoles.Demandeur,
                     AppRoles.ChargeDpm,
                     AppRoles.GestionnaireJuniorDc,
                     AppRoles.ControleBudget
                 })
        {
            Assert.Contains(cat.Profils, p => p.Code == code && p.Historique);
        }
    }

    [Fact]
    public async Task E2E_List_Et_SetActif_RoundTrip()
    {
        var admin = CreateAdmin(out _);
        var created = await admin.CreateAsync(new CreateUtilisateurAdminRequest(
            "ACT-01", "Actif", null, "User", "actif.user", null,
            "Secret123!", true, null, null, null,
            [AppRoles.ChefDivision],
            [],
            new PerimetreUtilisateurDto(true, true, [], [])));

        var list = await admin.ListAsync();
        Assert.Contains(list, u => u.IdUtilisateur == created.IdUtilisateur && u.Actif);

        var disabled = await admin.SetActifAsync(created.IdUtilisateur, false);
        Assert.False(disabled!.Actif);

        var again = await admin.GetByIdAsync(created.IdUtilisateur);
        Assert.False(again!.Actif);
    }

    private static PerimetreUtilisateurSnapshot ToSnapshot(PerimetreUtilisateurDto dto)
        => new(dto.TousDepartements, dto.ToutesUnitesBudgetaires, dto.IdDepartements, dto.IdUnitesBudgetaires);

    private static UtilisateurAdminService CreateAdmin(
        out FakeAdminRepo repo,
        IReadOnlyList<string>? permissions = null)
    {
        repo = new FakeAdminRepo();
        return new UtilisateurAdminService(
            repo,
            new FakeAdminUser(99, permissions ?? [AppPermissions.AdminUtilisateurs]));
    }

    private sealed class FakeAdminUser(long userId, IReadOnlyList<string> permissions) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public long? UserId => userId;
        public string? Username => "admin.e2e";
        public string? DisplayName => "Admin E2E";
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => permissions;
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission)
            => permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        public long RequireUserId() => userId;
    }

    private sealed class FakeAdminRepo : IUtilisateurAdminRepository
    {
        private long _nextId = 1;
        private readonly Dictionary<long, Stored> _users = new();

        public HashSet<long> Departements { get; } = new();
        public Dictionary<long, long> Ubs { get; } = new();

        private sealed class Stored
        {
            public Utilisateur User { get; init; } = null!;
            public List<string> Profils { get; set; } = [];
            public List<string> Individuelles { get; set; } = [];
            public PerimetreUtilisateurDto Perimetre { get; set; } = new(false, false, [], []);
        }

        public Task<IReadOnlyList<UtilisateurAdminListItemDto>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UtilisateurAdminListItemDto>>(
                _users.Values.Select(s => new UtilisateurAdminListItemDto(
                    s.User.IdUtilisateur, s.User.Matricule, s.User.Nom, s.User.Postnom, s.User.Prenom,
                    $"{s.User.Nom} {s.User.Prenom}".Trim(), s.User.NomUtilisateur, s.User.Email, s.User.Actif,
                    s.User.DateDerniereConnexion, s.User.FK_DepartementPrincipal, null, null, s.Profils)).ToList());

        public Task<UtilisateurAdminDetailDto?> GetDetailAsync(long idUtilisateur, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(idUtilisateur, out var s))
                return Task.FromResult<UtilisateurAdminDetailDto?>(null);

            var fromProfils = EffectivePermissions.FromProfils(s.Profils);
            var effectives = EffectivePermissions.Merge(fromProfils, s.Individuelles);
            var etats = AppPermissions.PermissionsComplementaires.Select(code =>
            {
                var heritee = fromProfils.Contains(code, StringComparer.OrdinalIgnoreCase);
                var indiv = s.Individuelles.Contains(code, StringComparer.OrdinalIgnoreCase);
                return new PermissionEtatDto(code, AppPermissions.DescriptionPermission(code), heritee, indiv, heritee || indiv);
            }).ToList();

            return Task.FromResult<UtilisateurAdminDetailDto?>(new UtilisateurAdminDetailDto(
                s.User.IdUtilisateur, s.User.Matricule, s.User.Nom, s.User.Postnom, s.User.Prenom,
                s.User.NomUtilisateur, s.User.Email, s.User.Actif, s.User.DateCreation, s.User.DateDerniereConnexion,
                s.User.FK_StructureOrganisationnelle, null, s.User.FK_DepartementPrincipal, null, null,
                s.User.FK_StructureService, null, s.Profils, fromProfils, s.Individuelles, effectives, etats, s.Perimetre));
        }

        public Task<bool> ExistsNomUtilisateurAsync(string nomUtilisateur, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.Values.Any(s =>
                s.User.NomUtilisateur == nomUtilisateur && (excludeId is null || s.User.IdUtilisateur != excludeId)));

        public Task<bool> ExistsMatriculeAsync(string matricule, long? excludeId, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.Values.Any(s =>
                s.User.Matricule == matricule && (excludeId is null || s.User.IdUtilisateur != excludeId)));

        public Task<long> CreateAsync(
            Utilisateur utilisateur,
            IReadOnlyList<string> profils,
            IReadOnlyList<string> permissionsIndividuelles,
            PerimetreUtilisateurDto perimetre,
            CancellationToken cancellationToken = default)
        {
            utilisateur.IdUtilisateur = _nextId++;
            _users[utilisateur.IdUtilisateur] = new Stored
            {
                User = utilisateur,
                Profils = profils.ToList(),
                Individuelles = permissionsIndividuelles.ToList(),
                Perimetre = Normalize(perimetre)
            };
            return Task.FromResult(utilisateur.IdUtilisateur);
        }

        public Task<bool> UpdateAsync(
            long idUtilisateur,
            Action<Utilisateur> applyIdentity,
            IReadOnlyList<string> profils,
            IReadOnlyList<string> permissionsIndividuelles,
            PerimetreUtilisateurDto perimetre,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(idUtilisateur, out var s))
                return Task.FromResult(false);
            applyIdentity(s.User);
            s.Profils = profils.ToList();
            s.Individuelles = permissionsIndividuelles.ToList();
            s.Perimetre = Normalize(perimetre);
            return Task.FromResult(true);
        }

        public Task<bool> SetActifAsync(long idUtilisateur, bool actif, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(idUtilisateur, out var s))
                return Task.FromResult(false);
            s.User.Actif = actif;
            return Task.FromResult(true);
        }

        public Task<bool> UpdateMotDePasseHashAsync(long idUtilisateur, string hash, CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(idUtilisateur, out var s))
                return Task.FromResult(false);
            s.User.MotDePasseHash = hash;
            return Task.FromResult(true);
        }

        public Task<bool> DepartementExistsAsync(long idDepartement, CancellationToken cancellationToken = default)
            => Task.FromResult(Departements.Count == 0 || Departements.Contains(idDepartement));

        public Task<bool> StructureExistsAsync(long idStructure, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> UbExistsAsync(long idUb, CancellationToken cancellationToken = default)
            => Task.FromResult(Ubs.Count == 0 || Ubs.ContainsKey(idUb));

        public Task<(long IdDepartement, bool Actif)?> GetUbDepartementAsync(long idUb, CancellationToken cancellationToken = default)
        {
            if (Ubs.TryGetValue(idUb, out var dept))
                return Task.FromResult<(long, bool)?>((dept, true));
            if (Ubs.Count == 0)
                return Task.FromResult<(long, bool)?>((1, true));
            return Task.FromResult<(long, bool)?>(null);
        }

        private static PerimetreUtilisateurDto Normalize(PerimetreUtilisateurDto dto)
            => new(
                dto.TousDepartements,
                dto.ToutesUnitesBudgetaires,
                dto.TousDepartements ? [] : dto.IdDepartements.ToList(),
                dto.ToutesUnitesBudgetaires ? [] : dto.IdUnitesBudgetaires.ToList());
    }
}
