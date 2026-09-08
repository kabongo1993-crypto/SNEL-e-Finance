using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class DepartementRepository : IDepartementRepository
{
    private readonly BudgetDbContext _context;

    public DepartementRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DepartementDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Departements
            .AsNoTracking()
            .OrderBy(d => d.Code)
            .Select(d => new DepartementDto(
                d.IdDepartement,
                d.Code,
                d.Libelle,
                d.Actif,
                d.DateCreation,
                d.UnitesBudgetaires.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<DepartementDto?> GetByIdAsync(long idDepartement, CancellationToken cancellationToken = default)
    {
        return await _context.Departements
            .AsNoTracking()
            .Where(d => d.IdDepartement == idDepartement)
            .Select(d => new DepartementDto(
                d.IdDepartement,
                d.Code,
                d.Libelle,
                d.Actif,
                d.DateCreation,
                d.UnitesBudgetaires.Count))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string code, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Departements.AsNoTracking().Where(d => d.Code.ToUpper() == code);
        if (excludeId is not null)
        {
            query = query.Where(d => d.IdDepartement != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<int> CountUnitesBudgetairesAsync(long idDepartement, CancellationToken cancellationToken = default)
        => _context.UnitesBudgetaires
            .AsNoTracking()
            .CountAsync(u => u.FK_Departement == idDepartement, cancellationToken);

    public async Task<DepartementDto> CreateAsync(
        string code,
        string libelle,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = new Departement
        {
            Code = code,
            Libelle = libelle,
            Actif = actif,
            DateCreation = DateTime.UtcNow,
        };

        _context.Departements.Add(entity);
        await SauvegarderAsync(code, cancellationToken);
        return Map(entity, 0);
    }

    public async Task<DepartementDto?> UpdateAsync(
        long idDepartement,
        string code,
        string libelle,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Departements
            .FirstOrDefaultAsync(d => d.IdDepartement == idDepartement, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Code = code;
        entity.Libelle = libelle;
        entity.Actif = actif;
        await SauvegarderAsync(code, cancellationToken);
        var ub = await CountUnitesBudgetairesAsync(idDepartement, cancellationToken);
        return Map(entity, ub);
    }

    public async Task<bool> DeleteAsync(long idDepartement, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Departements
            .FirstOrDefaultAsync(d => d.IdDepartement == idDepartement, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.Departements.Remove(entity);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationFk(ex))
        {
            throw new InvalidOperationException(
                "Ce département ne peut pas être supprimé car il est utilisé par une ou plusieurs unités budgétaires.");
        }

        return true;
    }

    private async Task SauvegarderAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationUnique(ex))
        {
            throw new InvalidOperationException($"Un département avec le code {code} existe déjà.");
        }
    }

    private static DepartementDto Map(Departement entity, int nombreUb)
        => new(entity.IdDepartement, entity.Code, entity.Libelle, entity.Actif, entity.DateCreation, nombreUb);

    private static bool EstViolationUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationFk(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("FK_UB_DEPARTEMENT", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }
}
