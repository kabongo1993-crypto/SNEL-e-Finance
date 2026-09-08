using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface IProvinceRepository
{
    Task<IReadOnlyList<Province>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default);
    Task<Province?> GetByIdAsync(string idProvince, CancellationToken cancellationToken = default);
    Task<bool> IdExistsAsync(string idProvince, string? excludeId, CancellationToken cancellationToken = default);
    Task<bool> LibelleExistsAsync(string libelle, string? excludeId, CancellationToken cancellationToken = default);
    Task<int> CountComptesByIdAsync(string idProvince, CancellationToken cancellationToken = default);
    Task<Province> AddAsync(Province entity, CancellationToken cancellationToken = default);
    Task RenameIdAsync(string currentId, string newId, Province updated, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProvinceService
{
    Task<IReadOnlyList<ProvinceDto>> ListAsync(bool actifsSeulement = false, CancellationToken cancellationToken = default);
    Task<ProvinceDto?> GetByIdAsync(string idProvince, CancellationToken cancellationToken = default);
    Task<ProvinceDto> CreateAsync(CreateProvinceRequest request, CancellationToken cancellationToken = default);
    Task<ProvinceDto?> UpdateAsync(string idProvince, UpdateProvinceRequest request, CancellationToken cancellationToken = default);
}
