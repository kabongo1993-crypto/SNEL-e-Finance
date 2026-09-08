using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DetailBI;
using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public async Task<IReadOnlyDictionary<string, PrevisionBudgetaire>> GetPrevisionsBiParDetailAsync(
        long idVersion,
        long idUB,
        long idTypeBudgetBi,
        long idItemBI,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Include(p => p.ModePrevision)
            .Include(p => p.RepartitionsMensuelles)
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == idUB
                        && p.FK_TypeBudget == idTypeBudgetBi
                        && p.FK_ItemBI == idItemBI
                        && p.DetailBI != null
                        && p.DetailBI != "")
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, PrevisionBudgetaire>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var cle = DetailBILibelle.Normaliser(row.DetailBI);
            if (string.IsNullOrEmpty(cle))
                continue;
            map.TryAdd(cle, row);
        }

        return map;
    }

    public async Task<IReadOnlyDictionary<EngageBiAnnuelCle, decimal>> SumEngageBiMapsAsync(
        long idExercice,
        long idUB,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
    {
        var rows = await EngageQuery(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_ItemBI != null
                        && i.DetailBI != null
                        && i.DetailBI != "")
            .Select(i => new
            {
                IdItem = i.FK_ItemBI!.Value,
                Detail = i.DetailBI!,
                i.MontantUsd,
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(
                r => new EngageBiAnnuelCle(r.IdItem, DetailBILibelle.Normaliser(r.Detail)),
                EngageBiAnnuelCleComparer.Instance)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));
    }

    public async Task ReplaceImputationsBiForItemMoisAsync(
        long idDemande,
        long idItemBI,
        byte? mois,
        IReadOnlyList<DemandePaiementImputation> nouvelles,
        CancellationToken cancellationToken = default)
    {
        var existantes = await _context.DemandePaiementImputations
            .Where(i => i.FK_DemandePaiement == idDemande
                        && i.FK_ItemBI != null
                        && i.FK_RubriqueBudgetaire == null
                        && (i.LibelleItemAE == null || i.LibelleItemAE == ""))
            .ToListAsync(cancellationToken);

        var aRetirer = existantes
            .Where(i => i.FK_ItemBI == idItemBI && i.Mois == mois)
            .ToList();

        if (aRetirer.Count > 0)
            _context.DemandePaiementImputations.RemoveRange(aRetirer);

        if (nouvelles.Count > 0)
            _context.DemandePaiementImputations.AddRange(nouvelles);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private sealed class EngageBiAnnuelCleComparer : IEqualityComparer<EngageBiAnnuelCle>
    {
        public static readonly EngageBiAnnuelCleComparer Instance = new();

        public bool Equals(EngageBiAnnuelCle? x, EngageBiAnnuelCle? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.IdItemBI == y.IdItemBI
                   && DetailBILibelle.SontEquivalent(x.DetailBI, y.DetailBI);
        }

        public int GetHashCode(EngageBiAnnuelCle obj)
            => HashCode.Combine(
                obj.IdItemBI,
                StringComparer.OrdinalIgnoreCase.GetHashCode(DetailBILibelle.Normaliser(obj.DetailBI)));
    }
}
