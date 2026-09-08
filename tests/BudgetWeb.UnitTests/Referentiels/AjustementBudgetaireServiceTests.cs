using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class AjustementBudgetaireServiceTests
{
    private sealed class FakeUser : ICurrentUserService
    {
        public long? UserId { get; set; } = 1;
        public string? Username => "dg.test";
        public string? DisplayName => "DG Test";
        public IReadOnlyList<string> Roles { get; set; } = [AppRoles.UserDg];
        public IReadOnlyList<string> Permissions { get; set; } = AppPermissions.Dg;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) =>
            Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
        public bool HasPermission(string permission) =>
            Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));
        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException();
    }

    private sealed class FakeRepo : IAjustementBudgetaireRepository
    {
        public List<AjustementBudgetaire> Rows { get; } = [];
        public PrevisionBudgetaire? Prevision { get; set; }
        public string? WorkflowStatut { get; set; } = StatutVersionBudgetaire.Validee;
        public ExerciceBudgetaire? ExerciceCourant { get; set; }
        public decimal AppliedMontant { get; private set; }
        public List<string> Audits { get; } = [];

        public Task<IReadOnlyList<AjustementBudgetaire>> ListAsync(
            AjustementBudgetaireQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AjustementBudgetaire>>(
                Rows.Where(r => query.Statut is null || r.Statut == query.Statut).ToList());

        public Task<AjustementBudgetaire?> GetByIdAsync(long id, CancellationToken ct = default)
            => Task.FromResult(Rows.FirstOrDefault(r => r.IdAjustement == id));

        public Task<AjustementBudgetaire?> GetTrackedAsync(long id, CancellationToken ct = default)
            => GetByIdAsync(id, ct);

        public Task<IReadOnlyList<AjustementBudgetaire>> ListByPrevisionAsync(long idPrevision, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AjustementBudgetaire>>(
                Rows.Where(r => r.FK_PrevisionBudgetaire == idPrevision).OrderBy(r => r.DateCreation).ToList());

        public Task<PrevisionBudgetaire?> GetPrevisionAsync(long idPrevision, CancellationToken ct = default)
            => Task.FromResult(Prevision?.IdPrevision == idPrevision ? Prevision : null);

        public Task<string?> GetWorkflowStatutAsync(long idVersion, long idUB, CancellationToken ct = default)
            => Task.FromResult(WorkflowStatut);

        public Task<bool> ExistsBrouillonPourPrevisionAsync(long idPrevision, long? excludeId, CancellationToken ct = default)
            => Task.FromResult(Rows.Any(r =>
                r.FK_PrevisionBudgetaire == idPrevision
                && r.Statut == StatutAjustementBudgetaire.Brouillon
                && (excludeId is null || r.IdAjustement != excludeId)));

        public Task<IReadOnlyList<PrevisionBudgetaire>> GetPrevisionsValideesUbAsync(
            long idVersion, long idUB, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PrevisionBudgetaire>>(
                Prevision is null ? [] : [Prevision]);

        public Task<IReadOnlyList<PrevisionBudgetaire>> GetPrevisionsValideesAsync(
            AjustementBudgetaireQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PrevisionBudgetaire>>(
                Prevision is null || WorkflowStatut != StatutVersionBudgetaire.Validee ? [] : [Prevision]);

        public Task<IReadOnlyDictionary<long, (decimal? FirstAncien, int NbValides, bool HasBrouillon)>> GetAjustementStatsByPrevisionAsync(
            IReadOnlyList<long> idPrevisions, CancellationToken ct = default)
        {
            var dict = idPrevisions.ToDictionary(
                id => id,
                id =>
                {
                    var valides = Rows
                        .Where(r => r.FK_PrevisionBudgetaire == id && r.Statut == StatutAjustementBudgetaire.Valide)
                        .OrderBy(r => r.DateValidation ?? r.DateCreation)
                        .ToList();
                    var first = valides.Count == 0 ? (decimal?)null : valides[0].MontantAncien;
                    var hasBrouillon = Rows.Any(r =>
                        r.FK_PrevisionBudgetaire == id && r.Statut == StatutAjustementBudgetaire.Brouillon);
                    return (first, valides.Count, hasBrouillon);
                });
            return Task.FromResult<IReadOnlyDictionary<long, (decimal?, int, bool)>>(dict);
        }

        public Task<ExerciceBudgetaire?> GetExerciceCourantAsync(CancellationToken ct = default)
            => Task.FromResult(ExerciceCourant ?? Prevision?.VersionBudgetaire.ExerciceBudgetaire);

        public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(Rows.Count);

        public Task<AjustementBudgetaire> AddAsync(AjustementBudgetaire entity, CancellationToken ct = default)
        {
            entity.IdAjustement = Rows.Count + 1;
            entity.PrevisionBudgetaire = Prevision!;
            entity.VersionBudgetaire = Prevision!.VersionBudgetaire;
            entity.UniteBudgetaire = Prevision.UniteBudgetaire;
            entity.ExerciceBudgetaire = Prevision.VersionBudgetaire.ExerciceBudgetaire;
            entity.UtilisateurCreation = new Utilisateur { IdUtilisateur = 1, NomUtilisateur = "dg", Nom = "DG" };
            Rows.Add(entity);
            return Task.FromResult(entity);
        }

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task ApplyMontantPrevisionAsync(long idPrevision, decimal montantNouveau, long idUtilisateur, CancellationToken ct = default)
        {
            AppliedMontant = montantNouveau;
            if (Prevision is not null) Prevision.MontantAnnuel = montantNouveau;
            return Task.CompletedTask;
        }

        public Task AddAuditAsync(long idUtilisateur, string operation, long idAjustement, object? anciennes, object? nouvelles, CancellationToken ct = default)
        {
            Audits.Add(operation);
            return Task.CompletedTask;
        }
    }

    private static PrevisionBudgetaire SamplePrevision(decimal montant = 1_000_000m, short annee = 2026, string statutEx = "OUVERT")
    {
        var exercice = new ExerciceBudgetaire { IdExercice = 1, Annee = annee, Statut = statutEx };
        var version = new VersionBudgetaire
        {
            IdVersion = 4,
            NumeroVersion = 1,
            Libelle = "V1",
            FK_ExerciceBudgetaire = exercice.IdExercice,
            ExerciceBudgetaire = exercice,
        };
        var dept = new Departement { IdDepartement = 1, Code = "DF", Libelle = "Direction Financière", Actif = true };
        var ub = new UniteBudgetaire { IdUB = 1, CodeUB = "UB1", Libelle = "UB Test", Departement = dept, FK_Departement = 1 };
        return new PrevisionBudgetaire
        {
            IdPrevision = 10,
            FK_VersionBudgetaire = 4,
            FK_UniteBudgetaire = 1,
            MontantAnnuel = montant,
            VersionBudgetaire = version,
            UniteBudgetaire = ub,
            TypeBudget = new TypeBudget { IdTypeBudget = 1, CodeType = "DC", Libelle = "DC" },
            RubriqueBudgetaire = new RubriqueBudgetaire { IdRB = 1, CodeRB = "00100", Libelle = "Fournitures" },
            ModePrevision = new ModePrevision { IdModePrevision = 1, CodeMode = "ANNUEL", Libelle = "Annuel" },
        };
    }

    [Fact]
    public async Task Create_Refuse_Si_Ub_Non_Validee()
    {
        var repo = new FakeRepo { Prevision = SamplePrevision(), WorkflowStatut = StatutVersionBudgetaire.Brouillon };
        var svc = new AjustementBudgetaireService(repo, new FakeUser());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateAjustementBudgetaireRequest(10, 850_000, "Coupe DG")));
    }

    [Fact]
    public async Task Create_Refuse_Sans_Permission_Dg()
    {
        var repo = new FakeRepo { Prevision = SamplePrevision() };
        var user = new FakeUser { Permissions = [] };
        var svc = new AjustementBudgetaireService(repo, user);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(new CreateAjustementBudgetaireRequest(10, 850_000, "Coupe")));
    }

    [Fact]
    public async Task Create_Puis_Valider_Applique_Montant_Et_Conserve_Historique()
    {
        var prev = SamplePrevision(1_000_000);
        var repo = new FakeRepo { Prevision = prev };
        var svc = new AjustementBudgetaireService(repo, new FakeUser());

        var created = await svc.CreateAsync(new CreateAjustementBudgetaireRequest(10, 850_000, "Réduction DG"));
        Assert.Equal(StatutAjustementBudgetaire.Brouillon, created.Statut);
        Assert.Equal(-150_000m, created.Variation);
        Assert.Equal(1_000_000m, created.MontantAncien);

        var validated = await svc.ValiderAsync(created.IdAjustement);
        Assert.Equal(StatutAjustementBudgetaire.Valide, validated.Statut);
        Assert.Equal(850_000m, repo.AppliedMontant);
        Assert.Equal(850_000m, prev.MontantAnnuel);

        var histo = await svc.GetHistoriqueLigneAsync(10);
        Assert.Equal(1_000_000m, histo.MontantInitial);
        Assert.Equal(850_000m, histo.MontantActuel);
        Assert.Single(histo.Ajustements.Where(a => a.Statut == StatutAjustementBudgetaire.Valide));
    }

    [Fact]
    public async Task Ajustements_Successifs_Recalculent_Variation()
    {
        var prev = SamplePrevision(1_000_000);
        var repo = new FakeRepo { Prevision = prev };
        var svc = new AjustementBudgetaireService(repo, new FakeUser());

        var a1 = await svc.CreateAsync(new CreateAjustementBudgetaireRequest(10, 850_000, "A1"));
        await svc.ValiderAsync(a1.IdAjustement);

        var a2 = await svc.CreateAsync(new CreateAjustementBudgetaireRequest(10, 900_000, "A2"));
        Assert.Equal(850_000m, a2.MontantAncien);
        Assert.Equal(50_000m, a2.Variation);
        await svc.ValiderAsync(a2.IdAjustement);

        var histo = await svc.GetHistoriqueLigneAsync(10);
        Assert.Equal(1_000_000m, histo.MontantInitial);
        Assert.Equal(900_000m, histo.MontantActuel);
        Assert.Equal(2, histo.Ajustements.Count(a => a.Statut == StatutAjustementBudgetaire.Valide));
    }

    [Fact]
    public async Task Create_Refuse_Si_Exercice_Non_Courant()
    {
        var prev = SamplePrevision(1_000_000, annee: 2024, statutEx: "CLOTURE");
        prev.VersionBudgetaire.FK_ExerciceBudgetaire = 99;
        prev.VersionBudgetaire.ExerciceBudgetaire = new ExerciceBudgetaire
        {
            IdExercice = 99,
            Annee = 2024,
            Statut = StatutExerciceBudgetaire.Cloture,
        };
        var repo = new FakeRepo
        {
            Prevision = prev,
            ExerciceCourant = new ExerciceBudgetaire { IdExercice = 1, Annee = 2026, Statut = StatutExerciceBudgetaire.Ouvert },
        };
        var svc = new AjustementBudgetaireService(repo, new FakeUser());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(new CreateAjustementBudgetaireRequest(10, 850_000, "Coupe")));
        Assert.Contains("exercice courant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LignesDisponibles_Retourne_Uniquement_Ub_Validees()
    {
        var prev = SamplePrevision(1_000_000);
        var repo = new FakeRepo
        {
            Prevision = prev,
            WorkflowStatut = StatutVersionBudgetaire.Validee,
            ExerciceCourant = prev.VersionBudgetaire.ExerciceBudgetaire,
        };
        var svc = new AjustementBudgetaireService(repo, new FakeUser());
        var lignes = await svc.GetLignesDisponiblesAsync(new AjustementBudgetaireQuery());
        Assert.Single(lignes);
        Assert.True(lignes[0].EstExerciceCourant);
        Assert.Equal("Direction Financière", lignes[0].LibelleDepartement);

        repo.WorkflowStatut = StatutVersionBudgetaire.Brouillon;
        var empty = await svc.GetLignesDisponiblesAsync(new AjustementBudgetaireQuery());
        Assert.Empty(empty);
    }

    [Fact]
    public void MapUser_Dg_Recoit_Permissions_Ajustement()
    {
        var u = new Utilisateur
        {
            IdUtilisateur = 2,
            NomUtilisateur = "dg.snel",
            Nom = "Direction",
            Matricule = AppRoles.MatriculeDg,
            Actif = true,
        };
        var dto = AuthService.MapUser(u);
        Assert.Contains(AppRoles.UserDg, dto.Roles);
        Assert.Contains(AppPermissions.AjustementsEcrire, dto.Permissions);
        Assert.Contains(AppPermissions.AjustementsValider, dto.Permissions);
    }

    [Fact]
    public void MapUser_AdminFull_Inclut_Ajustements()
    {
        var u = new Utilisateur
        {
            IdUtilisateur = 1,
            NomUtilisateur = "admin.snel",
            Nom = "Admin",
            Matricule = AppRoles.MatriculeAdminFull,
            Actif = true,
        };
        var dto = AuthService.MapUser(u);
        Assert.Contains(AppPermissions.AjustementsEcrire, dto.Permissions);
    }
}
