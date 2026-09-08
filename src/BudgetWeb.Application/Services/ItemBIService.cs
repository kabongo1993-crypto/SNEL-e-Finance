using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public class ItemBIService : IItemBIService
{
    private readonly IItemBIRepository _repository;

    public ItemBIService(IItemBIRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<ItemBIDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<ItemBINoeudDto>> GetArbreAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return ConstruireArbre(items);
    }

    public Task<ItemBIDto?> GetByIdAsync(long idItemBI, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idItemBI, cancellationToken);

    public async Task<ItemBIDto> CreateAsync(
        CreateItemBIRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, libelle, parentId, categorie, actif) = await ValiderAsync(
            request.CodeItem,
            request.Libelle,
            request.ParentId,
            request.Categorie,
            request.Actif,
            excludeId: null,
            cancellationToken);

        var niveau = await CalculerNiveauAsync(parentId, cancellationToken);
        return await _repository.CreateAsync(code, libelle, parentId, niveau, categorie, actif, cancellationToken);
    }

    public async Task<ItemBIDto?> UpdateAsync(
        long idItemBI,
        UpdateItemBIRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idItemBI, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var (code, libelle, parentId, categorie, actif) = await ValiderAsync(
            request.CodeItem,
            request.Libelle,
            request.ParentId,
            request.Categorie,
            request.Actif,
            excludeId: idItemBI,
            cancellationToken);

        await GarantirAbsenceCycleAsync(idItemBI, parentId, cancellationToken);

        var niveau = await CalculerNiveauAsync(parentId, cancellationToken);
        return await _repository.UpdateAsync(
            idItemBI,
            code,
            libelle,
            parentId,
            niveau,
            categorie,
            actif,
            cancellationToken);
    }

    public async Task<ItemBIDto?> SetActifAsync(
        long idItemBI,
        SetActifRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idItemBI, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        return await _repository.SetActifAsync(idItemBI, request.Actif, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idItemBI, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idItemBI, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var enfants = await _repository.CountEnfantsAsync(idItemBI, cancellationToken);
        var previsions = await _repository.CountPrevisionsAsync(idItemBI, cancellationToken);

        if (enfants > 0)
        {
            throw new InvalidOperationException(
                "Cet item BI ne peut pas être supprimé car il possède un ou plusieurs items enfants. Désactivez-le plutôt.");
        }

        if (previsions > 0)
        {
            throw new InvalidOperationException(
                "Cet item BI ne peut pas être supprimé car il est utilisé par une ou plusieurs prévisions budgétaires. Désactivez-le plutôt.");
        }

        return await _repository.DeleteAsync(idItemBI, cancellationToken);
    }

    private async Task<(string Code, string Libelle, long? ParentId, string? Categorie, bool Actif)> ValiderAsync(
        string? codeBrut,
        string? libelleBrut,
        long? parentIdBrut,
        string? categorieBrut,
        bool? actifBrut,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var code = (codeBrut ?? string.Empty).Trim().ToUpperInvariant();
        var libelle = (libelleBrut ?? string.Empty).Trim();
        var categorie = string.IsNullOrWhiteSpace(categorieBrut) ? null : categorieBrut.Trim();
        var parentId = parentIdBrut is > 0 ? parentIdBrut : null;

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Le code de l'item BI est obligatoire.");
        }

        if (code.Length > 30)
        {
            throw new InvalidOperationException("Le code de l'item BI ne peut pas dépasser 30 caractères.");
        }

        if (string.IsNullOrWhiteSpace(libelle))
        {
            throw new InvalidOperationException("Le libellé de l'item BI est obligatoire.");
        }

        if (libelle.Length > 300)
        {
            throw new InvalidOperationException("Le libellé de l'item BI ne peut pas dépasser 300 caractères.");
        }

        if (categorie is { Length: > 200 })
        {
            throw new InvalidOperationException("La catégorie ne peut pas dépasser 200 caractères.");
        }

        if (parentId is not null && !await _repository.ExistsByIdAsync(parentId.Value, cancellationToken))
        {
            throw new InvalidOperationException("L'item BI parent indiqué n'existe pas.");
        }

        if (await _repository.ExistsByCodeAsync(code, excludeId, cancellationToken))
        {
            throw new InvalidOperationException($"Un item BI avec le code {code} existe déjà.");
        }

        return (code, libelle, parentId, categorie, actifBrut ?? true);
    }

    private async Task GarantirAbsenceCycleAsync(long idItemBI, long? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return;
        }

        if (parentId == idItemBI)
        {
            throw new InvalidOperationException("Un item BI ne peut pas être son propre parent.");
        }

        if (await _repository.WouldCreateCycleAsync(idItemBI, parentId.Value, cancellationToken))
        {
            throw new InvalidOperationException(
                "L'item BI parent indiqué créerait une boucle dans la hiérarchie.");
        }
    }

    private async Task<int> CalculerNiveauAsync(long? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return 0;
        }

        var parent = await _repository.GetByIdAsync(parentId.Value, cancellationToken)
            ?? throw new InvalidOperationException("L'item BI parent indiqué n'existe pas.");

        return parent.Niveau + 1;
    }

    internal static IReadOnlyList<ItemBINoeudDto> ConstruireArbre(IReadOnlyList<ItemBIDto> items)
    {
        var ids = items.Select(i => i.IdItemBI).ToHashSet();
        var byParent = items
            .GroupBy(i => i.ParentId is long p && ids.Contains(p) ? p : 0L)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.CodeItem, StringComparer.OrdinalIgnoreCase).ToList());

        List<ItemBINoeudDto> Construire(long? parentId)
        {
            var key = parentId ?? 0L;
            if (!byParent.TryGetValue(key, out var children))
            {
                return [];
            }

            return children
                .Select(child => new ItemBINoeudDto(
                    child.IdItemBI,
                    child.CodeItem,
                    child.Libelle,
                    child.Niveau,
                    child.Categorie,
                    child.Actif,
                    Construire(child.IdItemBI)))
                .ToList();
        }

        return Construire(null);
    }
}
