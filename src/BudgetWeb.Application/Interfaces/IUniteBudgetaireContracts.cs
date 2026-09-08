using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IUniteBudgetaireRepository
{
    Task<IReadOnlyList<UniteBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UniteBudgetaireDto?> GetByIdAsync(long idUb, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string codeUb, long? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsDepartementAsync(long idDepartement, CancellationToken cancellationToken = default);

    Task<bool> ExistsStructureAsync(long idStructure, CancellationToken cancellationToken = default);

    Task<string?> GetCodeDepartementAsync(long idDepartement, CancellationToken cancellationToken = default);

    Task<string?> GetCodeDepartementOrganisationnelAsync(long idStructure, CancellationToken cancellationToken = default);

    Task<int> CountPrevisionsAsync(long idUb, CancellationToken cancellationToken = default);

    Task<UniteBudgetaireDto> CreateAsync(
        string codeUb,
        string libelle,
        long idDepartement,
        long idStructure,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<UniteBudgetaireDto?> UpdateAsync(
        long idUb,
        string codeUb,
        string libelle,
        long idDepartement,
        long idStructure,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idUb, CancellationToken cancellationToken = default);
}

public interface IUniteBudgetaireService
{
    Task<IReadOnlyList<UniteBudgetaireDto>> GetAllAsync(
        bool accessiblesSeulement = false,
        string? contexte = null,
        CancellationToken cancellationToken = default);

    Task<UniteBudgetaireDto?> GetByIdAsync(long idUb, CancellationToken cancellationToken = default);

    Task<UniteBudgetaireDto> CreateAsync(
        CreateUniteBudgetaireRequest request,
        CancellationToken cancellationToken = default);

    Task<UniteBudgetaireDto?> UpdateAsync(
        long idUb,
        UpdateUniteBudgetaireRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idUb, CancellationToken cancellationToken = default);
}
