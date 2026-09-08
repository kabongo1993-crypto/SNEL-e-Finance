using BudgetWeb.Application.DTOs.Referentiels;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Interfaces.Referentiels;
using BudgetWeb.Application.Referentiels;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Referentiels;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class TauxChangeServiceTests
{
    private const decimal TauxRef = 2_450m;

    [Fact]
    public void Conventions_ConvertirVersUsd_DiviseParTauxReference()
    {
        var paire = new TauxChangeConventions.PaireCanonique("USD", "CDF");
        var usd = TauxChangeConventions.ConvertirVersUsd(245_000m, "CDF", TauxRef, paire);
        Assert.Equal(100m, usd);
    }

    [Fact]
    public void Conventions_ConvertirUsdVersCdf_MultiplieParTauxReference()
    {
        var cdf = TauxChangeConventions.ConvertirUsdVersCdf(100m, TauxRef);
        Assert.Equal(245_000m, cdf);
    }

    [Fact]
    public async Task ConvertirAsync_Bidirectional_Coherent_100Usd()
    {
        var rows = CanonRows(new DateOnly(2026, 9, 1), TauxRef);
        var service = CreateService(rows);

        var usdVersCdf = await service.ConvertirAsync(100m, "USD", "CDF", new DateOnly(2026, 9, 15));
        Assert.Equal(245_000m, usdVersCdf.MontantCible);

        var cdfVersUsd = await service.ConvertirAsync(
            usdVersCdf.MontantCible,
            "CDF",
            "USD",
            new DateOnly(2026, 9, 15));

        Assert.Equal(100m, decimal.Round(cdfVersUsd.MontantCible, 4));
        Assert.Equal(TauxRef, usdVersCdf.TauxReference);
    }

    [Fact]
    public async Task GetApplicable_Identite_UsdVersUsd()
    {
        var service = CreateService([]);
        var result = await service.GetApplicableAsync("USD", "USD", new DateOnly(2026, 8, 27));
        Assert.NotNull(result);
        Assert.True(result!.EstIdentite);
        Assert.Equal(1m, result.Taux);
        Assert.Null(result.IdTauxChange);
    }

    [Fact]
    public async Task GetApplicable_UsdVersCdf_RetourneTauxReference()
    {
        var service = CreateService(CanonRows(new DateOnly(2026, 9, 1), TauxRef));
        var result = await service.GetApplicableAsync("USD", "CDF", new DateOnly(2026, 9, 15));
        Assert.NotNull(result);
        Assert.Equal(TauxRef, result!.Taux);
        Assert.Equal(TauxRef, result.TauxReference);
        Assert.False(result.EstInverseCalcule);
    }

    [Fact]
    public async Task GetApplicable_CdfVersUsd_RetourneInverseCalcule()
    {
        var service = CreateService(CanonRows(new DateOnly(2026, 9, 1), TauxRef));
        var result = await service.GetApplicableAsync("CDF", "USD", new DateOnly(2026, 9, 15));
        Assert.NotNull(result);
        Assert.Equal(1m / TauxRef, result!.Taux);
        Assert.Equal(TauxRef, result.TauxReference);
        Assert.True(result.EstInverseCalcule);
    }

    [Fact]
    public async Task GetApplicable_HistoriqueParDate()
    {
        var rows = new List<TauxChange>
        {
            CanonRow(1, 2_400m, new DateOnly(2026, 8, 1), StatutTauxChange.Inactif),
            CanonRow(2, 2_450m, new DateOnly(2026, 9, 1), StatutTauxChange.Inactif),
            CanonRow(3, 2_500m, new DateOnly(2026, 10, 1), StatutTauxChange.Actif),
        };
        var service = CreateService(rows);

        Assert.Equal(2_400m, (await service.GetApplicableAsync("USD", "CDF", new DateOnly(2026, 8, 27)))!.TauxReference);
        Assert.Equal(2_450m, (await service.GetApplicableAsync("USD", "CDF", new DateOnly(2026, 9, 5)))!.TauxReference);
        Assert.Equal(2_500m, (await service.GetApplicableAsync("USD", "CDF", new DateOnly(2026, 10, 15)))!.TauxReference);
        Assert.Equal(1m / 2_400m, (await service.GetApplicableAsync("CDF", "USD", new DateOnly(2026, 8, 27)))!.Taux);
    }

    [Fact]
    public async Task GetApplicable_UtiliseVersionHistoriqueInactif()
    {
        var rows = new List<TauxChange>
        {
            CanonRow(1, 2_400m, new DateOnly(2026, 8, 1), StatutTauxChange.Inactif),
            CanonRow(2, 2_450m, new DateOnly(2026, 9, 1), StatutTauxChange.Actif),
        };
        var service = CreateService(rows);
        var result = await service.GetApplicableAsync("USD", "CDF", new DateOnly(2026, 8, 27));
        Assert.NotNull(result);
        Assert.Equal(StatutTauxChange.Inactif, result!.Statut);
        Assert.Equal(2_400m, result.TauxReference);
    }

    [Fact]
    public async Task ConvertirVersUsdAsync_UtiliseTauxReference()
    {
        var service = CreateService(CanonRows(new DateOnly(2026, 8, 1), 2_290m));
        var result = await service.ConvertirVersUsdAsync(2_290_000m, "CDF", new DateOnly(2026, 8, 27));
        Assert.Equal(1_000m, result.MontantUsd);
        Assert.Equal(2_290m, result.TauxConversion);
        Assert.Equal(1L, result.IdTauxChange);
    }

    [Fact]
    public async Task CreateVersion_Accepte_OrientationInverse_EtStockeTauxCanonique()
    {
        // Registre figé USD/CDF. Saisie utilisateur : 1 CDF = 2450 USD → stocké 1 USD = 1/2450 CDF.
        var rows = new List<TauxChange>();
        var service = CreateService(rows, referentielWrite: true);

        var created = await service.CreateVersionAsync(
            new CreateTauxChangeRequest("CDF", "USD", 2_450m, new DateOnly(2026, 9, 1)));

        Assert.Equal("USD", created.DeviseBase);
        Assert.Equal("CDF", created.DeviseQuote);
        Assert.Equal(1m / 2_450m, created.TauxReference);
        Assert.Equal(StatutTauxChange.Actif, created.Statut);

        var applicableCdfVersUsd = await service.GetApplicableAsync("CDF", "USD", new DateOnly(2026, 9, 15));
        Assert.NotNull(applicableCdfVersUsd);
        Assert.Equal(2_450m, decimal.Round(applicableCdfVersUsd!.Taux, 8));
        Assert.True(applicableCdfVersUsd.EstInverseCalcule);
    }

    [Fact]
    public async Task CreateVersion_Refuse_TauxNegatif()
    {
        var service = CreateService([], referentielWrite: true);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateVersionAsync(new CreateTauxChangeRequest("USD", "CDF", 0m, new DateOnly(2026, 8, 1))));
        Assert.Contains("strictement positif", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateVersion_AncienActif_DevientInactif_NouveauActif()
    {
        var rows = CanonRows(new DateOnly(2026, 8, 1), 2_400m, StatutTauxChange.Actif);
        var service = CreateService(rows, referentielWrite: true);

        var created = await service.CreateVersionAsync(
            new CreateTauxChangeRequest("USD", "CDF", 2_450m, new DateOnly(2026, 9, 1)));

        Assert.Equal(StatutTauxChange.Actif, created.Statut);
        Assert.Equal(2_450m, created.TauxReference);
        Assert.Equal(StatutTauxChange.Inactif, rows[0].Statut);
        Assert.Single(rows.Where(r => r.Statut == StatutTauxChange.Actif));
    }

    [Fact]
    public async Task CreateVersion_InactiveLegacyInverseEtCanonique()
    {
        var rows = new List<TauxChange>
        {
            LegacyRow(1, "CDF", "USD", 2_101m, new DateOnly(2026, 8, 1), StatutTauxChange.Actif),
            CanonRow(2, 2_400m, new DateOnly(2026, 8, 1), StatutTauxChange.Actif),
        };
        var service = CreateService(rows, referentielWrite: true);

        await service.CreateVersionAsync(new CreateTauxChangeRequest("USD", "CDF", 2_450m, new DateOnly(2026, 9, 1)));

        var actifs = rows.Where(r => r.Statut == StatutTauxChange.Actif).ToList();
        Assert.Single(actifs);
        Assert.Equal(2_450m, actifs[0].Taux);
        Assert.Equal(2, rows.Count(r => r.Statut == StatutTauxChange.Inactif));
    }

    [Fact]
    public async Task CreateVersion_SansConfirmation_ProposeRemplacementSiActifExiste()
    {
        var rows = CanonRows(new DateOnly(2026, 9, 1), 2_450m);
        var service = CreateService(rows, referentielWrite: true);

        var ex = await Assert.ThrowsAsync<TauxChangeRemplacementRequisException>(() =>
            service.CreateVersionAsync(new CreateTauxChangeRequest("USD", "CDF", 2_400m, new DateOnly(2026, 8, 15))));

        Assert.Equal("ECRASER", ex.Proposition.ModePropose);
        Assert.False(ex.Proposition.EstUtilise);
        Assert.Equal(2_450m, ex.Proposition.TauxReferenceExistant);
    }

    [Fact]
    public async Task CreateVersion_Confirme_EcraseSiNonUtilise()
    {
        var rows = CanonRows(new DateOnly(2026, 9, 1), 2_450m);
        var service = CreateService(rows, referentielWrite: true);

        var updated = await service.CreateVersionAsync(
            new CreateTauxChangeRequest("USD", "CDF", 2_400m, new DateOnly(2026, 9, 1), ConfirmerRemplacement: true));

        Assert.Single(rows);
        Assert.Equal(2_400m, updated.TauxReference);
        Assert.Equal(new DateOnly(2026, 9, 1), updated.DateEffet);
        Assert.Equal(StatutTauxChange.Actif, updated.Statut);
        Assert.Equal(2_400m, rows[0].Taux);
    }

    [Fact]
    public async Task CreateVersion_Confirme_ClotureEtCreeSiUtilise()
    {
        var rows = CanonRows(new DateOnly(2026, 9, 1), 2_450m);
        var repo = new FakeTauxChangeRepository(rows);
        repo.MarquerUtiliseParProcedure(1);
        var service = new TauxChangeService(
            repo,
            new FakePaireTauxChangeRepository(
            [
                new PaireTauxChange
                {
                    IdPaireTauxChange = 1,
                    DeviseBase = "USD",
                    DeviseQuote = "CDF",
                    Actif = true,
                    DateCreation = DateTime.UtcNow,
                },
            ]),
            new FakeUser([AppPermissions.ReferentielsEcrire]));

        var created = await service.CreateVersionAsync(
            new CreateTauxChangeRequest("USD", "CDF", 2_500m, new DateOnly(2026, 9, 3), ConfirmerRemplacement: true));

        Assert.Equal(2, rows.Count);
        Assert.Equal(StatutTauxChange.Inactif, rows[0].Statut);
        Assert.Equal(StatutTauxChange.Actif, created.Statut);
        Assert.Equal(2_500m, created.TauxReference);
    }

    [Fact]
    public async Task CreateVersion_Refuse_DoublonDateEffetHistoriqueSansActifConflitLibre()
    {
        // Date historique inactive + ACTIF plus recent : collision sur date inactive
        // avec date < ACTIF => remplacement requis (pas un refus dur).
        var rows = new List<TauxChange>
        {
            CanonRow(1, 2_400m, new DateOnly(2026, 8, 1), StatutTauxChange.Inactif),
            CanonRow(2, 2_450m, new DateOnly(2026, 9, 1), StatutTauxChange.Actif),
        };
        var service = CreateService(rows, referentielWrite: true);

        var ex = await Assert.ThrowsAsync<TauxChangeRemplacementRequisException>(() =>
            service.CreateVersionAsync(new CreateTauxChangeRequest("USD", "CDF", 2_500m, new DateOnly(2026, 8, 1))));

        Assert.Equal("ECRASER", ex.Proposition.ModePropose);
    }

    [Fact]
    public async Task Inactivate_Refuse_VersionCourante()
    {
        var rows = CanonRows(new DateOnly(2026, 9, 1), 2_450m);
        var service = CreateService(rows, referentielWrite: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.InactivateAsync(1, new InactivateTauxChangeRequest("Test")));

        Assert.Contains("version courante", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static List<TauxChange> CanonRows(
        DateOnly dateEffet,
        decimal taux,
        string statut = StatutTauxChange.Actif)
        => [CanonRow(1, taux, dateEffet, statut)];

    private static TauxChange CanonRow(
        long id,
        decimal taux,
        DateOnly dateEffet,
        string statut = StatutTauxChange.Actif)
        => LegacyRow(id, "USD", "CDF", taux, dateEffet, statut);

    private static TauxChange LegacyRow(
        long id,
        string source,
        string cible,
        decimal taux,
        DateOnly dateEffet,
        string statut = StatutTauxChange.Actif)
        => new()
        {
            IdTauxChange = id,
            DeviseSource = source,
            DeviseCible = cible,
            Taux = taux,
            DateEffet = dateEffet,
            Statut = statut,
            FK_UtilisateurCreation = 1,
            DateCreation = DateTime.Now,
            UtilisateurCreation = new Utilisateur { IdUtilisateur = 1, NomUtilisateur = "admin" },
        };

    private static TauxChangeService CreateService(
        List<TauxChange> rows,
        bool referentielWrite = false)
    {
        var perms = referentielWrite
            ? new List<string> { AppPermissions.ReferentielsEcrire }
            : new List<string> { AppPermissions.PaiementsLire };
        return new TauxChangeService(
            new FakeTauxChangeRepository(rows),
            new FakePaireTauxChangeRepository(
            [
                new PaireTauxChange
                {
                    IdPaireTauxChange = 1,
                    DeviseBase = "USD",
                    DeviseQuote = "CDF",
                    Actif = true,
                    DateCreation = DateTime.UtcNow,
                },
            ]),
            new FakeUser(perms));
    }

    [Fact]
    public async Task CreateVersion_EnregistreOrientationEurUsd()
    {
        var rows = new List<TauxChange>();
        var paires = new FakePaireTauxChangeRepository();
        var service = new TauxChangeService(
            new FakeTauxChangeRepository(rows),
            paires,
            new FakeUser([AppPermissions.ReferentielsEcrire]));

        var created = await service.CreateVersionAsync(
            new CreateTauxChangeRequest("EUR", "USD", 3_450m, new DateOnly(2026, 9, 1)));

        Assert.Equal("EUR", created.DeviseBase);
        Assert.Equal("USD", created.DeviseQuote);
        Assert.Equal(3_450m, created.TauxReference);
        var registre = await paires.ListActivesAsync();
        Assert.Single(registre);
        Assert.Equal("EUR/USD", $"{registre[0].DeviseBase}/{registre[0].DeviseQuote}");
    }

    private sealed class FakePaireTauxChangeRepository : IPaireTauxChangeRepository
    {
        private readonly List<PaireTauxChange> _rows;

        public FakePaireTauxChangeRepository(IEnumerable<PaireTauxChange>? seed = null)
        {
            _rows = seed?.ToList() ?? [];
        }

        public Task<IReadOnlyList<PaireTauxChange>> ListActivesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PaireTauxChange>>(_rows.Where(r => r.Actif).ToList());

        public Task<PaireTauxChange?> FindCanoniqueAsync(
            string deviseBase,
            string deviseQuote,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_rows.FirstOrDefault(r =>
                r.Actif
                && r.DeviseBase == deviseBase.Trim().ToUpperInvariant()
                && r.DeviseQuote == deviseQuote.Trim().ToUpperInvariant()));

        public Task<PaireTauxChange?> ResolveForDevisesAsync(
            string deviseA,
            string deviseB,
            CancellationToken cancellationToken = default)
        {
            var a = deviseA.Trim().ToUpperInvariant();
            var b = deviseB.Trim().ToUpperInvariant();
            return Task.FromResult(_rows.FirstOrDefault(r =>
                r.Actif && ((r.DeviseBase == a && r.DeviseQuote == b) || (r.DeviseBase == b && r.DeviseQuote == a))));
        }

        public Task<bool> DevisesActivesExistentAsync(
            string deviseBase,
            string deviseQuote,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<PaireTauxChange> CreerOuObtenirPaireCanoniqueAsync(
            string deviseBase,
            string deviseQuote,
            CancellationToken cancellationToken = default)
        {
            var b = deviseBase.Trim().ToUpperInvariant();
            var q = deviseQuote.Trim().ToUpperInvariant();
            var existing = _rows.FirstOrDefault(r =>
                r.Actif && ((r.DeviseBase == b && r.DeviseQuote == q) || (r.DeviseBase == q && r.DeviseQuote == b)));
            if (existing is not null)
                return Task.FromResult(existing);

            var created = new PaireTauxChange
            {
                IdPaireTauxChange = _rows.Count + 1,
                DeviseBase = b,
                DeviseQuote = q,
                Actif = true,
                DateCreation = DateTime.UtcNow,
            };
            _rows.Add(created);
            return Task.FromResult(created);
        }

        public Task<PaireTauxChange> AddAsync(PaireTauxChange entity, CancellationToken cancellationToken = default)
            => Task.FromResult(entity);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUser(IReadOnlyList<string> permissions) : ICurrentUserService
    {
        public long? UserId => 1;
        public string? Username => "test";
        public string? DisplayName => "Test";
        public IReadOnlyList<string> Roles { get; } = [];
        public IReadOnlyList<string> Permissions { get; } = permissions;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission) =>
            Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));
        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException();
    }

    [Fact]
    public async Task UpdateVersion_Modifie_SiNonUtilise()
    {
        var rows = CanonRows(new DateOnly(2026, 9, 1), 2_450m);
        var repo = new FakeTauxChangeRepository(rows);
        var service = new TauxChangeService(
            repo,
            new FakePaireTauxChangeRepository(),
            new FakeUser([AppPermissions.ReferentielsEcrire]));

        var updated = await service.UpdateVersionAsync(
            1,
            new UpdateTauxChangeRequest(2_500m, new DateOnly(2026, 9, 1)));

        Assert.Equal(2_500m, updated.TauxReference);
        Assert.True(updated.EstModifiable);
        Assert.Equal(2_500m, rows[0].Taux);
    }

    [Fact]
    public async Task UpdateVersion_Refuse_SiUtiliseParProcedure()
    {
        var rows = CanonRows(new DateOnly(2026, 9, 1), 2_450m);
        var repo = new FakeTauxChangeRepository(rows);
        repo.MarquerUtiliseParProcedure(1);
        var service = new TauxChangeService(
            repo,
            new FakePaireTauxChangeRepository(),
            new FakeUser([AppPermissions.ReferentielsEcrire]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateVersionAsync(1, new UpdateTauxChangeRequest(2_500m, new DateOnly(2026, 9, 1))));

        Assert.Contains("procédure", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeTauxChangeRepository(List<TauxChange> rows) : ITauxChangeRepository
    {
        private readonly HashSet<long> _idsUtilisesParProcedure = [];

        public void MarquerUtiliseParProcedure(long id) => _idsUtilisesParProcedure.Add(id);

        public Task<IReadOnlyList<TauxChange>> ListAsync(
            TauxChangeListQuery query,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<TauxChange> q = rows;
            if (!string.IsNullOrWhiteSpace(query.DeviseBase))
                q = q.Where(r => r.DeviseSource == query.DeviseBase.Trim().ToUpperInvariant());
            if (!string.IsNullOrWhiteSpace(query.DeviseQuote))
                q = q.Where(r => r.DeviseCible == query.DeviseQuote.Trim().ToUpperInvariant());
            if (!string.IsNullOrWhiteSpace(query.Statut))
                q = q.Where(r => r.Statut == StatutTauxChange.Normaliser(query.Statut));
            return Task.FromResult<IReadOnlyList<TauxChange>>(q.ToList());
        }

        public Task<TauxChange?> GetByIdAsync(long idTauxChange, CancellationToken cancellationToken = default)
            => Task.FromResult(rows.FirstOrDefault(r => r.IdTauxChange == idTauxChange));

        public Task<TauxChange?> GetByIdTrackedAsync(long idTauxChange, CancellationToken cancellationToken = default)
            => GetByIdAsync(idTauxChange, cancellationToken);

        public Task<TauxChange?> FindApplicableCanoniqueAsync(
            string deviseBase,
            string deviseQuote,
            DateOnly dateReference,
            CancellationToken cancellationToken = default)
        {
            var row = rows
                .Where(r => r.DeviseSource == deviseBase.Trim().ToUpperInvariant()
                            && r.DeviseCible == deviseQuote.Trim().ToUpperInvariant()
                            && r.DateEffet <= dateReference)
                .OrderByDescending(r => r.DateEffet)
                .ThenByDescending(r => r.IdTauxChange)
                .FirstOrDefault();
            return Task.FromResult(row);
        }

        public Task<TauxChange?> FindActifCourantCanoniqueAsync(
            string deviseBase,
            string deviseQuote,
            CancellationToken cancellationToken = default)
        {
            var row = rows
                .Where(r => r.DeviseSource == deviseBase.Trim().ToUpperInvariant()
                            && r.DeviseCible == deviseQuote.Trim().ToUpperInvariant()
                            && r.Statut == StatutTauxChange.Actif)
                .OrderByDescending(r => r.DateEffet)
                .ThenByDescending(r => r.IdTauxChange)
                .FirstOrDefault();
            return Task.FromResult(row);
        }

        public Task<bool> ExistsForCanoniqueAndDateEffetAsync(
            string deviseBase,
            string deviseQuote,
            DateOnly dateEffet,
            long? excludeIdTauxChange = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(rows.Any(r =>
                r.DeviseSource == deviseBase.Trim().ToUpperInvariant()
                && r.DeviseCible == deviseQuote.Trim().ToUpperInvariant()
                && r.DateEffet == dateEffet
                && (excludeIdTauxChange == null || r.IdTauxChange != excludeIdTauxChange)));

        public Task<bool> EstReferenceParProcedureAsync(long idTauxChange, CancellationToken cancellationToken = default)
            => Task.FromResult(_idsUtilisesParProcedure.Contains(idTauxChange));

        public Task<IReadOnlySet<long>> GetIdsReferenceParProcedureAsync(
            IEnumerable<long> idsTauxChange,
            CancellationToken cancellationToken = default)
        {
            var set = new HashSet<long>(idsTauxChange.Where(_idsUtilisesParProcedure.Contains));
            return Task.FromResult<IReadOnlySet<long>>(set);
        }

        public Task<bool> ExistsOrientationInverseAsync(
            string deviseBase,
            string deviseQuote,
            CancellationToken cancellationToken = default)
            => Task.FromResult(rows.Any(r =>
                r.DeviseSource == deviseQuote.Trim().ToUpperInvariant()
                && r.DeviseCible == deviseBase.Trim().ToUpperInvariant()));

        public Task<TauxChange> CreateVersionReplacingActifAsync(
            TauxChange newVersion,
            long userIdModification,
            CancellationToken cancellationToken = default)
        {
            foreach (var actif in rows.Where(r =>
                         r.Statut == StatutTauxChange.Actif
                         && ((r.DeviseSource == newVersion.DeviseSource && r.DeviseCible == newVersion.DeviseCible)
                             || (r.DeviseSource == newVersion.DeviseCible && r.DeviseCible == newVersion.DeviseSource))).ToList())
            {
                actif.Statut = StatutTauxChange.Inactif;
                actif.FK_UtilisateurModification = userIdModification;
                actif.DateModification = DateTime.Now;
            }

            newVersion.IdTauxChange = rows.Count == 0 ? 1 : rows.Max(r => r.IdTauxChange) + 1;
            rows.Add(newVersion);
            return Task.FromResult(newVersion);
        }

        public Task<TauxChange> AddAsync(TauxChange entity, CancellationToken cancellationToken = default)
        {
            entity.IdTauxChange = rows.Count == 0 ? 1 : rows.Max(r => r.IdTauxChange) + 1;
            rows.Add(entity);
            return Task.FromResult(entity);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
