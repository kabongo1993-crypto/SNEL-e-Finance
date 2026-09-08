using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IMcpAuditService
{
    Task JournaliserLectureAsync(McpJournalRequest request, string? adresseIp, CancellationToken cancellationToken = default);
}

public interface IMcpAuditRepository
{
    Task AjouterLectureAsync(
        long idUtilisateur,
        string outil,
        string? parametresFonctionnels,
        int? nombreResultats,
        string? adresseIp,
        CancellationToken cancellationToken = default);
}

