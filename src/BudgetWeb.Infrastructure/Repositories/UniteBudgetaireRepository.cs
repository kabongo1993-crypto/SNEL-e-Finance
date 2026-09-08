using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class UniteBudgetaireRepository : IUniteBudgetaireRepository
{
    private readonly BudgetDbContext _context;

    public UniteBudgetaireRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UniteBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Projection : pas d'Include Structure (inutile pour combo prévisions) + compte prévisions groupé.
        var unites = await _context.UnitesBudgetaires.AsNoTracking()
            .OrderBy(u => u.CodeUB)
            .Select(u => new
            {
                u.IdUB,
                u.CodeUB,
                u.Libelle,
                u.FK_Departement,
                CodeDepartement = u.Departement.Code,
                LibelleDepartement = u.Departement.Libelle,
                u.FK_StructureOrganisationnelle,
                CodeStructure = u.StructureOrganisationnelle.Code,
                LibelleStructure = u.StructureOrganisationnelle.Libelle,
                TypeStructure = u.StructureOrganisationnelle.TypeStructure,
                u.Actif,
                u.DateCreation,
            })
            .ToListAsync(cancellationToken);

        var previsions = await _context.PrevisionsBudgetaires
            .AsNoTracking()
            .GroupBy(p => p.FK_UniteBudgetaire)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var previsionsParUb = previsions.ToDictionary(x => x.Id, x => x.Count);

        return unites
            .Select(u => new UniteBudgetaireDto(
                u.IdUB,
                u.CodeUB,
                u.Libelle,
                u.FK_Departement,
                u.CodeDepartement,
                u.LibelleDepartement,
                u.FK_StructureOrganisationnelle,
                u.CodeStructure,
                u.LibelleStructure,
                u.TypeStructure,
                u.Actif,
                u.DateCreation,
                previsionsParUb.GetValueOrDefault(u.IdUB)))
            .ToList();
    }

    public async Task<UniteBudgetaireDto?> GetByIdAsync(long idUb, CancellationToken cancellationToken = default)
    {
        var entity = await _context.UnitesBudgetaires
            .AsNoTracking()
            .Include(u => u.Departement)
            .Include(u => u.StructureOrganisationnelle)
            .FirstOrDefaultAsync(u => u.IdUB == idUb, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var previsions = await CountPrevisionsAsync(idUb, cancellationToken);
        return Map(entity, previsions);
    }

    public Task<bool> ExistsByCodeAsync(string codeUb, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.UnitesBudgetaires.AsNoTracking().Where(u => u.CodeUB.ToUpper() == codeUb);
        if (excludeId is not null)
        {
            query = query.Where(u => u.IdUB != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ExistsDepartementAsync(long idDepartement, CancellationToken cancellationToken = default)
        => _context.Departements
            .AsNoTracking()
            .AnyAsync(d => d.IdDepartement == idDepartement, cancellationToken);

    public Task<bool> ExistsStructureAsync(long idStructure, CancellationToken cancellationToken = default)
        => _context.StructuresOrganisationnelles
            .AsNoTracking()
            .AnyAsync(s => s.IdStructure == idStructure, cancellationToken);

    public async Task<string?> GetCodeDepartementAsync(long idDepartement, CancellationToken cancellationToken = default)
    {
        var departement = await _context.Departements
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdDepartement == idDepartement, cancellationToken);
        return departement?.Code;
    }

    public async Task<string?> GetCodeDepartementOrganisationnelAsync(
        long idStructure,
        CancellationToken cancellationToken = default)
    {
        var current = await _context.StructuresOrganisationnelles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdStructure == idStructure, cancellationToken);

        var guard = 0;
        while (current is not null && guard++ < 20)
        {
            if (current.TypeStructure.Equals(TypeStructureOrganisationnelle.Departement, StringComparison.OrdinalIgnoreCase))
            {
                return ExtraireCodeAffichage(current.Code);
            }

            if (current.FK_StructureOrganisationnelleParent is not long parentId)
            {
                break;
            }

            current = await _context.StructuresOrganisationnelles
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IdStructure == parentId, cancellationToken);
        }

        return null;
    }

    public Task<int> CountPrevisionsAsync(long idUb, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires
            .AsNoTracking()
            .CountAsync(p => p.FK_UniteBudgetaire == idUb, cancellationToken);

    public async Task<UniteBudgetaireDto> CreateAsync(
        string codeUb,
        string libelle,
        long idDepartement,
        long idStructure,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = new UniteBudgetaire
        {
            CodeUB = codeUb,
            Libelle = libelle,
            FK_Departement = idDepartement,
            FK_StructureOrganisationnelle = idStructure,
            Actif = actif,
            DateCreation = DateTime.UtcNow,
        };

        _context.UnitesBudgetaires.Add(entity);
        await SauvegarderAsync(codeUb, cancellationToken);
        await ChargerReferencesAsync(entity, cancellationToken);
        return Map(entity, 0);
    }

    public async Task<UniteBudgetaireDto?> UpdateAsync(
        long idUb,
        string codeUb,
        string libelle,
        long idDepartement,
        long idStructure,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.UnitesBudgetaires
            .FirstOrDefaultAsync(u => u.IdUB == idUb, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.CodeUB = codeUb;
        entity.Libelle = libelle;
        entity.FK_Departement = idDepartement;
        entity.FK_StructureOrganisationnelle = idStructure;
        entity.Actif = actif;
        await SauvegarderAsync(codeUb, cancellationToken);
        await ChargerReferencesAsync(entity, cancellationToken);
        var previsions = await CountPrevisionsAsync(idUb, cancellationToken);
        return Map(entity, previsions);
    }

    public async Task<bool> DeleteAsync(long idUb, CancellationToken cancellationToken = default)
    {
        var entity = await _context.UnitesBudgetaires
            .FirstOrDefaultAsync(u => u.IdUB == idUb, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.UnitesBudgetaires.Remove(entity);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationCleEtrangere(ex))
        {
            throw new InvalidOperationException(
                "Cette unité budgétaire ne peut pas être supprimée car elle est utilisée par une ou plusieurs prévisions budgétaires.");
        }

        return true;
    }

    private async Task SauvegarderAsync(string codeUb, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationUnique(ex))
        {
            throw new InvalidOperationException($"Une unité budgétaire avec le code {codeUb} existe déjà.");
        }
        catch (DbUpdateException ex) when (EstViolationCleEtrangere(ex))
        {
            throw new InvalidOperationException("Le département ou la structure indiquée n'existe pas.");
        }
    }

    private async Task ChargerReferencesAsync(UniteBudgetaire entity, CancellationToken cancellationToken)
    {
        await _context.Entry(entity).Reference(e => e.Departement).LoadAsync(cancellationToken);
        await _context.Entry(entity).Reference(e => e.StructureOrganisationnelle).LoadAsync(cancellationToken);
    }

    private static UniteBudgetaireDto Map(UniteBudgetaire entity, int nombrePrevisions)
        => new(
            entity.IdUB,
            entity.CodeUB,
            entity.Libelle,
            entity.FK_Departement,
            entity.Departement.Code,
            entity.Departement.Libelle,
            entity.FK_StructureOrganisationnelle,
            entity.StructureOrganisationnelle.Code,
            entity.StructureOrganisationnelle.Libelle,
            entity.StructureOrganisationnelle.TypeStructure,
            entity.Actif,
            entity.DateCreation,
            nombrePrevisions);

    private static string ExtraireCodeAffichage(string codeTechnique)
    {
        if (string.IsNullOrWhiteSpace(codeTechnique))
        {
            return codeTechnique;
        }

        var parts = codeTechnique.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? codeTechnique : parts[^1];
    }

    private static bool EstViolationUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UQ_UNITE_BUDGETAIRE_Code", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationCleEtrangere(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("FK_UB_DEPARTEMENT", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FK_UB_STRUCTURE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FK_PREVISION_UB", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }
}
