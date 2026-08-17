using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class ReferentielRepository : IReferentielRepository
{
    private readonly BudgetDbContext _context;

    public ReferentielRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TypeBudgetDto>> GetTypesBudgetAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TypesBudget
            .AsNoTracking()
            .OrderBy(t => t.OrdreAffichage)
            .Select(t => new TypeBudgetDto(t.IdTypeBudget, t.CodeType, t.Libelle, t.OrdreAffichage, t.Actif))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModePrevisionDto>> GetModesPrevisionAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ModesPrevision
            .AsNoTracking()
            .OrderBy(m => m.CodeMode)
            .Select(m => new ModePrevisionDto(m.IdModePrevision, m.CodeMode, m.Libelle, m.Actif))
            .ToListAsync(cancellationToken);
    }
}
