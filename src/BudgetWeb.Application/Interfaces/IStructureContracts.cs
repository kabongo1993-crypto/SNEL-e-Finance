using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IStructureRepository
{
    Task<IReadOnlyList<StructureDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<StructureDto?> GetByIdAsync(long idStructure, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, long? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsByIdAsync(long idStructure, CancellationToken cancellationToken = default);

    Task<bool> WouldCreateCycleAsync(long idStructure, long parentId, CancellationToken cancellationToken = default);

    Task<int> CountEnfantsAsync(long idStructure, CancellationToken cancellationToken = default);

    Task<int> CountUnitesBudgetairesAsync(long idStructure, CancellationToken cancellationToken = default);

    Task<StructureDto> CreateAsync(
        string typeStructure,
        string code,
        string libelle,
        long? parentId,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<StructureDto?> UpdateAsync(
        long idStructure,
        string typeStructure,
        string code,
        string libelle,
        long? parentId,
        bool actif,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idStructure, CancellationToken cancellationToken = default);
}

public interface IStructureService
{
    Task<IReadOnlyList<StructureDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<StructureDto?> GetByIdAsync(long idStructure, CancellationToken cancellationToken = default);

    Task<StructureDto> CreateAsync(
        CreateStructureRequest request,
        CancellationToken cancellationToken = default);

    Task<StructureDto?> UpdateAsync(
        long idStructure,
        UpdateStructureRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idStructure, CancellationToken cancellationToken = default);
}
