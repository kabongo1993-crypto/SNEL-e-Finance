using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IVersionBudgetaireRepository
{
    Task<IReadOnlyList<VersionBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto?> GetByIdAsync(long idVersion, CancellationToken cancellationToken = default);

    Task<bool> ExistsByExerciceNumeroAsync(
        long idExercice,
        int numeroVersion,
        long? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsExerciceAsync(long idExercice, CancellationToken cancellationToken = default);

    Task<bool> ExistsVersionAsync(long idVersion, CancellationToken cancellationToken = default);

    Task<bool> ExistsUtilisateurAsync(long idUtilisateur, CancellationToken cancellationToken = default);

    Task<bool> WouldCreateCycleAsync(long idVersion, long idVersionPrecedente, CancellationToken cancellationToken = default);

    Task<int> CountPrevisionsAsync(long idVersion, CancellationToken cancellationToken = default);

    Task<int> CountTransfertsAsync(long idVersion, CancellationToken cancellationToken = default);

    Task<int> CountVersionsSuivantesAsync(long idVersion, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UtilisateurLookupDto>> GetUtilisateursAsync(CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto> CreateAsync(
        long idExercice,
        int numeroVersion,
        string libelle,
        long? idVersionPrecedente,
        DateOnly dateDebutEffet,
        DateOnly? dateFinEffet,
        string? motif,
        string statut,
        long idUtilisateurCreation,
        CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto?> UpdateAsync(
        long idVersion,
        long idExercice,
        int numeroVersion,
        string libelle,
        long? idVersionPrecedente,
        DateOnly dateDebutEffet,
        DateOnly? dateFinEffet,
        string? motif,
        long idUtilisateurCreation,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idVersion, CancellationToken cancellationToken = default);

    /// <summary>Applique une transition métier + journal audit, en transaction.</summary>
    Task<VersionBudgetaireDto?> AppliquerTransitionAsync(
        long idVersion,
        string nouveauStatut,
        long idUtilisateur,
        string operation,
        Action<Domain.Entities.VersionBudgetaire> appliquerTrace,
        CancellationToken cancellationToken = default);
}

public interface IVersionBudgetaireService
{
    Task<IReadOnlyList<VersionBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto?> GetByIdAsync(long idVersion, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UtilisateurLookupDto>> GetUtilisateursAsync(CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto> CreateAsync(
        CreateVersionBudgetaireRequest request,
        CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto?> UpdateAsync(
        long idVersion,
        UpdateVersionBudgetaireRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idVersion, CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto> SoumettreAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto> ControlerAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto> ValiderAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default);

    Task<VersionBudgetaireDto> RejeterAsync(
        long idVersion,
        long idUtilisateur,
        string motif,
        CancellationToken cancellationToken = default);

    /// <summary>REJETEE → BROUILLON (action Modifier).</summary>
    Task<VersionBudgetaireDto> ReouvrirAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default);

    /// <summary>Si REJETEE, passe en BROUILLON après enregistrement de prévisions.</summary>
    Task ReouvrirSiRejeteeApresSaisieAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default);
}
