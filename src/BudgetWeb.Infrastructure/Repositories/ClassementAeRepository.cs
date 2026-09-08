using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class ClassementAeRepository : IClassementAeRepository
{
    private readonly BudgetDbContext _context;

    public ClassementAeRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ClassementAeLigneDto>> GetByVersionUbAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        var lignes = await _context.ClassementsAE.AsNoTracking()
            .Where(c => c.FK_VersionBudgetaire == idVersion && c.FK_UniteBudgetaire == idUB)
            .OrderBy(c => c.OrdreAffichage)
            .Select(c => new
            {
                c.IdClassementAE,
                c.TypeLigne,
                c.FK_GroupeItemAE,
                LibelleGroupe = c.GroupeItemAE != null ? c.GroupeItemAE.Libelle : null,
                c.LibelleItemAE,
                c.OrdreAffichage,
            })
            .ToListAsync(cancellationToken);

        var items = lignes
            .Where(l => l.TypeLigne == ClassementAeTypeLigne.Item && l.LibelleItemAE != null)
            .Select(l => l.LibelleItemAE!)
            .Distinct()
            .ToList();

        var groupesDeduits = new Dictionary<string, (long? Id, string? Libelle)>(StringComparer.Ordinal);
        if (items.Count > 0)
        {
            var prevs = await _context.PrevisionsBudgetaires.AsNoTracking()
                .Where(p => p.FK_VersionBudgetaire == idVersion
                            && p.FK_UniteBudgetaire == idUB
                            && p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation
                            && p.LibelleItemAE != null
                            && items.Contains(p.LibelleItemAE))
                .Select(p => new
                {
                    Libelle = p.LibelleItemAE!,
                    p.FK_GroupeItemAE,
                    LibelleGroupe = p.GroupeItemAE != null ? p.GroupeItemAE.Libelle : null,
                })
                .ToListAsync(cancellationToken);

            foreach (var g in prevs.GroupBy(p => p.Libelle, StringComparer.Ordinal))
            {
                var first = g.First();
                groupesDeduits[g.Key] = (first.FK_GroupeItemAE, first.LibelleGroupe);
            }
        }

        return lignes.Select(l =>
        {
            long? idDeduit = null;
            string? libDeduit = null;
            if (l.TypeLigne == ClassementAeTypeLigne.Item
                && l.LibelleItemAE is not null
                && groupesDeduits.TryGetValue(l.LibelleItemAE, out var deduit))
            {
                idDeduit = deduit.Id;
                libDeduit = deduit.Libelle;
            }

            return new ClassementAeLigneDto(
                l.IdClassementAE,
                l.TypeLigne,
                l.FK_GroupeItemAE,
                l.LibelleGroupe,
                l.LibelleItemAE,
                idDeduit,
                libDeduit,
                l.OrdreAffichage);
        }).ToList();
    }

    public Task<bool> ExistsAnyAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
        => _context.ClassementsAE.AsNoTracking()
            .AnyAsync(c => c.FK_VersionBudgetaire == idVersion && c.FK_UniteBudgetaire == idUB, cancellationToken);

    public async Task<int> GetMaxOrdreAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
    {
        var max = await _context.ClassementsAE.AsNoTracking()
            .Where(c => c.FK_VersionBudgetaire == idVersion && c.FK_UniteBudgetaire == idUB)
            .Select(c => (int?)c.OrdreAffichage)
            .MaxAsync(cancellationToken);
        return max ?? 0;
    }

    public Task<bool> ExistsItemAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default)
        => _context.ClassementsAE.AsNoTracking()
            .AnyAsync(c => c.FK_VersionBudgetaire == idVersion
                           && c.FK_UniteBudgetaire == idUB
                           && c.TypeLigne == ClassementAeTypeLigne.Item
                           && c.LibelleItemAE == libelleItemAE, cancellationToken);

    public Task<bool> ExistsGroupeAsync(
        long idVersion,
        long idUB,
        long idGroupeItemAE,
        CancellationToken cancellationToken = default)
        => _context.ClassementsAE.AsNoTracking()
            .AnyAsync(c => c.FK_VersionBudgetaire == idVersion
                           && c.FK_UniteBudgetaire == idUB
                           && c.TypeLigne == ClassementAeTypeLigne.Groupe
                           && c.FK_GroupeItemAE == idGroupeItemAE, cancellationToken);

    public async Task EnsureGroupeAsync(
        long idVersion,
        long idUB,
        long idGroupeItemAE,
        CancellationToken cancellationToken = default)
    {
        if (await ExistsGroupeAsync(idVersion, idUB, idGroupeItemAE, cancellationToken))
        {
            return;
        }

        var max = await GetMaxOrdreAsync(idVersion, idUB, cancellationToken);
        _context.ClassementsAE.Add(new ClassementAE
        {
            FK_VersionBudgetaire = idVersion,
            FK_UniteBudgetaire = idUB,
            TypeLigne = ClassementAeTypeLigne.Groupe,
            FK_GroupeItemAE = idGroupeItemAE,
            LibelleItemAE = null,
            OrdreAffichage = max + 1,
            DateCreation = DateTime.Now,
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureItemAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default)
    {
        if (await ExistsItemAsync(idVersion, idUB, libelleItemAE, cancellationToken))
        {
            return;
        }

        if (!await ExistsPrevisionAeAsync(idVersion, idUB, libelleItemAE, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Impossible de classer l'action « {libelleItemAE} » : aucune prévision AE correspondante.");
        }

        var max = await GetMaxOrdreAsync(idVersion, idUB, cancellationToken);
        _context.ClassementsAE.Add(new ClassementAE
        {
            FK_VersionBudgetaire = idVersion,
            FK_UniteBudgetaire = idUB,
            TypeLigne = ClassementAeTypeLigne.Item,
            FK_GroupeItemAE = null,
            LibelleItemAE = libelleItemAE,
            OrdreAffichage = max + 1,
            DateCreation = DateTime.Now,
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameItemAsync(
        long idVersion,
        long idUB,
        string ancienLibelle,
        string nouveauLibelle,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(ancienLibelle, nouveauLibelle, StringComparison.Ordinal))
        {
            return;
        }

        var entity = await _context.ClassementsAE
            .FirstOrDefaultAsync(c => c.FK_VersionBudgetaire == idVersion
                                      && c.FK_UniteBudgetaire == idUB
                                      && c.TypeLigne == ClassementAeTypeLigne.Item
                                      && c.LibelleItemAE == ancienLibelle, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.LibelleItemAE = nouveauLibelle;
        entity.DateModification = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveItemIfUnusedAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default)
    {
        if (await ExistsPrevisionAeAsync(idVersion, idUB, libelleItemAE, cancellationToken))
        {
            return;
        }

        var entity = await _context.ClassementsAE
            .FirstOrDefaultAsync(c => c.FK_VersionBudgetaire == idVersion
                                      && c.FK_UniteBudgetaire == idUB
                                      && c.TypeLigne == ClassementAeTypeLigne.Item
                                      && c.LibelleItemAE == libelleItemAE, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _context.ClassementsAE.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task PurgeOrphanGroupesAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
    {
        var groupesUtilises = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == idUB
                        && p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation
                        && p.FK_GroupeItemAE != null)
            .Select(p => p.FK_GroupeItemAE!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var orphelins = await _context.ClassementsAE
            .Where(c => c.FK_VersionBudgetaire == idVersion
                        && c.FK_UniteBudgetaire == idUB
                        && c.TypeLigne == ClassementAeTypeLigne.Groupe
                        && c.FK_GroupeItemAE != null
                        && !groupesUtilises.Contains(c.FK_GroupeItemAE.Value))
            .ToListAsync(cancellationToken);

        if (orphelins.Count == 0)
        {
            return;
        }

        _context.ClassementsAE.RemoveRange(orphelins);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReindexAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
    {
        var lignes = await _context.ClassementsAE
            .Where(c => c.FK_VersionBudgetaire == idVersion && c.FK_UniteBudgetaire == idUB)
            .OrderBy(c => c.OrdreAffichage)
            .ThenBy(c => c.IdClassementAE)
            .ToListAsync(cancellationToken);

        if (lignes.Count == 0)
        {
            return;
        }

        // Décalage temporaire positif (CHECK OrdreAffichage >= 1 + UX unique).
        const int tempOffset = 1_000_000;
        for (var i = 0; i < lignes.Count; i++)
        {
            lignes[i].OrdreAffichage = tempOffset + i + 1;
            lignes[i].DateModification = DateTime.Now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < lignes.Count; i++)
        {
            lignes[i].OrdreAffichage = i + 1;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(
        long idVersion,
        long idUB,
        IReadOnlyList<long> idsOrdonnes,
        CancellationToken cancellationToken = default)
    {
        var lignes = await _context.ClassementsAE
            .Where(c => c.FK_VersionBudgetaire == idVersion && c.FK_UniteBudgetaire == idUB)
            .ToListAsync(cancellationToken);

        var idsExistants = lignes.Select(l => l.IdClassementAE).OrderBy(x => x).ToList();
        var idsDemandes = idsOrdonnes.OrderBy(x => x).ToList();
        if (idsExistants.Count != idsDemandes.Count
            || !idsExistants.SequenceEqual(idsDemandes))
        {
            throw new InvalidOperationException(
                "La liste fournie n'est pas une permutation exacte du classement Version × UB.");
        }

        // Décalage temporaire positif (CHECK OrdreAffichage >= 1 + UX unique).
        const int tempOffset = 1_000_000;
        var byId = lignes.ToDictionary(l => l.IdClassementAE);
        for (var i = 0; i < idsOrdonnes.Count; i++)
        {
            byId[idsOrdonnes[i]].OrdreAffichage = tempOffset + i + 1;
            byId[idsOrdonnes[i]].DateModification = DateTime.Now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < idsOrdonnes.Count; i++)
        {
            byId[idsOrdonnes[i]].OrdreAffichage = i + 1;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<long?>> GetGroupesDistinctsActionAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default)
    {
        return await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == idUB
                        && p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation
                        && p.LibelleItemAE == libelleItemAE)
            .Select(p => p.FK_GroupeItemAE)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsPrevisionAeAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires.AsNoTracking()
            .AnyAsync(p => p.FK_VersionBudgetaire == idVersion
                           && p.FK_UniteBudgetaire == idUB
                           && p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation
                           && p.LibelleItemAE == libelleItemAE, cancellationToken);

    public Task<int> CountPrevisionsAeActionAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires.AsNoTracking()
            .CountAsync(p => p.FK_VersionBudgetaire == idVersion
                             && p.FK_UniteBudgetaire == idUB
                             && p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation
                             && p.LibelleItemAE == libelleItemAE, cancellationToken);

    public async Task RenamePrevisionsAeAsync(
        long idVersion,
        long idUB,
        string ancienLibelle,
        string nouveauLibelle,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.PrevisionsBudgetaires
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == idUB
                        && p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation
                        && p.LibelleItemAE == ancienLibelle)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.LibelleItemAE = nouveauLibelle;
            row.DateModification = DateTime.Now;
        }

        if (rows.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateGroupePrevisionsAeAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        long? idGroupeItemAE,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.PrevisionsBudgetaires
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == idUB
                        && p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation
                        && p.LibelleItemAE == libelleItemAE)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.FK_GroupeItemAE = idGroupeItemAE;
            row.DateModification = DateTime.Now;
        }

        if (rows.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<(long IdVersion, long IdUB, string LibelleItemAE, long? IdGroupe)>> GetActionsAePourInitAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation
                        && p.LibelleItemAE != null
                        && p.LibelleItemAE != "")
            .Select(p => new
            {
                p.FK_VersionBudgetaire,
                p.FK_UniteBudgetaire,
                Libelle = p.LibelleItemAE!,
                p.FK_GroupeItemAE,
                p.IdPrevision,
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => (r.FK_VersionBudgetaire, r.FK_UniteBudgetaire, r.Libelle))
            .Select(g =>
            {
                var first = g.OrderBy(x => x.IdPrevision).First();
                return (first.FK_VersionBudgetaire, first.FK_UniteBudgetaire, first.Libelle, first.FK_GroupeItemAE);
            })
            .OrderBy(x => x.Item1)
            .ThenBy(x => x.Item2)
            .ThenBy(x => x.Item3, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string?> GetLibelleGroupeAsync(long idGroupeItemAE, CancellationToken cancellationToken = default)
        => await _context.GroupesItemsAE.AsNoTracking()
            .Where(g => g.IdGroupeItemAE == idGroupeItemAE)
            .Select(g => g.Libelle)
            .FirstOrDefaultAsync(cancellationToken);
}
