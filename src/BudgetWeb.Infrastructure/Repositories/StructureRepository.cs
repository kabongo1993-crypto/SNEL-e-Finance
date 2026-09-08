using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class StructureRepository : IStructureRepository
{
    private readonly BudgetDbContext _context;

    public StructureRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StructureDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var structures = await _context.StructuresOrganisationnelles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var ubCounts = await _context.UnitesBudgetaires
            .AsNoTracking()
            .GroupBy(u => u.FK_StructureOrganisationnelle)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var ubByStructure = ubCounts.ToDictionary(x => x.Id, x => x.Count);
        var byId = structures.ToDictionary(s => s.IdStructure);
        var childCounts = structures
            .Where(s => s.FK_StructureOrganisationnelleParent.HasValue)
            .GroupBy(s => s.FK_StructureOrganisationnelleParent!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return structures
            .OrderBy(s => OrdreType(s.TypeStructure))
            .ThenBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
            .Select(s => Map(s, byId, childCounts, ubByStructure))
            .ToList();
    }

    public async Task<StructureDto?> GetByIdAsync(long idStructure, CancellationToken cancellationToken = default)
    {
        var entity = await _context.StructuresOrganisationnelles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdStructure == idStructure, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return await MapperUneAsync(entity, cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string code, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.StructuresOrganisationnelles.AsNoTracking().Where(s => s.Code.ToUpper() == code);
        if (excludeId is not null)
        {
            query = query.Where(s => s.IdStructure != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ExistsByIdAsync(long idStructure, CancellationToken cancellationToken = default)
        => _context.StructuresOrganisationnelles
            .AsNoTracking()
            .AnyAsync(s => s.IdStructure == idStructure, cancellationToken);

    public async Task<bool> WouldCreateCycleAsync(long idStructure, long parentId, CancellationToken cancellationToken = default)
    {
        if (idStructure == parentId)
        {
            return true;
        }

        var currentId = (long?)parentId;
        var guard = 0;
        while (currentId is long id && guard++ < 50)
        {
            if (id == idStructure)
            {
                return true;
            }

            currentId = await _context.StructuresOrganisationnelles
                .AsNoTracking()
                .Where(s => s.IdStructure == id)
                .Select(s => s.FK_StructureOrganisationnelleParent)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    public Task<int> CountEnfantsAsync(long idStructure, CancellationToken cancellationToken = default)
        => _context.StructuresOrganisationnelles
            .AsNoTracking()
            .CountAsync(s => s.FK_StructureOrganisationnelleParent == idStructure, cancellationToken);

    public Task<int> CountUnitesBudgetairesAsync(long idStructure, CancellationToken cancellationToken = default)
        => _context.UnitesBudgetaires
            .AsNoTracking()
            .CountAsync(u => u.FK_StructureOrganisationnelle == idStructure, cancellationToken);

    public async Task<StructureDto> CreateAsync(
        string typeStructure,
        string code,
        string libelle,
        long? parentId,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = new StructureOrganisationnelle
        {
            TypeStructure = typeStructure,
            Code = code,
            Libelle = libelle,
            FK_StructureOrganisationnelleParent = parentId,
            Actif = actif,
            DateCreation = DateTime.UtcNow,
        };

        _context.StructuresOrganisationnelles.Add(entity);
        await SauvegarderAsync(code, cancellationToken);
        return await MapperUneAsync(entity, cancellationToken);
    }

    public async Task<StructureDto?> UpdateAsync(
        long idStructure,
        string typeStructure,
        string code,
        string libelle,
        long? parentId,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.StructuresOrganisationnelles
            .FirstOrDefaultAsync(s => s.IdStructure == idStructure, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.TypeStructure = typeStructure;
        entity.Code = code;
        entity.Libelle = libelle;
        entity.FK_StructureOrganisationnelleParent = parentId;
        entity.Actif = actif;
        await SauvegarderAsync(code, cancellationToken);
        return await MapperUneAsync(entity, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idStructure, CancellationToken cancellationToken = default)
    {
        var entity = await _context.StructuresOrganisationnelles
            .FirstOrDefaultAsync(s => s.IdStructure == idStructure, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.StructuresOrganisationnelles.Remove(entity);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationCleEtrangere(ex))
        {
            throw new InvalidOperationException(MessageSuppressionImpossible(ex));
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
            throw new InvalidOperationException($"Une structure avec le code {code} existe déjà.");
        }
        catch (DbUpdateException ex) when (EstViolationCleEtrangere(ex))
        {
            throw new InvalidOperationException("La structure parente indiquée n'existe pas.");
        }
    }

    private async Task<StructureDto> MapperUneAsync(
        StructureOrganisationnelle entity,
        CancellationToken cancellationToken)
    {
        var byId = new Dictionary<long, StructureOrganisationnelle>();
        if (entity.FK_StructureOrganisationnelleParent is long parentId)
        {
            var parent = await _context.StructuresOrganisationnelles
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IdStructure == parentId, cancellationToken);
            if (parent is not null)
            {
                byId[parent.IdStructure] = parent;
            }
        }

        var enfants = await CountEnfantsAsync(entity.IdStructure, cancellationToken);
        var unites = await CountUnitesBudgetairesAsync(entity.IdStructure, cancellationToken);

        return Map(
            entity,
            byId,
            new Dictionary<long, int> { [entity.IdStructure] = enfants },
            new Dictionary<long, int> { [entity.IdStructure] = unites });
    }

    private static StructureDto Map(
        StructureOrganisationnelle entity,
        IReadOnlyDictionary<long, StructureOrganisationnelle> byId,
        IReadOnlyDictionary<long, int> childCounts,
        IReadOnlyDictionary<long, int> ubCounts)
    {
        StructureOrganisationnelle? parent = null;
        if (entity.FK_StructureOrganisationnelleParent is { } parentId)
        {
            byId.TryGetValue(parentId, out parent);
        }

        return new StructureDto(
            entity.IdStructure,
            entity.FK_StructureOrganisationnelleParent,
            entity.TypeStructure,
            entity.Code,
            entity.Libelle,
            entity.Actif,
            entity.DateCreation,
            parent?.Code,
            parent?.Libelle,
            childCounts.GetValueOrDefault(entity.IdStructure),
            ubCounts.GetValueOrDefault(entity.IdStructure));
    }

    private static int OrdreType(string type)
        => type.ToUpperInvariant() switch
        {
            TypeStructureOrganisationnelle.Entite => 0,
            TypeStructureOrganisationnelle.Departement => 1,
            TypeStructureOrganisationnelle.Direction => 2,
            TypeStructureOrganisationnelle.Division => 3,
            TypeStructureOrganisationnelle.Service => 4,
            TypeStructureOrganisationnelle.Section => 5,
            _ => 6
        };

    private static bool EstViolationUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UQ_STRUCTURE_Code", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationCleEtrangere(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("FK_STRUCTURE_PARENT", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FK_UB_STRUCTURE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static string MessageSuppressionImpossible(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("FK_STRUCTURE_PARENT", StringComparison.OrdinalIgnoreCase))
        {
            return "Cette structure ne peut pas être supprimée car elle possède une ou plusieurs structures enfants.";
        }

        if (message.Contains("FK_UB_STRUCTURE", StringComparison.OrdinalIgnoreCase))
        {
            return "Cette structure ne peut pas être supprimée car elle est utilisée par une ou plusieurs unités budgétaires.";
        }

        return "Cette structure ne peut pas être supprimée car elle est utilisée par d'autres données.";
    }
}
