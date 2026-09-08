using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class TypeBudgetRepository : ITypeBudgetRepository
{
    private readonly BudgetDbContext _context;

    public TypeBudgetRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TypeBudgetDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TypesBudget
            .AsNoTracking()
            .OrderBy(t => t.OrdreAffichage)
            .ThenBy(t => t.CodeType)
            .Select(t => new TypeBudgetDto(
                t.IdTypeBudget,
                t.CodeType,
                t.Libelle,
                t.OrdreAffichage,
                t.Actif,
                t.PrevisionsBudgetaires.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<TypeBudgetDto?> GetByIdAsync(long idTypeBudget, CancellationToken cancellationToken = default)
    {
        return await _context.TypesBudget
            .AsNoTracking()
            .Where(t => t.IdTypeBudget == idTypeBudget)
            .Select(t => new TypeBudgetDto(
                t.IdTypeBudget,
                t.CodeType,
                t.Libelle,
                t.OrdreAffichage,
                t.Actif,
                t.PrevisionsBudgetaires.Count))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string codeType, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.TypesBudget.AsNoTracking().Where(t => t.CodeType.ToUpper() == codeType);
        if (excludeId is not null)
        {
            query = query.Where(t => t.IdTypeBudget != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ExistsByOrdreAsync(int ordreAffichage, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.TypesBudget.AsNoTracking().Where(t => t.OrdreAffichage == ordreAffichage);
        if (excludeId is not null)
        {
            query = query.Where(t => t.IdTypeBudget != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<int> CountPrevisionsAsync(long idTypeBudget, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires
            .AsNoTracking()
            .CountAsync(p => p.FK_TypeBudget == idTypeBudget, cancellationToken);

    public async Task<TypeBudgetDto> CreateAsync(
        string codeType,
        string libelle,
        int ordreAffichage,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = new TypeBudget
        {
            CodeType = codeType,
            Libelle = libelle,
            OrdreAffichage = ordreAffichage,
            Actif = actif,
        };

        _context.TypesBudget.Add(entity);
        await SauvegarderAsync(codeType, ordreAffichage, cancellationToken);
        return Map(entity, 0);
    }

    public async Task<TypeBudgetDto?> UpdateAsync(
        long idTypeBudget,
        string codeType,
        string libelle,
        int ordreAffichage,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.TypesBudget
            .FirstOrDefaultAsync(t => t.IdTypeBudget == idTypeBudget, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.CodeType = codeType;
        entity.Libelle = libelle;
        entity.OrdreAffichage = ordreAffichage;
        entity.Actif = actif;
        await SauvegarderAsync(codeType, ordreAffichage, cancellationToken);
        var previsions = await CountPrevisionsAsync(idTypeBudget, cancellationToken);
        return Map(entity, previsions);
    }

    public async Task<bool> DeleteAsync(long idTypeBudget, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TypesBudget
            .FirstOrDefaultAsync(t => t.IdTypeBudget == idTypeBudget, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.TypesBudget.Remove(entity);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationFk(ex))
        {
            throw new InvalidOperationException(
                "Ce type de budget ne peut pas être supprimé car il est utilisé par une ou plusieurs prévisions budgétaires.");
        }

        return true;
    }

    private async Task SauvegarderAsync(string codeType, int ordreAffichage, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationUnique(ex))
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            if (message.Contains("UQ_TYPE_BUDGET_Ordre", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Un type de budget avec l'ordre d'affichage {ordreAffichage} existe déjà.");
            }

            throw new InvalidOperationException($"Un type de budget avec le code {codeType} existe déjà.");
        }
    }

    private static TypeBudgetDto Map(TypeBudget entity, int nombrePrevisions)
        => new(entity.IdTypeBudget, entity.CodeType, entity.Libelle, entity.OrdreAffichage, entity.Actif, nombrePrevisions);

    private static bool EstViolationUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UQ_TYPE_BUDGET_Code", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UQ_TYPE_BUDGET_Ordre", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationFk(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("FK_PREVISION_TYPE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }
}
