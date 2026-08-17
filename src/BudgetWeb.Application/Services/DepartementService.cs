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

    public async Task<DepartementDto> CreateAsync(
        CreateDepartementRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormaliserCode(request.Code);
        var libelle = (request.Libelle ?? string.Empty).Trim();

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

        if (await _repository.ExistsByCodeAsync(code, cancellationToken))
        {
            throw new InvalidOperationException($"Un département avec le code {code} existe déjà.");
        }

        var actif = request.Actif ?? true;
        return await _repository.CreateAsync(code, libelle, actif, cancellationToken);
    }

    private static string NormaliserCode(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant();
}
