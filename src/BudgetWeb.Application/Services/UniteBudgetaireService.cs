using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public class UniteBudgetaireService : IUniteBudgetaireService
{
    private readonly IUniteBudgetaireRepository _repository;
    private readonly IPerimetreAccesService _perimetreAcces;

    public UniteBudgetaireService(
        IUniteBudgetaireRepository repository,
        IPerimetreAccesService perimetreAcces)
    {
        _repository = repository;
        _perimetreAcces = perimetreAcces;
    }

    public async Task<IReadOnlyList<UniteBudgetaireDto>> GetAllAsync(
        bool accessiblesSeulement = false,
        string? contexte = null,
        CancellationToken cancellationToken = default)
    {
        var unites = await _repository.GetAllAsync(cancellationToken);
        if (!accessiblesSeulement)
            return unites;

        var idsAutorises = string.Equals(contexte, "dpm", StringComparison.OrdinalIgnoreCase)
            ? await _perimetreAcces.GetIdsUbAutoriseesDpmAsync(cancellationToken)
            : await _perimetreAcces.GetIdsUbAutoriseesSaisieAsync(cancellationToken);

        if (idsAutorises is null)
            return unites;

        var set = idsAutorises.ToHashSet();
        return unites.Where(u => set.Contains(u.IdUB)).ToList();
    }

    public Task<UniteBudgetaireDto?> GetByIdAsync(long idUb, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idUb, cancellationToken);

    public async Task<UniteBudgetaireDto> CreateAsync(
        CreateUniteBudgetaireRequest request,
        CancellationToken cancellationToken = default)
    {
        var (codeUb, libelle, idDepartement, idStructure, actif) = await ValiderAsync(
            request.CodeUB,
            request.Libelle,
            request.IdDepartement,
            request.IdStructure,
            request.Actif,
            excludeId: null,
            cancellationToken);

        return await _repository.CreateAsync(codeUb, libelle, idDepartement, idStructure, actif, cancellationToken);
    }

    public async Task<UniteBudgetaireDto?> UpdateAsync(
        long idUb,
        UpdateUniteBudgetaireRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idUb, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var (codeUb, libelle, idDepartement, idStructure, actif) = await ValiderAsync(
            request.CodeUB,
            request.Libelle,
            request.IdDepartement,
            request.IdStructure,
            request.Actif,
            excludeId: idUb,
            cancellationToken);

        return await _repository.UpdateAsync(idUb, codeUb, libelle, idDepartement, idStructure, actif, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idUb, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idUb, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (await _repository.CountPrevisionsAsync(idUb, cancellationToken) > 0)
        {
            throw new InvalidOperationException(
                "Cette unité budgétaire ne peut pas être supprimée car elle est utilisée par une ou plusieurs prévisions budgétaires.");
        }

        return await _repository.DeleteAsync(idUb, cancellationToken);
    }

    private async Task<(string Code, string Libelle, long IdDepartement, long IdStructure, bool Actif)> ValiderAsync(
        string? codeBrut,
        string? libelleBrut,
        long idDepartement,
        long idStructure,
        bool? actif,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var codeUb = (codeBrut ?? string.Empty).Trim().ToUpperInvariant();
        var libelle = (libelleBrut ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(codeUb))
        {
            throw new InvalidOperationException("Le code de l'unité budgétaire est obligatoire.");
        }

        if (codeUb.Length > 30)
        {
            throw new InvalidOperationException("Le code de l'unité budgétaire ne peut pas dépasser 30 caractères.");
        }

        if (string.IsNullOrWhiteSpace(libelle))
        {
            throw new InvalidOperationException("Le libellé de l'unité budgétaire est obligatoire.");
        }

        if (libelle.Length > 200)
        {
            throw new InvalidOperationException("Le libellé de l'unité budgétaire ne peut pas dépasser 200 caractères.");
        }

        if (idDepartement <= 0)
        {
            throw new InvalidOperationException("Le département est obligatoire.");
        }

        if (idStructure <= 0)
        {
            throw new InvalidOperationException("La structure organisationnelle est obligatoire.");
        }

        if (!await _repository.ExistsDepartementAsync(idDepartement, cancellationToken))
        {
            throw new InvalidOperationException("Le département indiqué n'existe pas.");
        }

        if (!await _repository.ExistsStructureAsync(idStructure, cancellationToken))
        {
            throw new InvalidOperationException("La structure organisationnelle indiquée n'existe pas.");
        }

        var codeDepartement = await _repository.GetCodeDepartementAsync(idDepartement, cancellationToken);
        var codeDepartementStructure = await _repository.GetCodeDepartementOrganisationnelAsync(
            idStructure,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(codeDepartementStructure))
        {
            throw new InvalidOperationException(
                "La structure sélectionnée n'est rattachée à aucun département organisationnel.");
        }

        if (!string.Equals(codeDepartement, codeDepartementStructure, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"La structure sélectionnée n'appartient pas au département {codeDepartement}.");
        }

        if (await _repository.ExistsByCodeAsync(codeUb, excludeId, cancellationToken))
        {
            throw new InvalidOperationException($"Une unité budgétaire avec le code {codeUb} existe déjà.");
        }

        return (codeUb, libelle, idDepartement, idStructure, actif ?? true);
    }
}
