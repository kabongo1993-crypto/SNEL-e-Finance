using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public class TypeBudgetService : ITypeBudgetService
{
    private readonly ITypeBudgetRepository _repository;

    public TypeBudgetService(ITypeBudgetRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<TypeBudgetDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<TypeBudgetDto?> GetByIdAsync(long idTypeBudget, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idTypeBudget, cancellationToken);

    public async Task<TypeBudgetDto> CreateAsync(
        CreateTypeBudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        var (codeType, libelle, ordre, actif) = Valider(request.CodeType, request.Libelle, request.OrdreAffichage, request.Actif);
        await GarantirUniciteAsync(codeType, ordre, excludeId: null, cancellationToken);
        return await _repository.CreateAsync(codeType, libelle, ordre, actif, cancellationToken);
    }

    public async Task<TypeBudgetDto?> UpdateAsync(
        long idTypeBudget,
        UpdateTypeBudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        var (codeType, libelle, ordre, actif) = Valider(request.CodeType, request.Libelle, request.OrdreAffichage, request.Actif);
        await GarantirUniciteAsync(codeType, ordre, excludeId: idTypeBudget, cancellationToken);
        return await _repository.UpdateAsync(idTypeBudget, codeType, libelle, ordre, actif, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idTypeBudget, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idTypeBudget, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (await _repository.CountPrevisionsAsync(idTypeBudget, cancellationToken) > 0)
        {
            throw new InvalidOperationException(
                "Ce type de budget ne peut pas être supprimé car il est utilisé par une ou plusieurs prévisions budgétaires.");
        }

        return await _repository.DeleteAsync(idTypeBudget, cancellationToken);
    }

    private async Task GarantirUniciteAsync(
        string codeType,
        int ordreAffichage,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        if (await _repository.ExistsByCodeAsync(codeType, excludeId, cancellationToken))
        {
            throw new InvalidOperationException($"Un type de budget avec le code {codeType} existe déjà.");
        }

        if (await _repository.ExistsByOrdreAsync(ordreAffichage, excludeId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Un type de budget avec l'ordre d'affichage {ordreAffichage} existe déjà.");
        }
    }

    private static (string CodeType, string Libelle, int Ordre, bool Actif) Valider(
        string? codeBrut,
        string? libelleBrut,
        int? ordreAffichage,
        bool? actif)
    {
        var codeType = (codeBrut ?? string.Empty).Trim().ToUpperInvariant();
        var libelle = (libelleBrut ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(codeType))
        {
            throw new InvalidOperationException("Le code du type de budget est obligatoire.");
        }

        if (codeType.Length > 10)
        {
            throw new InvalidOperationException("Le code du type de budget ne peut pas dépasser 10 caractères.");
        }

        if (string.IsNullOrWhiteSpace(libelle))
        {
            throw new InvalidOperationException("Le libellé du type de budget est obligatoire.");
        }

        if (libelle.Length > 100)
        {
            throw new InvalidOperationException("Le libellé du type de budget ne peut pas dépasser 100 caractères.");
        }

        if (ordreAffichage is null)
        {
            throw new InvalidOperationException("L'ordre d'affichage est obligatoire.");
        }

        return (codeType, libelle, ordreAffichage.Value, actif ?? true);
    }
}
