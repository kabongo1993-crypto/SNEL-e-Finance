using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class RubriqueBudgetaireRepository : IRubriqueBudgetaireRepository
{
    private readonly BudgetDbContext _context;

    public RubriqueBudgetaireRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _context.RubriquesBudgetaires.AsNoTracking().ToListAsync(cancellationToken);
        var groupes = await ChargerGroupesAsync(cancellationToken);
        var previsions = await CompterPrevisionsAsync(cancellationToken);
        return MapperListe(items, previsions, groupes);
    }

    public async Task<RubriqueBudgetaireDto?> GetByIdAsync(long idRB, CancellationToken cancellationToken = default)
    {
        var entity = await _context.RubriquesBudgetaires
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdRB == idRB, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return await MapperUneAsync(entity, cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string codeRB, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.RubriquesBudgetaires.AsNoTracking().Where(r => r.CodeRB.ToUpper() == codeRB);
        if (excludeId is not null)
        {
            query = query.Where(r => r.IdRB != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ExistsByIdAsync(long idRB, CancellationToken cancellationToken = default)
        => _context.RubriquesBudgetaires.AsNoTracking().AnyAsync(r => r.IdRB == idRB, cancellationToken);

    public async Task<bool> WouldCreateCycleAsync(long idRB, long parentId, CancellationToken cancellationToken = default)
    {
        if (idRB == parentId)
        {
            return true;
        }

        var currentId = (long?)parentId;
        var guard = 0;
        while (currentId is long id && guard++ < 50)
        {
            if (id == idRB)
            {
                return true;
            }

            currentId = await _context.RubriquesBudgetaires
                .AsNoTracking()
                .Where(r => r.IdRB == id)
                .Select(r => r.FK_RubriqueBudgetaireParent)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    public Task<int> CountEnfantsAsync(long idRB, CancellationToken cancellationToken = default)
        => _context.RubriquesBudgetaires
            .AsNoTracking()
            .CountAsync(r => r.FK_RubriqueBudgetaireParent == idRB, cancellationToken);

    public Task<int> CountPrevisionsAsync(long idRB, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires
            .AsNoTracking()
            .CountAsync(p => p.FK_RubriqueBudgetaire == idRB, cancellationToken);

    public async Task<RubriqueBudgetaireDto> CreateAsync(
        string codeRB,
        string libelle,
        long? parentId,
        int niveau,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = new RubriqueBudgetaire
        {
            CodeRB = codeRB,
            Libelle = libelle,
            FK_RubriqueBudgetaireParent = parentId,
            Niveau = niveau,
            Actif = actif,
            DateCreation = DateTime.Now,
        };

        _context.RubriquesBudgetaires.Add(entity);
        await SauvegarderAsync(codeRB, cancellationToken);
        return await MapperUneAsync(entity, cancellationToken);
    }

    public async Task<RubriqueBudgetaireDto?> UpdateAsync(
        long idRB,
        string codeRB,
        string libelle,
        long? parentId,
        int niveau,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.RubriquesBudgetaires
            .FirstOrDefaultAsync(r => r.IdRB == idRB, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.CodeRB = codeRB;
        entity.Libelle = libelle;
        entity.FK_RubriqueBudgetaireParent = parentId;
        entity.Actif = actif;
        await AppliquerNiveauEtDescendantsAsync(entity, niveau, cancellationToken);
        await SauvegarderAsync(codeRB, cancellationToken);
        return await MapperUneAsync(entity, cancellationToken);
    }

    public async Task<RubriqueBudgetaireDto?> SetActifAsync(
        long idRB,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.RubriquesBudgetaires
            .FirstOrDefaultAsync(r => r.IdRB == idRB, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Actif = actif;
        await _context.SaveChangesAsync(cancellationToken);
        return await MapperUneAsync(entity, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idRB, CancellationToken cancellationToken = default)
    {
        var entity = await _context.RubriquesBudgetaires
            .FirstOrDefaultAsync(r => r.IdRB == idRB, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.RubriquesBudgetaires.Remove(entity);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationFk(ex))
        {
            throw new InvalidOperationException(MessageSuppressionImpossible(ex));
        }

        return true;
    }

    private async Task AppliquerNiveauEtDescendantsAsync(
        RubriqueBudgetaire entity,
        int niveau,
        CancellationToken cancellationToken)
    {
        entity.Niveau = niveau;
        var enfants = await _context.RubriquesBudgetaires
            .Where(r => r.FK_RubriqueBudgetaireParent == entity.IdRB)
            .ToListAsync(cancellationToken);

        foreach (var enfant in enfants)
        {
            await AppliquerNiveauEtDescendantsAsync(enfant, niveau + 1, cancellationToken);
        }
    }

    private async Task SauvegarderAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationUnique(ex))
        {
            throw new InvalidOperationException($"Une rubrique budgétaire avec le code {code} existe déjà.");
        }
        catch (DbUpdateException ex) when (EstViolationCheck(ex))
        {
            throw new InvalidOperationException("Le niveau de la rubrique doit être supérieur ou égal à 0.");
        }
        catch (DbUpdateException ex) when (EstViolationFk(ex))
        {
            throw new InvalidOperationException("La rubrique parente indiquée n'existe pas.");
        }
    }

    private async Task<RubriqueBudgetaireDto> MapperUneAsync(RubriqueBudgetaire entity, CancellationToken cancellationToken)
    {
        var items = new List<RubriqueBudgetaire> { entity };
        if (entity.FK_RubriqueBudgetaireParent is long parentId)
        {
            var parent = await _context.RubriquesBudgetaires
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdRB == parentId, cancellationToken);
            if (parent is not null)
            {
                items.Add(parent);
            }
        }

        var previsions = new Dictionary<long, int>
        {
            [entity.IdRB] = await CountPrevisionsAsync(entity.IdRB, cancellationToken)
        };
        var enfants = new Dictionary<long, int>
        {
            [entity.IdRB] = await CountEnfantsAsync(entity.IdRB, cancellationToken)
        };
        var groupes = await ChargerGroupesAsync(cancellationToken);
        return MapperListe(items, previsions, groupes, enfants).First(r => r.IdRB == entity.IdRB);
    }

    private async Task<Dictionary<long, int>> CompterPrevisionsAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.PrevisionsBudgetaires
            .AsNoTracking()
            .Where(p => p.FK_RubriqueBudgetaire != null)
            .GroupBy(p => p.FK_RubriqueBudgetaire!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.Id, x => x.Count);
    }

    private async Task<IReadOnlyDictionary<long, GroupeRubriqueBudgetaire>> ChargerGroupesAsync(
        CancellationToken cancellationToken)
    {
        var list = await _context.GroupesRubriquesBudgetaires.AsNoTracking().ToListAsync(cancellationToken);
        return list.ToDictionary(g => g.IdGroupeRB);
    }

    private static IReadOnlyList<RubriqueBudgetaireDto> MapperListe(
        IReadOnlyList<RubriqueBudgetaire> items,
        IReadOnlyDictionary<long, int> previsions,
        IReadOnlyDictionary<long, GroupeRubriqueBudgetaire> groupes,
        IReadOnlyDictionary<long, int>? enfantsOverride = null)
    {
        var byId = items.ToDictionary(r => r.IdRB);
        var childCounts = enfantsOverride ?? items
            .Where(r => r.FK_RubriqueBudgetaireParent.HasValue)
            .GroupBy(r => r.FK_RubriqueBudgetaireParent!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return items
            .OrderBy(r => r.Niveau)
            .ThenBy(r => r.CodeRB, StringComparer.OrdinalIgnoreCase)
            .Select(r =>
            {
                RubriqueBudgetaire? parent = null;
                if (r.FK_RubriqueBudgetaireParent is long parentId)
                {
                    byId.TryGetValue(parentId, out parent);
                }

                GroupeRubriqueBudgetaire? groupe = null;
                if (r.FK_GroupeRubriqueBudgetaire is long idGroupe)
                {
                    groupes.TryGetValue(idGroupe, out groupe);
                }

                return new RubriqueBudgetaireDto(
                    r.IdRB,
                    r.CodeRB,
                    r.Libelle,
                    r.FK_RubriqueBudgetaireParent,
                    parent?.CodeRB,
                    parent?.Libelle,
                    r.Niveau,
                    r.Actif,
                    r.DateCreation,
                    childCounts.GetValueOrDefault(r.IdRB),
                    previsions.GetValueOrDefault(r.IdRB),
                    groupe?.IdGroupeRB,
                    groupe?.CodeGroupe,
                    groupe?.Libelle,
                    groupe?.OrdreAffichage);
            })
            .ToList();
    }

    private static bool EstViolationUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UQ_RB_Code", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationCheck(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("CK_RB_Niveau", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationFk(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("FK_RB_PARENT", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FK_PREVISION_RB", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static string MessageSuppressionImpossible(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("FK_RB_PARENT", StringComparison.OrdinalIgnoreCase))
        {
            return "Cette rubrique ne peut pas être supprimée car elle possède une ou plusieurs rubriques enfants. Désactivez-la plutôt.";
        }

        if (message.Contains("FK_PREVISION_RB", StringComparison.OrdinalIgnoreCase))
        {
            return "Cette rubrique ne peut pas être supprimée car elle est utilisée par une ou plusieurs prévisions budgétaires. Désactivez-la plutôt.";
        }

        return "Cette rubrique ne peut pas être supprimée car elle est utilisée par d'autres données. Désactivez-la plutôt.";
    }
}
