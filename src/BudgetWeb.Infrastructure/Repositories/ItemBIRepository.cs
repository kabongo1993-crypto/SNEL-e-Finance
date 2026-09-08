using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class ItemBIRepository : IItemBIRepository
{
    private readonly BudgetDbContext _context;

    public ItemBIRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ItemBIDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _context.ItemsBI.AsNoTracking().ToListAsync(cancellationToken);
        var previsions = await CompterPrevisionsAsync(cancellationToken);
        return MapperListe(items, previsions);
    }

    public async Task<ItemBIDto?> GetByIdAsync(long idItemBI, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ItemsBI
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.IdItemBI == idItemBI, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return await MapperUneAsync(entity, cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string codeItem, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ItemsBI.AsNoTracking().Where(i => i.CodeItem.ToUpper() == codeItem);
        if (excludeId is not null)
        {
            query = query.Where(i => i.IdItemBI != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ExistsByIdAsync(long idItemBI, CancellationToken cancellationToken = default)
        => _context.ItemsBI.AsNoTracking().AnyAsync(i => i.IdItemBI == idItemBI, cancellationToken);

    public async Task<bool> WouldCreateCycleAsync(long idItemBI, long parentId, CancellationToken cancellationToken = default)
    {
        if (idItemBI == parentId)
        {
            return true;
        }

        var currentId = (long?)parentId;
        var guard = 0;
        while (currentId is long id && guard++ < 50)
        {
            if (id == idItemBI)
            {
                return true;
            }

            currentId = await _context.ItemsBI
                .AsNoTracking()
                .Where(i => i.IdItemBI == id)
                .Select(i => i.FK_ItemBIParent)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    public Task<int> CountEnfantsAsync(long idItemBI, CancellationToken cancellationToken = default)
        => _context.ItemsBI
            .AsNoTracking()
            .CountAsync(i => i.FK_ItemBIParent == idItemBI, cancellationToken);

    public Task<int> CountPrevisionsAsync(long idItemBI, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires
            .AsNoTracking()
            .CountAsync(p => p.FK_ItemBI == idItemBI, cancellationToken);

    public async Task<ItemBIDto> CreateAsync(
        string codeItem,
        string libelle,
        long? parentId,
        int niveau,
        string? categorie,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = new ItemBI
        {
            CodeItem = codeItem,
            Libelle = libelle,
            FK_ItemBIParent = parentId,
            Niveau = niveau,
            Categorie = categorie,
            Actif = actif,
            DateCreation = DateTime.Now,
        };

        _context.ItemsBI.Add(entity);
        await SauvegarderAsync(codeItem, cancellationToken);
        return await MapperUneAsync(entity, cancellationToken);
    }

    public async Task<ItemBIDto?> UpdateAsync(
        long idItemBI,
        string codeItem,
        string libelle,
        long? parentId,
        int niveau,
        string? categorie,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ItemsBI
            .FirstOrDefaultAsync(i => i.IdItemBI == idItemBI, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.CodeItem = codeItem;
        entity.Libelle = libelle;
        entity.FK_ItemBIParent = parentId;
        entity.Categorie = categorie;
        entity.Actif = actif;
        await AppliquerNiveauEtDescendantsAsync(entity, niveau, cancellationToken);
        await SauvegarderAsync(codeItem, cancellationToken);
        return await MapperUneAsync(entity, cancellationToken);
    }

    public async Task<ItemBIDto?> SetActifAsync(
        long idItemBI,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ItemsBI
            .FirstOrDefaultAsync(i => i.IdItemBI == idItemBI, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Actif = actif;
        await _context.SaveChangesAsync(cancellationToken);
        return await MapperUneAsync(entity, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idItemBI, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ItemsBI
            .FirstOrDefaultAsync(i => i.IdItemBI == idItemBI, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.ItemsBI.Remove(entity);
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
        ItemBI entity,
        int niveau,
        CancellationToken cancellationToken)
    {
        entity.Niveau = niveau;
        var enfants = await _context.ItemsBI
            .Where(i => i.FK_ItemBIParent == entity.IdItemBI)
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
            throw new InvalidOperationException($"Un item BI avec le code {code} existe déjà.");
        }
        catch (DbUpdateException ex) when (EstViolationCheck(ex))
        {
            throw new InvalidOperationException("Le niveau de l'item BI doit être supérieur ou égal à 0.");
        }
        catch (DbUpdateException ex) when (EstViolationFk(ex))
        {
            throw new InvalidOperationException("L'item BI parent indiqué n'existe pas.");
        }
    }

    private async Task<ItemBIDto> MapperUneAsync(ItemBI entity, CancellationToken cancellationToken)
    {
        var items = new List<ItemBI> { entity };
        if (entity.FK_ItemBIParent is long parentId)
        {
            var parent = await _context.ItemsBI
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IdItemBI == parentId, cancellationToken);
            if (parent is not null)
            {
                items.Add(parent);
            }
        }

        var previsions = new Dictionary<long, int>
        {
            [entity.IdItemBI] = await CountPrevisionsAsync(entity.IdItemBI, cancellationToken)
        };
        var enfants = new Dictionary<long, int>
        {
            [entity.IdItemBI] = await CountEnfantsAsync(entity.IdItemBI, cancellationToken)
        };
        return MapperListe(items, previsions, enfants).First(i => i.IdItemBI == entity.IdItemBI);
    }

    private async Task<Dictionary<long, int>> CompterPrevisionsAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.PrevisionsBudgetaires
            .AsNoTracking()
            .Where(p => p.FK_ItemBI != null)
            .GroupBy(p => p.FK_ItemBI!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.Id, x => x.Count);
    }

    private static IReadOnlyList<ItemBIDto> MapperListe(
        IReadOnlyList<ItemBI> items,
        IReadOnlyDictionary<long, int> previsions,
        IReadOnlyDictionary<long, int>? enfantsOverride = null)
    {
        var byId = items.ToDictionary(i => i.IdItemBI);
        var childCounts = enfantsOverride ?? items
            .Where(i => i.FK_ItemBIParent.HasValue)
            .GroupBy(i => i.FK_ItemBIParent!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return items
            .OrderBy(i => i.Niveau)
            .ThenBy(i => i.CodeItem, StringComparer.OrdinalIgnoreCase)
            .Select(i =>
            {
                ItemBI? parent = null;
                if (i.FK_ItemBIParent is long parentId)
                {
                    byId.TryGetValue(parentId, out parent);
                }

                return new ItemBIDto(
                    i.IdItemBI,
                    i.CodeItem,
                    i.Libelle,
                    i.FK_ItemBIParent,
                    parent?.CodeItem,
                    parent?.Libelle,
                    i.Niveau,
                    i.Categorie,
                    i.Actif,
                    i.DateCreation,
                    childCounts.GetValueOrDefault(i.IdItemBI),
                    previsions.GetValueOrDefault(i.IdItemBI));
            })
            .ToList();
    }

    private static bool EstViolationUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UQ_ITEM_BI_Code", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationCheck(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("CK_ITEM_BI_Niveau", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationFk(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("FK_ITEM_BI_PARENT", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FK_PREVISION_ITEM_BI", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static string MessageSuppressionImpossible(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("FK_ITEM_BI_PARENT", StringComparison.OrdinalIgnoreCase))
        {
            return "Cet item BI ne peut pas être supprimé car il possède un ou plusieurs items enfants. Désactivez-le plutôt.";
        }

        if (message.Contains("FK_PREVISION_ITEM_BI", StringComparison.OrdinalIgnoreCase))
        {
            return "Cet item BI ne peut pas être supprimé car il est utilisé par une ou plusieurs prévisions budgétaires. Désactivez-le plutôt.";
        }

        return "Cet item BI ne peut pas être supprimé car il est utilisé par d'autres données. Désactivez-le plutôt.";
    }
}
