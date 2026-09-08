using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IWorkflowPrevisionUbRepository
{
    Task<WorkflowPrevisionUbDto?> GetAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task EnsureExistsAsync(long idVersion, long idUB, long idUtilisateur, CancellationToken cancellationToken = default);

    /// <summary>Statut opérationnel Version×UB ; null si aucune ligne (équivaut à BROUILLON pour la saisie).</summary>
    Task<string?> GetStatutAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<bool> ExistsPrevisionForPairAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<bool> ExistsVersionAsync(long idVersion, CancellationToken cancellationToken = default);

    Task<bool> ExistsUbAsync(long idUB, CancellationToken cancellationToken = default);

    Task<(long IdDepartement, string CodeDepartement, string LibelleDepartement)?> GetUbDepartementAsync(
        long idUB,
        CancellationToken cancellationToken = default);

    Task TransitionnerUbAsync(
        long idVersion,
        long idUB,
        string nouveauStatut,
        long idUtilisateur,
        string operationAudit,
        string portee,
        Action<Domain.Entities.WorkflowPrevisionUb> appliquerTrace,
        object? auditExtra,
        CancellationToken cancellationToken = default);

    Task<WorkflowDepartementBulkResultDto> SoumettreDepartementAsync(
        long idVersion,
        long idDepartement,
        long idUtilisateur,
        CancellationToken cancellationToken = default);

    Task<WorkflowDepartementBulkResultDto> ControlerDepartementAsync(
        long idVersion,
        long idDepartement,
        long idUtilisateur,
        CancellationToken cancellationToken = default);

    Task<WorkflowDepartementBulkResultDto> ValiderDepartementAsync(
        long idVersion,
        long idDepartement,
        long idUtilisateur,
        CancellationToken cancellationToken = default);

    Task<WorkflowDepartementBulkResultDto> RejeterDepartementAsync(
        long idVersion,
        long idDepartement,
        string motif,
        long idUtilisateur,
        CancellationToken cancellationToken = default);

    Task RecalculerStatutVersionAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListStatutsUbAsync(long idVersion, CancellationToken cancellationToken = default);
}

public interface IWorkflowPrevisionUbService
{
    Task<WorkflowPrevisionUbDto> SoumettreAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<WorkflowPrevisionUbDto> ControlerAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<WorkflowPrevisionUbDto> ValiderAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<WorkflowPrevisionUbDto> RejeterAsync(long idVersion, long idUB, string motif, CancellationToken cancellationToken = default);

    Task<WorkflowDepartementBulkResultDto> SoumettreDepartementAsync(
        long idVersion,
        long idDepartement,
        CancellationToken cancellationToken = default);

    Task<WorkflowDepartementBulkResultDto> ControlerDepartementAsync(
        long idVersion,
        long idDepartement,
        CancellationToken cancellationToken = default);

    Task<WorkflowDepartementBulkResultDto> ValiderDepartementAsync(
        long idVersion,
        long idDepartement,
        CancellationToken cancellationToken = default);

    Task<WorkflowDepartementBulkResultDto> RejeterDepartementAsync(
        long idVersion,
        long idDepartement,
        string motif,
        CancellationToken cancellationToken = default);

    Task<WorkflowPrevisionUbDto> ReouvrirAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<WorkflowPrevisionUbDto> AnnulerSoumissionAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task EnsureExistsAsync(long idVersion, long idUB, long idUtilisateur, CancellationToken cancellationToken = default);

    /// <summary>Statut opérationnel Version×UB (BROUILLON si aucune ligne workflow).</summary>
    Task<string> GetStatutOperationnelAsync(long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task ReouvrirSiRejeteeApresSaisieAsync(long idVersion, long idUB, long idUtilisateur, CancellationToken cancellationToken = default);

    Task RecalculerStatutVersionAsync(long idVersion, CancellationToken cancellationToken = default);
}
