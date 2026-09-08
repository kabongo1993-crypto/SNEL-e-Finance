using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class GroupeRubriqueBudgetaireRepository : IGroupeRubriqueBudgetaireRepository
{
    private readonly BudgetDbContext _context;

    public GroupeRubriqueBudgetaireRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GroupeRubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _context.RubriquesBudgetaires
            .AsNoTracking()
            .Where(r => r.FK_GroupeRubriqueBudgetaire != null)
            .GroupBy(r => r.FK_GroupeRubriqueBudgetaire!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byId = counts.ToDictionary(x => x.Id, x => x.Count);

        var groupes = await _context.GroupesRubriquesBudgetaires
            .AsNoTracking()
            .OrderBy(g => g.OrdreAffichage)
            .ThenBy(g => g.CodeGroupe)
            .ThenBy(g => g.Libelle)
            .ToListAsync(cancellationToken);

        return groupes
            .Select(g => new GroupeRubriqueBudgetaireDto(
                g.IdGroupeRB,
                g.CodeGroupe,
                g.Libelle,
                g.OrdreAffichage,
                g.Actif,
                g.DateCreation,
                byId.GetValueOrDefault(g.IdGroupeRB)))
            .ToList();
    }
}
