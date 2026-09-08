using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class ClassementAeServiceTests
{
    [Fact]
    public async Task CreateAe_AvecGroupe_CreeGroupeEtItem()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Item X", 5);
        var svc = new ClassementAeService(fake);

        await svc.ApresEcritureAeAsync(1, 10, "Item X", 5);

        Assert.Contains(fake.Lignes, l => l.TypeLigne == ClassementAeTypeLigne.Groupe && l.FK_GroupeItemAE == 5);
        Assert.Contains(fake.Lignes, l => l.TypeLigne == ClassementAeTypeLigne.Item && l.LibelleItemAE == "Item X");
        Assert.Equal(2, fake.Lignes.Count);
    }

    [Fact]
    public async Task CreateAe_SansGroupe_CreeUniquementItem()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Item X", null);
        var svc = new ClassementAeService(fake);

        await svc.ApresEcritureAeAsync(1, 10, "Item X", null);

        Assert.Single(fake.Lignes);
        Assert.Equal(ClassementAeTypeLigne.Item, fake.Lignes[0].TypeLigne);
        Assert.Null(fake.Lignes[0].FK_GroupeItemAE);
    }

    [Fact]
    public async Task PlusieursRb_MemeAction_UnSeulItem()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Item X", 5);
        fake.SeedPrevision(1, 10, "Item X", 5);
        var svc = new ClassementAeService(fake);

        await svc.ApresEcritureAeAsync(1, 10, "Item X", 5);
        await svc.ApresEcritureAeAsync(1, 10, "Item X", 5);

        Assert.Equal(1, fake.Lignes.Count(l => l.TypeLigne == ClassementAeTypeLigne.Item));
        Assert.Equal(1, fake.Lignes.Count(l => l.TypeLigne == ClassementAeTypeLigne.Groupe));
    }

    [Fact]
    public async Task DeuxGroupesDifferents_Refuse()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Item X", 5);
        var svc = new ClassementAeService(fake);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GarantirGroupeHomogeneAsync(1, 10, "Item X", 7));
        Assert.Contains("groupe différent", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeuxActions_MemeGroupe_UnGroupeDeuxItems()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Item X", 5);
        fake.SeedPrevision(1, 10, "Item Y", 5);
        var svc = new ClassementAeService(fake);

        await svc.ApresEcritureAeAsync(1, 10, "Item X", 5);
        await svc.ApresEcritureAeAsync(1, 10, "Item Y", 5);

        Assert.Equal(1, fake.Lignes.Count(l => l.TypeLigne == ClassementAeTypeLigne.Groupe));
        Assert.Equal(2, fake.Lignes.Count(l => l.TypeLigne == ClassementAeTypeLigne.Item));
    }

    [Fact]
    public async Task DeuxUb_ClassementsIndependants()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Item X", 5);
        fake.SeedPrevision(1, 20, "Item X", 5);
        var svc = new ClassementAeService(fake);

        await svc.ApresEcritureAeAsync(1, 10, "Item X", 5);
        await svc.ApresEcritureAeAsync(1, 20, "Item X", 5);

        Assert.Equal(2, fake.Lignes.Count(l => l.FK_UniteBudgetaire == 10));
        Assert.Equal(2, fake.Lignes.Count(l => l.FK_UniteBudgetaire == 20));
    }

    [Fact]
    public async Task Renommage_ConserveOrdre()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Ancien", 5);
        var svc = new ClassementAeService(fake);
        await svc.ApresEcritureAeAsync(1, 10, "Ancien", 5);
        var ordre = fake.Lignes.Single(l => l.TypeLigne == ClassementAeTypeLigne.Item).OrdreAffichage;

        fake.SeedPrevision(1, 10, "Nouveau", 5); // simule update déjà fait sur une ligne
        await svc.ApresRenommageAeAsync(1, 10, "Ancien", "Nouveau", 5);

        var item = fake.Lignes.Single(l => l.TypeLigne == ClassementAeTypeLigne.Item);
        Assert.Equal("Nouveau", item.LibelleItemAE);
        Assert.Equal(ordre, item.OrdreAffichage);
    }

    [Fact]
    public async Task ChangementGroupe_ConserveOrdreItem()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Item X", 5);
        fake.SeedPrevision(1, 10, "Item Y", 5);
        var svc = new ClassementAeService(fake);
        await svc.ApresEcritureAeAsync(1, 10, "Item X", 5);
        await svc.ApresEcritureAeAsync(1, 10, "Item Y", 5);
        var ordreXAvant = fake.Lignes.Single(l => l.LibelleItemAE == "Item X").OrdreAffichage;
        var ordreYAvant = fake.Lignes.Single(l => l.LibelleItemAE == "Item Y").OrdreAffichage;

        await svc.ApresChangementGroupeAeAsync(1, 10, "Item X", 8);

        var ordreXApres = fake.Lignes.Single(l => l.LibelleItemAE == "Item X").OrdreAffichage;
        var ordreYApres = fake.Lignes.Single(l => l.LibelleItemAE == "Item Y").OrdreAffichage;
        Assert.True(ordreXAvant < ordreYAvant);
        Assert.True(ordreXApres < ordreYApres);
        Assert.Contains(fake.Lignes, l => l.TypeLigne == ClassementAeTypeLigne.Groupe && l.FK_GroupeItemAE == 8);
        Assert.Contains(fake.Lignes, l => l.TypeLigne == ClassementAeTypeLigne.Groupe && l.FK_GroupeItemAE == 5);
        Assert.Equal(8L, fake.Previsions.Single(p => p.Libelle == "Item X").IdGroupe);
    }

    [Fact]
    public async Task SuppressionDerniereRb_SupprimeItemEtGroupeOrphelin()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Item X", 5);
        var svc = new ClassementAeService(fake);
        await svc.ApresEcritureAeAsync(1, 10, "Item X", 5);

        fake.Previsions.Clear();
        await svc.ApresSuppressionAeAsync(1, 10, "Item X");

        Assert.Empty(fake.Lignes);
    }

    [Fact]
    public async Task Reorder_Ok()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "A", null);
        fake.SeedPrevision(1, 10, "B", null);
        var svc = new ClassementAeService(fake);
        await svc.ApresEcritureAeAsync(1, 10, "A", null);
        await svc.ApresEcritureAeAsync(1, 10, "B", null);

        var ids = fake.Lignes.OrderBy(l => l.OrdreAffichage).Select(l => l.IdClassementAE).ToList();
        ids.Reverse();
        await svc.ReorderAsync(new ReorderClassementAeRequest(1, 10, ids));

        var ordered = fake.Lignes.OrderBy(l => l.OrdreAffichage).Select(l => l.LibelleItemAE).ToList();
        Assert.Equal(["B", "A"], ordered);
    }

    [Fact]
    public async Task Reorder_PermutationInvalide_Refuse()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "A", null);
        var svc = new ClassementAeService(fake);
        await svc.ApresEcritureAeAsync(1, 10, "A", null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ReorderAsync(new ReorderClassementAeRequest(1, 10, [999])));
        Assert.Contains("permutation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Initialisation_Idempotente()
    {
        var fake = new FakeClassementRepo();
        fake.SeedPrevision(1, 10, "Item X", 5);
        var svc = new ClassementAeService(fake);

        var r1 = await svc.InitialiserManquantsAsync();
        var count = fake.Lignes.Count;
        var r2 = await svc.InitialiserManquantsAsync();

        Assert.True(r1.LignesCreees >= 2);
        Assert.Equal(1, r2.CouplesDejaInitialises);
        Assert.Equal(count, fake.Lignes.Count);
    }

    private sealed class FakeClassementRepo : IClassementAeRepository
    {
        public List<Ligne> Lignes { get; } = [];
        public List<Prev> Previsions { get; } = [];
        private long _nextId = 1;

        public void SeedPrevision(long version, long ub, string libelle, long? groupe)
            => Previsions.Add(new Prev(version, ub, libelle, groupe));

        public Task<IReadOnlyList<ClassementAeLigneDto>> GetByVersionUbAsync(
            long idVersion, long idUB, CancellationToken cancellationToken = default)
        {
            var list = Lignes
                .Where(l => l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB)
                .OrderBy(l => l.OrdreAffichage)
                .Select(l => new ClassementAeLigneDto(
                    l.IdClassementAE, l.TypeLigne, l.FK_GroupeItemAE, null, l.LibelleItemAE,
                    null, null, l.OrdreAffichage))
                .ToList();
            return Task.FromResult<IReadOnlyList<ClassementAeLigneDto>>(list);
        }

        public Task<bool> ExistsAnyAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult(Lignes.Any(l => l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB));

        public Task<int> GetMaxOrdreAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult(Lignes
                .Where(l => l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB)
                .Select(l => l.OrdreAffichage)
                .DefaultIfEmpty(0)
                .Max());

        public Task<bool> ExistsItemAsync(long idVersion, long idUB, string libelleItemAE, CancellationToken cancellationToken = default)
            => Task.FromResult(Lignes.Any(l =>
                l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB
                && l.TypeLigne == ClassementAeTypeLigne.Item && l.LibelleItemAE == libelleItemAE));

        public Task<bool> ExistsGroupeAsync(long idVersion, long idUB, long idGroupeItemAE, CancellationToken cancellationToken = default)
            => Task.FromResult(Lignes.Any(l =>
                l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB
                && l.TypeLigne == ClassementAeTypeLigne.Groupe && l.FK_GroupeItemAE == idGroupeItemAE));

        public async Task EnsureGroupeAsync(long idVersion, long idUB, long idGroupeItemAE, CancellationToken cancellationToken = default)
        {
            if (await ExistsGroupeAsync(idVersion, idUB, idGroupeItemAE, cancellationToken)) return;
            var max = await GetMaxOrdreAsync(idVersion, idUB, cancellationToken);
            Lignes.Add(new Ligne(_nextId++, idVersion, idUB, ClassementAeTypeLigne.Groupe, idGroupeItemAE, null, max + 1));
        }

        public async Task EnsureItemAsync(long idVersion, long idUB, string libelleItemAE, CancellationToken cancellationToken = default)
        {
            if (await ExistsItemAsync(idVersion, idUB, libelleItemAE, cancellationToken)) return;
            if (!await ExistsPrevisionAeAsync(idVersion, idUB, libelleItemAE, cancellationToken))
                throw new InvalidOperationException("orphelin");
            var max = await GetMaxOrdreAsync(idVersion, idUB, cancellationToken);
            Lignes.Add(new Ligne(_nextId++, idVersion, idUB, ClassementAeTypeLigne.Item, null, libelleItemAE, max + 1));
        }

        public Task RenameItemAsync(long idVersion, long idUB, string ancienLibelle, string nouveauLibelle, CancellationToken cancellationToken = default)
        {
            foreach (var l in Lignes.Where(l =>
                         l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB
                         && l.TypeLigne == ClassementAeTypeLigne.Item && l.LibelleItemAE == ancienLibelle))
            {
                l.LibelleItemAE = nouveauLibelle;
            }

            return Task.CompletedTask;
        }

        public async Task RemoveItemIfUnusedAsync(long idVersion, long idUB, string libelleItemAE, CancellationToken cancellationToken = default)
        {
            if (await ExistsPrevisionAeAsync(idVersion, idUB, libelleItemAE, cancellationToken)) return;
            Lignes.RemoveAll(l =>
                l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB
                && l.TypeLigne == ClassementAeTypeLigne.Item && l.LibelleItemAE == libelleItemAE);
        }

        public Task PurgeOrphanGroupesAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
        {
            var used = Previsions
                .Where(p => p.IdVersion == idVersion && p.IdUB == idUB && p.IdGroupe is > 0)
                .Select(p => p.IdGroupe!.Value)
                .ToHashSet();
            Lignes.RemoveAll(l =>
                l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB
                && l.TypeLigne == ClassementAeTypeLigne.Groupe
                && l.FK_GroupeItemAE is long g && !used.Contains(g));
            return Task.CompletedTask;
        }

        public Task ReindexAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
        {
            var ordered = Lignes
                .Where(l => l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB)
                .OrderBy(l => l.OrdreAffichage)
                .ThenBy(l => l.IdClassementAE)
                .ToList();
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].OrdreAffichage = i + 1;
            return Task.CompletedTask;
        }

        public Task ReorderAsync(long idVersion, long idUB, IReadOnlyList<long> idsOrdonnes, CancellationToken cancellationToken = default)
        {
            var scope = Lignes.Where(l => l.FK_VersionBudgetaire == idVersion && l.FK_UniteBudgetaire == idUB).ToList();
            var exist = scope.Select(l => l.IdClassementAE).OrderBy(x => x).ToList();
            var demand = idsOrdonnes.OrderBy(x => x).ToList();
            if (exist.Count != demand.Count || !exist.SequenceEqual(demand))
                throw new InvalidOperationException("permutation exacte");
            var byId = scope.ToDictionary(l => l.IdClassementAE);
            for (var i = 0; i < idsOrdonnes.Count; i++)
                byId[idsOrdonnes[i]].OrdreAffichage = i + 1;
            return Task.CompletedTask;
        }

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public Task<IReadOnlyList<long?>> GetGroupesDistinctsActionAsync(
            long idVersion, long idUB, string libelleItemAE, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<long?>>(
                Previsions.Where(p => p.IdVersion == idVersion && p.IdUB == idUB && p.Libelle == libelleItemAE)
                    .Select(p => p.IdGroupe)
                    .Distinct()
                    .ToList());

        public Task<bool> ExistsPrevisionAeAsync(long idVersion, long idUB, string libelleItemAE, CancellationToken cancellationToken = default)
            => Task.FromResult(Previsions.Any(p => p.IdVersion == idVersion && p.IdUB == idUB && p.Libelle == libelleItemAE));

        public Task<int> CountPrevisionsAeActionAsync(long idVersion, long idUB, string libelleItemAE, CancellationToken cancellationToken = default)
            => Task.FromResult(Previsions.Count(p => p.IdVersion == idVersion && p.IdUB == idUB && p.Libelle == libelleItemAE));

        public Task RenamePrevisionsAeAsync(long idVersion, long idUB, string ancienLibelle, string nouveauLibelle, CancellationToken cancellationToken = default)
        {
            foreach (var p in Previsions.Where(p => p.IdVersion == idVersion && p.IdUB == idUB && p.Libelle == ancienLibelle))
                p.Libelle = nouveauLibelle;
            return Task.CompletedTask;
        }

        public Task UpdateGroupePrevisionsAeAsync(long idVersion, long idUB, string libelleItemAE, long? idGroupeItemAE, CancellationToken cancellationToken = default)
        {
            foreach (var p in Previsions.Where(p => p.IdVersion == idVersion && p.IdUB == idUB && p.Libelle == libelleItemAE))
                p.IdGroupe = idGroupeItemAE;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<(long IdVersion, long IdUB, string LibelleItemAE, long? IdGroupe)>> GetActionsAePourInitAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(long, long, string, long?)>>(
                Previsions
                    .GroupBy(p => (p.IdVersion, p.IdUB, p.Libelle))
                    .Select(g => (g.Key.IdVersion, g.Key.IdUB, g.Key.Libelle, g.First().IdGroupe))
                    .ToList());

        public Task<string?> GetLibelleGroupeAsync(long idGroupeItemAE, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>($"G{idGroupeItemAE}");

        public sealed class Ligne
        {
            public Ligne(long id, long v, long ub, string type, long? g, string? lib, int ordre)
            {
                IdClassementAE = id;
                FK_VersionBudgetaire = v;
                FK_UniteBudgetaire = ub;
                TypeLigne = type;
                FK_GroupeItemAE = g;
                LibelleItemAE = lib;
                OrdreAffichage = ordre;
            }

            public long IdClassementAE { get; }
            public long FK_VersionBudgetaire { get; }
            public long FK_UniteBudgetaire { get; }
            public string TypeLigne { get; }
            public long? FK_GroupeItemAE { get; set; }
            public string? LibelleItemAE { get; set; }
            public int OrdreAffichage { get; set; }
        }

        public sealed class Prev(long idVersion, long idUB, string libelle, long? idGroupe)
        {
            public long IdVersion { get; } = idVersion;
            public long IdUB { get; } = idUB;
            public string Libelle { get; set; } = libelle;
            public long? IdGroupe { get; set; } = idGroupe;
        }
    }
}
