using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public class RubriqueBudgetaireService : IRubriqueBudgetaireService
{
    private readonly IRubriqueBudgetaireRepository _repository;

    public RubriqueBudgetaireService(IRubriqueBudgetaireRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<RubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<RubriqueBudgetaireNoeudDto>> GetArbreAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return ConstruireArbre(items);
    }

    public Task<RubriqueBudgetaireDto?> GetByIdAsync(long idRB, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idRB, cancellationToken);

    public async Task<RubriqueBudgetaireDto> CreateAsync(
        CreateRubriqueBudgetaireRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, libelle, parentId, actif) = await ValiderAsync(
            request.CodeRB,
            request.Libelle,
            request.ParentId,
            request.Actif,
            excludeId: null,
            cancellationToken);

        var niveau = await CalculerNiveauAsync(parentId, cancellationToken);
        return await _repository.CreateAsync(code, libelle, parentId, niveau, actif, cancellationToken);
    }

    public async Task<RubriqueBudgetaireDto?> UpdateAsync(
        long idRB,
        UpdateRubriqueBudgetaireRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idRB, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var (code, libelle, parentId, actif) = await ValiderAsync(
            request.CodeRB,
            request.Libelle,
            request.ParentId,
            request.Actif,
            excludeId: idRB,
            cancellationToken);

        await GarantirAbsenceCycleAsync(idRB, parentId, cancellationToken);

        var niveau = await CalculerNiveauAsync(parentId, cancellationToken);
        return await _repository.UpdateAsync(idRB, code, libelle, parentId, niveau, actif, cancellationToken);
    }

    public async Task<RubriqueBudgetaireDto?> SetActifAsync(
        long idRB,
        SetActifRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idRB, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        return await _repository.SetActifAsync(idRB, request.Actif, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idRB, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idRB, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var enfants = await _repository.CountEnfantsAsync(idRB, cancellationToken);
        var previsions = await _repository.CountPrevisionsAsync(idRB, cancellationToken);

        if (enfants > 0)
        {
            throw new InvalidOperationException(
                "Cette rubrique ne peut pas être supprimée car elle possède une ou plusieurs rubriques enfants. Désactivez-la plutôt.");
        }

        if (previsions > 0)
        {
            throw new InvalidOperationException(
                "Cette rubrique ne peut pas être supprimée car elle est utilisée par une ou plusieurs prévisions budgétaires. Désactivez-la plutôt.");
        }

        return await _repository.DeleteAsync(idRB, cancellationToken);
    }

    private async Task<(string Code, string Libelle, long? ParentId, bool Actif)> ValiderAsync(
        string? codeBrut,
        string? libelleBrut,
        long? parentIdBrut,
        bool? actifBrut,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var code = (codeBrut ?? string.Empty).Trim().ToUpperInvariant();
        var libelle = (libelleBrut ?? string.Empty).Trim();
        var parentId = parentIdBrut is > 0 ? parentIdBrut : null;

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Le code de la rubrique est obligatoire.");
        }

        if (code.Length > 30)
        {
            throw new InvalidOperationException("Le code de la rubrique ne peut pas dépasser 30 caractères.");
        }

        if (string.IsNullOrWhiteSpace(libelle))
        {
            throw new InvalidOperationException("Le libellé de la rubrique est obligatoire.");
        }

        if (libelle.Length > 300)
        {
            throw new InvalidOperationException("Le libellé de la rubrique ne peut pas dépasser 300 caractères.");
        }

        if (parentId is not null && !await _repository.ExistsByIdAsync(parentId.Value, cancellationToken))
        {
            throw new InvalidOperationException("La rubrique parente indiquée n'existe pas.");
        }

        if (await _repository.ExistsByCodeAsync(code, excludeId, cancellationToken))
        {
            throw new InvalidOperationException($"Une rubrique budgétaire avec le code {code} existe déjà.");
        }

        return (code, libelle, parentId, actifBrut ?? true);
    }

    private async Task GarantirAbsenceCycleAsync(long idRB, long? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return;
        }

        if (parentId == idRB)
        {
            throw new InvalidOperationException("Une rubrique ne peut pas être sa propre parente.");
        }

        if (await _repository.WouldCreateCycleAsync(idRB, parentId.Value, cancellationToken))
        {
            throw new InvalidOperationException(
                "La rubrique parente indiquée créerait une boucle dans la hiérarchie.");
        }
    }

    private async Task<int> CalculerNiveauAsync(long? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return 0;
        }

        var parent = await _repository.GetByIdAsync(parentId.Value, cancellationToken)
            ?? throw new InvalidOperationException("La rubrique parente indiquée n'existe pas.");

        return parent.Niveau + 1;
    }

    internal static IReadOnlyList<RubriqueBudgetaireNoeudDto> ConstruireArbre(
        IReadOnlyList<RubriqueBudgetaireDto> items)
    {
        var ids = items.Select(i => i.IdRB).ToHashSet();
        var byParent = items
            .GroupBy(i => i.ParentId is long p && ids.Contains(p) ? p : 0L)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.CodeRB, StringComparer.OrdinalIgnoreCase).ToList());

        List<RubriqueBudgetaireNoeudDto> Construire(long? parentId)
        {
            var key = parentId ?? 0L;
            if (!byParent.TryGetValue(key, out var children))
            {
                return [];
            }

            return children
                .Select(child =>
                {
                    var enfants = Construire(child.IdRB);
                    return new RubriqueBudgetaireNoeudDto(
                        child.IdRB,
                        child.CodeRB,
                        child.Libelle,
                        child.Niveau,
                        child.Actif,
                        EstRupture: enfants.Count > 0 || child.NombreEnfants > 0,
                        Enfants: enfants);
                })
                .ToList();
        }

        return Construire(null);
    }
}
