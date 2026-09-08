using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public class DepartementService : IDepartementService
{
    private readonly IDepartementRepository _repository;

    public DepartementService(IDepartementRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<DepartementDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<DepartementDto?> GetByIdAsync(long idDepartement, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idDepartement, cancellationToken);

    public async Task<DepartementDto> CreateAsync(
        CreateDepartementRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, libelle, actif) = Valider(request.Code, request.Libelle, request.Actif);
        if (await _repository.ExistsByCodeAsync(code, excludeId: null, cancellationToken))
        {
            throw new InvalidOperationException($"Un département avec le code {code} existe déjà.");
        }

        return await _repository.CreateAsync(code, libelle, actif, cancellationToken);
    }

    public async Task<DepartementDto?> UpdateAsync(
        long idDepartement,
        UpdateDepartementRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, libelle, actif) = Valider(request.Code, request.Libelle, request.Actif);
        if (await _repository.ExistsByCodeAsync(code, excludeId: idDepartement, cancellationToken))
        {
            throw new InvalidOperationException($"Un département avec le code {code} existe déjà.");
        }

        return await _repository.UpdateAsync(idDepartement, code, libelle, actif, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idDepartement, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idDepartement, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (await _repository.CountUnitesBudgetairesAsync(idDepartement, cancellationToken) > 0)
        {
            throw new InvalidOperationException(
                "Ce département ne peut pas être supprimé car il est utilisé par une ou plusieurs unités budgétaires.");
        }

        return await _repository.DeleteAsync(idDepartement, cancellationToken);
    }

    private static (string Code, string Libelle, bool Actif) Valider(string? codeBrut, string? libelleBrut, bool? actif)
    {
        var code = (codeBrut ?? string.Empty).Trim().ToUpperInvariant();
        var libelle = (libelleBrut ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Le code du département est obligatoire.");
        }

        if (code.Length > 30)
        {
            throw new InvalidOperationException("Le code du département ne peut pas dépasser 30 caractères.");
        }

        if (string.IsNullOrWhiteSpace(libelle))
        {
            throw new InvalidOperationException("Le libellé du département est obligatoire.");
        }

        if (libelle.Length > 200)
        {
            throw new InvalidOperationException("Le libellé du département ne peut pas dépasser 200 caractères.");
        }

        return (code, libelle, actif ?? true);
    }
}
