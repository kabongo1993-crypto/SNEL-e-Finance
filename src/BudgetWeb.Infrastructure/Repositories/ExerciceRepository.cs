using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class ExerciceRepository : IExerciceRepository
{
    private readonly BudgetDbContext _context;

    public ExerciceRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ExerciceDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExercicesBudgetaires
            .AsNoTracking()
            .OrderByDescending(e => e.Annee)
            .Select(e => new ExerciceDto(
                e.IdExercice,
                e.Annee,
                e.Statut,
                e.DateOuverture,
                e.DateCloture,
                e.VersionsBudgetaires.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<ExerciceDto?> GetByIdAsync(long idExercice, CancellationToken cancellationToken = default)
    {
        return await _context.ExercicesBudgetaires
            .AsNoTracking()
            .Where(e => e.IdExercice == idExercice)
            .Select(e => new ExerciceDto(
                e.IdExercice,
                e.Annee,
                e.Statut,
                e.DateOuverture,
                e.DateCloture,
                e.VersionsBudgetaires.Count))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsByAnneeAsync(short annee, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ExercicesBudgetaires.AsNoTracking().Where(e => e.Annee == annee);
        if (excludeId is not null)
        {
            query = query.Where(e => e.IdExercice != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<int> CountVersionsAsync(long idExercice, CancellationToken cancellationToken = default)
        => _context.VersionsBudgetaires
            .AsNoTracking()
            .CountAsync(v => v.FK_ExerciceBudgetaire == idExercice, cancellationToken);

    public async Task<ExerciceDto> CreateAsync(
        short annee,
        string statut,
        DateOnly? dateOuverture,
        DateOnly? dateCloture,
        CancellationToken cancellationToken = default)
    {
        var entity = new ExerciceBudgetaire
        {
            Annee = annee,
            Statut = statut,
            DateOuverture = dateOuverture,
            DateCloture = dateCloture,
        };

        _context.ExercicesBudgetaires.Add(entity);
        await SauvegarderAsync(annee, cancellationToken);
        return Map(entity, 0);
    }

    public async Task<ExerciceDto?> UpdateAsync(
        long idExercice,
        short annee,
        string statut,
        DateOnly? dateOuverture,
        DateOnly? dateCloture,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExercicesBudgetaires
            .FirstOrDefaultAsync(e => e.IdExercice == idExercice, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Annee = annee;
        entity.Statut = statut;
        entity.DateOuverture = dateOuverture;
        entity.DateCloture = dateCloture;

        await SauvegarderAsync(annee, cancellationToken);
        var nombreVersions = await CountVersionsAsync(idExercice, cancellationToken);
        return Map(entity, nombreVersions);
    }

    public async Task<bool> DeleteAsync(long idExercice, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExercicesBudgetaires
            .FirstOrDefaultAsync(e => e.IdExercice == idExercice, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.ExercicesBudgetaires.Remove(entity);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationCleEtrangere(ex))
        {
            throw new InvalidOperationException(
                "Cet exercice ne peut pas être supprimé car il est utilisé par une ou plusieurs versions budgétaires.");
        }

        return true;
    }

    private async Task SauvegarderAsync(short annee, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationUnique(ex))
        {
            throw new InvalidOperationException($"L'exercice {annee} existe déjà.");
        }
        catch (DbUpdateException ex) when (EstViolationCheckAnnee(ex))
        {
            throw new InvalidOperationException("L'année de l'exercice doit être comprise entre 2000 et 2100.");
        }
    }

    private static ExerciceDto Map(ExerciceBudgetaire entity, int nombreVersions)
        => new(
            entity.IdExercice,
            entity.Annee,
            entity.Statut,
            entity.DateOuverture,
            entity.DateCloture,
            nombreVersions);

    private static bool EstViolationUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UQ_EXERCICE_Annee", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationCheckAnnee(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("CK_EXERCICE_Annee", StringComparison.OrdinalIgnoreCase)
            || message.Contains("CHECK constraint", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationCleEtrangere(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("FK_VERSION_EXERCICE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }
}
