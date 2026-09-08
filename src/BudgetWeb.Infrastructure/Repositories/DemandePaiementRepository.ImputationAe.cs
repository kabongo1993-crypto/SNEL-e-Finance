using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public async Task<IReadOnlyDictionary<long, PrevisionBudgetaire>> GetPrevisionsAeParRubriqueAsync(
        long idVersion,
        long idUB,
        long idTypeBudgetAe,
        string libelleItemAE,
        CancellationToken cancellationToken = default)
    {
        var item = (libelleItemAE ?? string.Empty).Trim();
        var rows = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Include(p => p.ModePrevision)
            .Include(p => p.RepartitionsMensuelles)
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == idUB
                        && p.FK_TypeBudget == idTypeBudgetAe
                        && p.FK_RubriqueBudgetaire != null
                        && p.LibelleItemAE == item)
            .ToListAsync(cancellationToken);

        var map = new Dictionary<long, PrevisionBudgetaire>();
        foreach (var row in rows)
        {
            var idRb = row.FK_RubriqueBudgetaire!.Value;
            map.TryAdd(idRb, row);
        }

        return map;
    }

    public async Task<IReadOnlyDictionary<EngageAeAnnuelCle, decimal>> SumEngageAeMapsAsync(
        long idExercice,
        long idUB,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
    {
        var rows = await EngageQuery(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire != null
                        && i.LibelleItemAE != null
                        && i.LibelleItemAE != "")
            .Select(i => new
            {
                IdRB = i.FK_RubriqueBudgetaire!.Value,
                Item = i.LibelleItemAE!,
                i.MontantUsd,
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new EngageAeAnnuelCle(r.IdRB, r.Item.Trim()), new EngageAeAnnuelCleComparer())
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));
    }

    public async Task ReplaceImputationsAeForItemMoisAsync(
        long idDemande,
        string libelleItemAE,
        byte? mois,
        IReadOnlyList<DemandePaiementImputation> nouvelles,
        CancellationToken cancellationToken = default)
    {
        var item = (libelleItemAE ?? string.Empty).Trim();
        var existantes = await _context.DemandePaiementImputations
            .Where(i => i.FK_DemandePaiement == idDemande
                        && i.FK_RubriqueBudgetaire != null
                        && i.LibelleItemAE != null
                        && i.LibelleItemAE != ""
                        && i.FK_ItemBI == null
                        && (i.DetailBI == null || i.DetailBI == ""))
            .ToListAsync(cancellationToken);

        var aRetirer = existantes
            .Where(i => string.Equals((i.LibelleItemAE ?? string.Empty).Trim(), item, StringComparison.OrdinalIgnoreCase)
                        && i.Mois == mois)
            .ToList();

        if (aRetirer.Count > 0)
            _context.DemandePaiementImputations.RemoveRange(aRetirer);

        if (nouvelles.Count > 0)
            _context.DemandePaiementImputations.AddRange(nouvelles);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private sealed class EngageAeAnnuelCleComparer : IEqualityComparer<EngageAeAnnuelCle>
    {
        public bool Equals(EngageAeAnnuelCle? x, EngageAeAnnuelCle? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.IdRubriqueBudgetaire == y.IdRubriqueBudgetaire
                   && string.Equals(x.LibelleItemAE, y.LibelleItemAE, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(EngageAeAnnuelCle obj)
            => HashCode.Combine(
                obj.IdRubriqueBudgetaire,
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.LibelleItemAE ?? string.Empty));
    }
}
