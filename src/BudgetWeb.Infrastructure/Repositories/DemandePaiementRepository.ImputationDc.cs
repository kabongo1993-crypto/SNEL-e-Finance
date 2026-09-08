using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public async Task<IReadOnlyList<RubriqueBudgetaire>> ListRubriquesDcActivesAsync(
        CancellationToken cancellationToken = default)
        => await _context.RubriquesBudgetaires.AsNoTracking()
            .Where(r => r.Actif)
            .OrderBy(r => r.CodeRB)
            .ThenBy(r => r.IdRB)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<long, PrevisionBudgetaire>> GetPrevisionsDcParRubriqueAsync(
        long idVersion,
        long idUB,
        long idTypeBudgetDc,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Include(p => p.ModePrevision)
            .Include(p => p.RepartitionsMensuelles)
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == idUB
                        && p.FK_TypeBudget == idTypeBudgetDc
                        && p.FK_RubriqueBudgetaire != null)
            .ToListAsync(cancellationToken);

        var map = new Dictionary<long, PrevisionBudgetaire>();
        foreach (var row in rows)
        {
            var idRb = row.FK_RubriqueBudgetaire!.Value;
            map.TryAdd(idRb, row);
        }

        return map;
    }

    public async Task<(IReadOnlyDictionary<EngageDcMensuelCle, decimal> Mensuel, IReadOnlyDictionary<long, decimal> Annuel)>
        SumEngageDcMapsAsync(
            long idExercice,
            long idUB,
            long? excludeDemandeId,
            CancellationToken cancellationToken = default)
    {
        var rows = await EngageQuery(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire != null
                        && i.Mois != null)
            .Select(i => new
            {
                IdRB = i.FK_RubriqueBudgetaire!.Value,
                Mois = i.Mois!.Value,
                i.MontantUsd,
            })
            .ToListAsync(cancellationToken);

        var mensuel = rows
            .GroupBy(r => new EngageDcMensuelCle(r.IdRB, r.Mois))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));

        var annuel = rows
            .GroupBy(r => r.IdRB)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));

        return (mensuel, annuel);
    }

    public async Task ReplaceImputationsDcForMoisAsync(
        long idDemande,
        byte mois,
        IReadOnlyList<DemandePaiementImputation> nouvelles,
        CancellationToken cancellationToken = default)
    {
        var existantes = await _context.DemandePaiementImputations
            .Where(i => i.FK_DemandePaiement == idDemande
                        && i.Mois == mois
                        && i.FK_RubriqueBudgetaire != null
                        && string.IsNullOrWhiteSpace(i.LibelleItemAE)
                        && i.FK_ItemBI == null
                        && string.IsNullOrWhiteSpace(i.DetailBI))
            .ToListAsync(cancellationToken);

        if (existantes.Count > 0)
            _context.DemandePaiementImputations.RemoveRange(existantes);

        if (nouvelles.Count > 0)
            _context.DemandePaiementImputations.AddRange(nouvelles);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
