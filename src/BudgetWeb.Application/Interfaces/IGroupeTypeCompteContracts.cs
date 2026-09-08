using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface IGroupeTypeCompteRepository
{
    Task<IReadOnlyList<GroupeTypeCompte>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default);
    Task<GroupeTypeCompte?> GetByIdAsync(long idGroupeTypeCompte, CancellationToken cancellationToken = default);
    Task<bool> LibelleExistsAsync(string libelle, long? excludeId, CancellationToken cancellationToken = default);
    Task<GroupeTypeCompte> AddAsync(GroupeTypeCompte entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IGroupeTypeCompteService
{
    Task<IReadOnlyList<GroupeTypeCompteDto>> ListAsync(bool actifsSeulement = false, CancellationToken cancellationToken = default);
    Task<GroupeTypeCompteDto?> GetByIdAsync(long idGroupeTypeCompte, CancellationToken cancellationToken = default);
    Task<GroupeTypeCompteDto> CreateAsync(CreateGroupeTypeCompteRequest request, CancellationToken cancellationToken = default);
    Task<GroupeTypeCompteDto?> UpdateAsync(long idGroupeTypeCompte, UpdateGroupeTypeCompteRequest request, CancellationToken cancellationToken = default);
}
