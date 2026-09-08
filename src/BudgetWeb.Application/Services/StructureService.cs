using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Services;

public class StructureService : IStructureService
{
    private readonly IStructureRepository _repository;

    public StructureService(IStructureRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<StructureDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<StructureDto?> GetByIdAsync(long idStructure, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idStructure, cancellationToken);

    public async Task<StructureDto> CreateAsync(
        CreateStructureRequest request,
        CancellationToken cancellationToken = default)
    {
        var (typeStructure, code, libelle, parentId, actif) = await ValiderAsync(
            request.TypeStructure,
            request.Code,
            request.Libelle,
            request.ParentId,
            request.Actif,
            excludeId: null,
            cancellationToken);

        return await _repository.CreateAsync(typeStructure, code, libelle, parentId, actif, cancellationToken);
    }

    public async Task<StructureDto?> UpdateAsync(
        long idStructure,
        UpdateStructureRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idStructure, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var (typeStructure, code, libelle, parentId, actif) = await ValiderAsync(
            request.TypeStructure,
            request.Code,
            request.Libelle,
            request.ParentId,
            request.Actif,
            excludeId: idStructure,
            cancellationToken);

        if (parentId == idStructure)
        {
            throw new InvalidOperationException("Une structure ne peut pas être sa propre parente.");
        }

        if (parentId is not null && await _repository.WouldCreateCycleAsync(idStructure, parentId.Value, cancellationToken))
        {
            throw new InvalidOperationException(
                "La structure parente indiquée créerait une boucle dans l'organigramme.");
        }

        return await _repository.UpdateAsync(
            idStructure,
            typeStructure,
            code,
            libelle,
            parentId,
            actif,
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idStructure, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idStructure, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var enfants = await _repository.CountEnfantsAsync(idStructure, cancellationToken);
        var unites = await _repository.CountUnitesBudgetairesAsync(idStructure, cancellationToken);

        if (enfants > 0 && unites > 0)
        {
            throw new InvalidOperationException(
                "Cette structure ne peut pas être supprimée car elle possède des structures enfants et est utilisée par une ou plusieurs unités budgétaires.");
        }

        if (enfants > 0)
        {
            throw new InvalidOperationException(
                "Cette structure ne peut pas être supprimée car elle possède une ou plusieurs structures enfants.");
        }

        if (unites > 0)
        {
            throw new InvalidOperationException(
                "Cette structure ne peut pas être supprimée car elle est utilisée par une ou plusieurs unités budgétaires.");
        }

        return await _repository.DeleteAsync(idStructure, cancellationToken);
    }

    private async Task<(string Type, string Code, string Libelle, long? ParentId, bool Actif)> ValiderAsync(
        string? typeBrut,
        string? codeBrut,
        string? libelleBrut,
        long? parentIdBrut,
        bool? actifBrut,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var typeStructure = (typeBrut ?? string.Empty).Trim().ToUpperInvariant();
        var code = (codeBrut ?? string.Empty).Trim().ToUpperInvariant();
        var libelle = (libelleBrut ?? string.Empty).Trim();
        var parentId = parentIdBrut is > 0 ? parentIdBrut : null;

        if (!TypeStructureOrganisationnelle.IsValid(typeStructure))
        {
            throw new InvalidOperationException("Le type de structure est obligatoire et doit être une valeur reconnue.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Le code de la structure est obligatoire.");
        }

        if (code.Length > 30)
        {
            throw new InvalidOperationException("Le code de la structure ne peut pas dépasser 30 caractères.");
        }

        if (string.IsNullOrWhiteSpace(libelle))
        {
            throw new InvalidOperationException("Le libellé de la structure est obligatoire.");
        }

        if (libelle.Length > 200)
        {
            throw new InvalidOperationException("Le libellé de la structure ne peut pas dépasser 200 caractères.");
        }

        if (typeStructure == TypeStructureOrganisationnelle.Entite && parentId is not null)
        {
            throw new InvalidOperationException("Une entité ne peut pas avoir de structure parente.");
        }

        if (parentId is not null && !await _repository.ExistsByIdAsync(parentId.Value, cancellationToken))
        {
            throw new InvalidOperationException("La structure parente indiquée n'existe pas.");
        }

        if (await _repository.ExistsByCodeAsync(code, excludeId, cancellationToken))
        {
            throw new InvalidOperationException($"Une structure avec le code {code} existe déjà.");
        }

        return (typeStructure, code, libelle, parentId, actifBrut ?? true);
    }
}
