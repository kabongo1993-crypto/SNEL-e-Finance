using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IUtilisateurAdminService
{
    Task<IReadOnlyList<UtilisateurAdminListItemDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<UtilisateurAdminDetailDto?> GetByIdAsync(long idUtilisateur, CancellationToken cancellationToken = default);
    Task<UtilisateurAdminDetailDto> CreateAsync(CreateUtilisateurAdminRequest request, CancellationToken cancellationToken = default);
    Task<UtilisateurAdminDetailDto?> UpdateAsync(long idUtilisateur, UpdateUtilisateurAdminRequest request, CancellationToken cancellationToken = default);
    Task<UtilisateurAdminDetailDto?> SetActifAsync(long idUtilisateur, bool actif, CancellationToken cancellationToken = default);
    Task ResetMotDePasseAsync(long idUtilisateur, ResetMotDePasseAdminRequest request, CancellationToken cancellationToken = default);
    Task<ProfilsCatalogueDto> GetCatalogueAsync(CancellationToken cancellationToken = default);
}

public interface IUtilisateurAdminRepository
{
    Task<IReadOnlyList<UtilisateurAdminListItemDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<UtilisateurAdminDetailDto?> GetDetailAsync(long idUtilisateur, CancellationToken cancellationToken = default);
    Task<bool> ExistsNomUtilisateurAsync(string nomUtilisateur, long? excludeId, CancellationToken cancellationToken = default);
    Task<bool> ExistsMatriculeAsync(string matricule, long? excludeId, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(
        Domain.Entities.Utilisateur utilisateur,
        IReadOnlyList<string> profils,
        IReadOnlyList<string> permissionsIndividuelles,
        PerimetreUtilisateurDto perimetre,
        CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(
        long idUtilisateur,
        Action<Domain.Entities.Utilisateur> applyIdentity,
        IReadOnlyList<string> profils,
        IReadOnlyList<string> permissionsIndividuelles,
        PerimetreUtilisateurDto perimetre,
        CancellationToken cancellationToken = default);
    Task<bool> SetActifAsync(long idUtilisateur, bool actif, CancellationToken cancellationToken = default);
    Task<bool> UpdateMotDePasseHashAsync(long idUtilisateur, string hash, CancellationToken cancellationToken = default);
    Task<bool> DepartementExistsAsync(long idDepartement, CancellationToken cancellationToken = default);
    Task<bool> StructureExistsAsync(long idStructure, CancellationToken cancellationToken = default);
    Task<bool> UbExistsAsync(long idUb, CancellationToken cancellationToken = default);
    Task<(long IdDepartement, bool Actif)?> GetUbDepartementAsync(long idUb, CancellationToken cancellationToken = default);
}
