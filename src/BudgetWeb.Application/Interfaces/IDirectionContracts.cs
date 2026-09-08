using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface IDirectionRepository
{
    Task<IReadOnlyList<DirectionTresorerie>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default);
    Task<DirectionTresorerie?> GetByIdAsync(long idDirection, CancellationToken cancellationToken = default);
    Task<bool> LibelleExistsAsync(string libelle, long? excludeId, CancellationToken cancellationToken = default);
    Task<DirectionTresorerie> AddAsync(DirectionTresorerie entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IDirectionService
{
    Task<IReadOnlyList<DirectionDto>> ListAsync(bool actifsSeulement = false, CancellationToken cancellationToken = default);
    Task<DirectionDto?> GetByIdAsync(long idDirection, CancellationToken cancellationToken = default);
    Task<DirectionDto> CreateAsync(CreateDirectionRequest request, CancellationToken cancellationToken = default);
    Task<DirectionDto?> UpdateAsync(long idDirection, UpdateDirectionRequest request, CancellationToken cancellationToken = default);
}
