using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces;

public interface ICompteCategorieRepository
{
    Task<IReadOnlyList<CompteCategorie>> ListByCompteAsync(long idCompte, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompteCategorie>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<CompteCategorie?> GetByIdAsync(long idCompteCategorie, CancellationToken cancellationToken = default);
    Task<CompteCategorie?> GetTrackedByIdAsync(long idCompteCategorie, CancellationToken cancellationToken = default);
    Task<bool> ExistsIdAsync(long idCompteCategorie, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<long>> ListIdsAsync(CancellationToken cancellationToken = default);
    Task<CompteCategorie> AddAsync(CompteCategorie entity, CancellationToken cancellationToken = default);
    Task AddRangeWithExplicitIdsAsync(IReadOnlyList<CompteCategorie> entities, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
}

public interface ICompteCategorieService
{
    Task<IReadOnlyList<CompteCategorieDto>> ListByCompteAsync(long idCompte, CancellationToken cancellationToken = default);
    Task<CompteCategorieDto> CreateAsync(long idCompte, UpsertCompteCategorieRequest request, CancellationToken cancellationToken = default);
    Task<CompteCategorieDto?> UpdateAsync(long idCompte, long idCompteCategorie, UpsertCompteCategorieRequest request, CancellationToken cancellationToken = default);
    Task<CompteCategorieDto?> CloturerAsync(long idCompte, long idCompteCategorie, CloturerCompteCategorieRequest request, CancellationToken cancellationToken = default);
    Task<ImportCompteCategoriesPreviewDto> PreviewImportAsync(ImportCompteCategoriesPreviewRequest request, CancellationToken cancellationToken = default);
    Task<ImportCompteCategoriesResultDto> ImportAsync(ImportCompteCategoriesRequest request, CancellationToken cancellationToken = default);
}
