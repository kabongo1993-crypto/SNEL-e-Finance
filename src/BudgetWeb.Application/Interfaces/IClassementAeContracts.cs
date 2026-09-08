using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IClassementAeRepository
{
    Task<IReadOnlyList<ClassementAeLigneDto>> GetByVersionUbAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAnyAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<int> GetMaxOrdreAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<bool> ExistsItemAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsGroupeAsync(
        long idVersion,
        long idUB,
        long idGroupeItemAE,
        CancellationToken cancellationToken = default);

    Task EnsureGroupeAsync(
        long idVersion,
        long idUB,
        long idGroupeItemAE,
        CancellationToken cancellationToken = default);

    Task EnsureItemAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default);

    Task RenameItemAsync(
        long idVersion,
        long idUB,
        string ancienLibelle,
        string nouveauLibelle,
        CancellationToken cancellationToken = default);

    Task RemoveItemIfUnusedAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default);

    Task PurgeOrphanGroupesAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task ReindexAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task ReorderAsync(
        long idVersion,
        long idUB,
        IReadOnlyList<long> idsOrdonnes,
        CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    /// <summary>Groupes distincts non null pour une action AE (Version×UB×libellé).</summary>
    Task<IReadOnlyList<long?>> GetGroupesDistinctsActionAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsPrevisionAeAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default);

    Task<int> CountPrevisionsAeActionAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        CancellationToken cancellationToken = default);

    Task RenamePrevisionsAeAsync(
        long idVersion,
        long idUB,
        string ancienLibelle,
        string nouveauLibelle,
        CancellationToken cancellationToken = default);

    Task UpdateGroupePrevisionsAeAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        long? idGroupeItemAE,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Paires Version×UB ayant des AE, avec actions (libellé + groupe dominant) pour init.
    /// </summary>
    Task<IReadOnlyList<(long IdVersion, long IdUB, string LibelleItemAE, long? IdGroupe)>> GetActionsAePourInitAsync(
        CancellationToken cancellationToken = default);

    Task<string?> GetLibelleGroupeAsync(long idGroupeItemAE, CancellationToken cancellationToken = default);
}

public interface IClassementAeService
{
    Task<IReadOnlyList<ClassementAeLigneDto>> GetAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default);

    Task ReorderAsync(ReorderClassementAeRequest request, CancellationToken cancellationToken = default);

    Task<ClassementAeInitResultDto> InitialiserManquantsAsync(CancellationToken cancellationToken = default);

    /// <summary>Refuse si l'action a déjà un autre groupe (y compris NULL ≠ groupe).</summary>
    Task GarantirGroupeHomogeneAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        long? idGroupePropose,
        CancellationToken cancellationToken = default);

    Task ApresEcritureAeAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        long? idGroupeItemAE,
        CancellationToken cancellationToken = default);

    Task ApresSuppressionAeAsync(
        long idVersion,
        long idUB,
        string? libelleItemAE,
        CancellationToken cancellationToken = default);

    Task ApresRenommageAeAsync(
        long idVersion,
        long idUB,
        string ancienLibelle,
        string nouveauLibelle,
        long? idGroupeItemAE,
        CancellationToken cancellationToken = default);

    Task ApresChangementGroupeAeAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        long? nouveauGroupe,
        CancellationToken cancellationToken = default);
}
