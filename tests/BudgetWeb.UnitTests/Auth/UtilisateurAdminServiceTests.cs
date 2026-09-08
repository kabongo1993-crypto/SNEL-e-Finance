using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Auth;

public class UtilisateurAdminServiceTests
{
    [Fact]
    public async Task Create_Refuse_Sans_Permission()
    {
        var service = CreateService(out _, permissions: []);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(ValidCreate()));
    }

    [Fact]
    public async Task Create_Et_Get_Avec_AdminUtilisateurs()
    {
        var service = CreateService(out var repo, permissions: [AppPermissions.AdminUtilisateurs]);
        var created = await service.CreateAsync(ValidCreate(profils: [AppRoles.ServiceDemandeur]));

        Assert.True(created.IdUtilisateur > 0);
        Assert.Contains(AppRoles.ServiceDemandeur, created.Profils);
        Assert.Contains(AppPermissions.PaiementsSoumettre, created.PermissionsEffectives);

        var listed = await service.ListAsync();
        Assert.Single(listed);
        Assert.Equal("M-100", listed[0].Matricule);
    }

    [Fact]
    public async Task Update_Fusionne_Permissions_Individuelles()
    {
        var service = CreateService(out _, permissions: [AppPermissions.AdminUtilisateurs], currentUserId: 99);
        var created = await service.CreateAsync(ValidCreate(profils: [AppRoles.GestionnaireJunior]));

        Assert.Contains(AppPermissions.PaiementsLire, created.PermissionsEffectives);
        Assert.DoesNotContain(AppPermissions.PaiementsImputerDc, created.PermissionsEffectives);

        var updated = await service.UpdateAsync(created.IdUtilisateur, new UpdateUtilisateurAdminRequest(
            created.Matricule,
            created.Nom,
            created.Postnom,
            created.Prenom,
            created.NomUtilisateur,
            created.Email,
            true,
            null,
            null,
            null,
            [AppRoles.GestionnaireJunior],
            [AppPermissions.PaiementsImputerDc, AppPermissions.PaiementsImputerAe],
            new PerimetreUtilisateurDto(false, false, [], [])));

        Assert.NotNull(updated);
        Assert.Contains(AppPermissions.PaiementsImputerDc, updated!.PermissionsIndividuelles);
        Assert.Contains(AppPermissions.PaiementsImputerDc, updated.PermissionsEffectives);
        Assert.Contains(AppPermissions.PaiementsControlerBudget, updated.PermissionsEffectives);
        var etat = updated.PermissionsComplementairesEtat.First(e => e.Code == AppPermissions.PaiementsImputerDc);
        Assert.True(etat.AccordeeIndividuellement);
        Assert.False(etat.HeriteeProfil);
        Assert.True(etat.Effective);
    }

    [Fact]
    public async Task Retirer_Permission_Individuelle()
    {
        var service = CreateService(out _, permissions: [AppPermissions.AdminUtilisateurs], currentUserId: 99);
        var created = await service.CreateAsync(ValidCreate(
            profils: [AppRoles.ChargeDp],
            individuelles: [AppPermissions.PaiementsReprendreEntite]));

        Assert.Contains(AppPermissions.PaiementsReprendreEntite, created.PermissionsEffectives);

        var updated = await service.UpdateAsync(created.IdUtilisateur, new UpdateUtilisateurAdminRequest(
            created.Matricule, created.Nom, null, null, created.NomUtilisateur, null, true,
            null, null, null,
            [AppRoles.ChargeDp],
            [],
            new PerimetreUtilisateurDto(false, false, [], [])));

        Assert.DoesNotContain(AppPermissions.PaiementsReprendreEntite, updated!.PermissionsEffectives);
        Assert.Contains(AppPermissions.PaiementsChargeDpm, updated.PermissionsEffectives);
    }

    [Fact]
    public async Task Plusieurs_Profils()
    {
        var service = CreateService(out _, permissions: [AppPermissions.AdminUtilisateurs]);
        var created = await service.CreateAsync(ValidCreate(
            profils: [AppRoles.ServiceDemandeur, AppRoles.ChargeDp]));

        Assert.Equal(2, created.Profils.Count);
        Assert.Contains(AppPermissions.PaiementsSoumettre, created.PermissionsEffectives);
        Assert.Contains(AppPermissions.PaiementsChargeDpm, created.PermissionsEffectives);
    }

    [Fact]
    public async Task SetActif_Desactive()
    {
        var service = CreateService(out _, permissions: [AppPermissions.AdminUtilisateurs], currentUserId: 99);
        var created = await service.CreateAsync(ValidCreate());
        var updated = await service.SetActifAsync(created.IdUtilisateur, false);
        Assert.False(updated!.Actif);
    }

    [Fact]
    public async Task DirecteurBudgets_Sans_AdminAll()
    {
        var perms = AppPermissions.PermissionsPourProfil(AppRoles.DirecteurBudgets);
        Assert.DoesNotContain(AppPermissions.AdminAll, perms);
        Assert.Contains(AppPermissions.PaiementsLire, perms);

        var dto = AuthService.MapUser(
            new Utilisateur { IdUtilisateur = 1, NomUtilisateur = "dir", Nom = "D", Matricule = "X", Actif = true },
            [AppRoles.DirecteurBudgets]);
        Assert.DoesNotContain(AppPermissions.AdminAll, dto.Permissions);
        Assert.Contains(AppRoles.DirecteurBudgets, dto.Roles);
    }

    [Fact]
    public async Task MapUser_Fusionne_Individuelles()
    {
        var dto = AuthService.MapUser(
            new Utilisateur { IdUtilisateur = 1, NomUtilisateur = "j", Nom = "J", Matricule = "M", Actif = true },
            [AppRoles.GestionnaireJunior],
            [AppPermissions.PaiementsImputerBi, AppPermissions.PaiementsReprendreEntite]);

        Assert.Contains(AppPermissions.PaiementsControlerBudget, dto.Permissions);
        Assert.Contains(AppPermissions.PaiementsImputerBi, dto.Permissions);
        Assert.Contains(AppPermissions.PaiementsReprendreEntite, dto.Permissions);
    }

    [Fact]
    public async Task Catalogue_Refuse_Sans_Permission()
    {
        var service = CreateService(out _, permissions: []);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetCatalogueAsync());
    }

    [Fact]
    public async Task Catalogue_Contient_Profils_Cibles_Et_Historiques()
    {
        var service = CreateService(out _, permissions: [AppPermissions.AdminProfils]);
        var cat = await service.GetCatalogueAsync();
        Assert.Contains(cat.Profils, p => p.Code == AppRoles.Demandeur && p.Historique);
        Assert.Contains(cat.Profils, p => p.Code == AppRoles.ChargeDp && !p.Historique);
        Assert.Contains(cat.Permissions, p => p.Code == AppPermissions.PaiementsReprendreEntite && p.Complementaire);
        var directeur = cat.Profils.First(p => p.Code == AppRoles.DirecteurBudgets);
        Assert.DoesNotContain(AppPermissions.AdminAll, directeur.Permissions);
    }

    [Fact]
    public async Task Perimetre_Plusieurs_Departements_Et_Ub()
    {
        var service = CreateService(out var repo, permissions: [AppPermissions.AdminUtilisateurs]);
        repo.Departements.Add(1);
        repo.Departements.Add(2);
        repo.Ubs[10] = 1;
        repo.Ubs[11] = 2;

        var created = await service.CreateAsync(ValidCreate(
            perimetre: new PerimetreUtilisateurDto(false, false, [1, 2], [10, 11])));

        Assert.Equal(2, created.Perimetre.IdDepartements.Count);
        Assert.Equal(2, created.Perimetre.IdUnitesBudgetaires.Count);
        Assert.False(created.Perimetre.TousDepartements);
    }

    [Fact]
    public async Task Perimetre_Tous_Departements_Sans_Lignes()
    {
        var service = CreateService(out _, permissions: [AppPermissions.AdminUtilisateurs]);
        var created = await service.CreateAsync(ValidCreate(
            perimetre: new PerimetreUtilisateurDto(true, true, [1, 2], [10])));

        Assert.True(created.Perimetre.TousDepartements);
        Assert.True(created.Perimetre.ToutesUnitesBudgetaires);
        Assert.Empty(created.Perimetre.IdDepartements);
        Assert.Empty(created.Perimetre.IdUnitesBudgetaires);
    }

    [Fact]
    public async Task Create_Direction_Junior_Dc_Sans_Perimetre_Applique_Tous()
    {
        var service = CreateService(out _, permissions: [AppPermissions.AdminUtilisateurs]);
        var created = await service.CreateAsync(ValidCreate(
            profils: [AppRoles.GestionnaireJuniorDc, AppRoles.GestionnaireSenior],
            perimetre: new PerimetreUtilisateurDto(false, false, [], [])));

        Assert.True(created.Perimetre.TousDepartements);
        Assert.True(created.Perimetre.ToutesUnitesBudgetaires);
    }

    [Fact]
    public async Task Create_Entite_Sans_Perimetre_N_Applique_Pas_Tous()
    {
        var service = CreateService(out _, permissions: [AppPermissions.AdminUtilisateurs]);
        var created = await service.CreateAsync(ValidCreate(
            profils: [AppRoles.ServiceDemandeur],
            perimetre: new PerimetreUtilisateurDto(false, false, [], [])));

        Assert.False(created.Perimetre.TousDepartements);
        Assert.False(created.Perimetre.ToutesUnitesBudgetaires);
    }

    [Fact]
    public async Task Perimetre_Refuse_Ub_Hors_Departement()
    {
        var service = CreateService(out var repo, permissions: [AppPermissions.AdminUtilisateurs]);
        repo.Departements.Add(1);
        repo.Ubs[10] = 2; // UB dans dept 2, périmètre dept 1 seulement

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ValidCreate(
                perimetre: new PerimetreUtilisateurDto(false, false, [1], [10]))));
    }

    [Fact]
    public void PerimetreAccess_Un_Departement()
    {
        Assert.True(PerimetreAccess.PeutAccederDepartement(false, [5], 5));
        Assert.False(PerimetreAccess.PeutAccederDepartement(false, [5], 6));
    }

    [Fact]
    public void PerimetreAccess_Tous_Departements()
    {
        Assert.True(PerimetreAccess.PeutAccederDepartement(true, [], 99));
    }

    [Fact]
    public void PerimetreAccess_Ub_Selectionnees()
    {
        Assert.True(PerimetreAccess.PeutAccederUb(false, false, [1], [10], 10, 1));
        Assert.False(PerimetreAccess.PeutAccederUb(false, false, [1], [10], 11, 1));
    }

    [Fact]
    public void PerimetreAccess_Toutes_Ub()
    {
        Assert.True(PerimetreAccess.PeutAccederUb(true, true, [], [], 42, 7));
    }

    [Fact]
    public void PerimetreAccess_Ub_Refusee_Si_Departement_Non_Autorise()
    {
        Assert.False(PerimetreAccess.PeutAccederUb(false, false, [1], [10], 10, 2));
    }

    [Fact]
    public void EffectivePermissions_Heritage_Plus_Individuel()
    {
        var effectives = EffectivePermissions.Compute(
            [AppRoles.ChargeDp],
            [AppPermissions.PaiementsReprendreEntite]);
        Assert.Contains(AppPermissions.PaiementsChargeDpm, effectives);
        Assert.Contains(AppPermissions.PaiementsReprendreEntite, effectives);
    }

    private static CreateUtilisateurAdminRequest ValidCreate(
        IReadOnlyList<string>? profils = null,
        IReadOnlyList<string>? individuelles = null,
        PerimetreUtilisateurDto? perimetre = null)
        => new(
            "M-100",
            "Kabongo",
            null,
            "Jean",
            "j.kabongo",
            "j.kabongo@snel.cd",
            "Secret123!",
            true,
            null,
            null,
            null,
            profils,
            individuelles,
            perimetre ?? new PerimetreUtilisateurDto(false, false, [], []));

    private static UtilisateurAdminService CreateService(
        out FakeUtilisateurAdminRepository repo,
        IReadOnlyList<string> permissions,
        long currentUserId = 1)
    {
        repo = new FakeUtilisateurAdminRepository();
        return new UtilisateurAdminService(repo, new FakeCurrentUser(currentUserId, permissions));
    }

    private sealed class FakeCurrentUser(long userId, IReadOnlyList<string> permissions) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public long? UserId => userId;
        public string? Username => "admin";
        public string? DisplayName => "Admin";
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => permissions;
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission)
            => permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        public long RequireUserId() => userId;
    }

    private sealed class FakeUtilisateurAdminRepository : IUtilisateurAdminRepository
    {
        private long _nextId = 1;
        private readonly Dictionary<long, Stored> _users = new();

        public HashSet<long> Departements { get; } = new();
        public Dictionary<long, long> Ubs { get; } = new(); // idUb -> idDept

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
                    s.User.IdUtilisateur,
                    s.User.Matricule,
                    s.User.Nom,
                    s.User.Postnom,
                    s.User.Prenom,
                    $"{s.User.Nom} {s.User.Prenom}".Trim(),
                    s.User.NomUtilisateur,
                    s.User.Email,
                    s.User.Actif,
                    s.User.DateDerniereConnexion,
                    s.User.FK_DepartementPrincipal,
                    null,
                    null,
                    s.Profils)).ToList());

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
                s.User.IdUtilisateur,
                s.User.Matricule,
                s.User.Nom,
                s.User.Postnom,
                s.User.Prenom,
                s.User.NomUtilisateur,
                s.User.Email,
                s.User.Actif,
                s.User.DateCreation,
                s.User.DateDerniereConnexion,
                s.User.FK_StructureOrganisationnelle,
                null,
                s.User.FK_DepartementPrincipal,
                null,
                null,
                s.User.FK_StructureService,
                null,
                s.Profils,
                fromProfils,
                s.Individuelles,
                effectives,
                etats,
                s.Perimetre));
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
                Perimetre = NormalizeStoredPerimetre(perimetre)
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
            s.Perimetre = NormalizeStoredPerimetre(perimetre);
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

        private static PerimetreUtilisateurDto NormalizeStoredPerimetre(PerimetreUtilisateurDto dto)
            => new(
                dto.TousDepartements,
                dto.ToutesUnitesBudgetaires,
                dto.TousDepartements ? [] : dto.IdDepartements.ToList(),
                dto.ToutesUnitesBudgetaires ? [] : dto.IdUnitesBudgetaires.ToList());
    }
}
