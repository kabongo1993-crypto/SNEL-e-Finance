using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public sealed class McpAuditService : IMcpAuditService
{
    private readonly IMcpAuditRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public McpAuditService(IMcpAuditRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public Task JournaliserLectureAsync(
        McpJournalRequest request,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();
        var outil = (request.Outil ?? string.Empty).Trim();
        if (outil.Length == 0)
            throw new InvalidOperationException("L'outil MCP est obligatoire.");
        if (outil.Length > 80)
            outil = outil[..80];

        var parametres = request.Parametres;
        if (parametres is { Length: > 2000 })
            parametres = parametres[..2000];

        return _repository.AjouterLectureAsync(
            userId,
            outil,
            parametres,
            request.NombreResultats,
            TruncateIp(adresseIp),
            cancellationToken);
    }

    private static string? TruncateIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var t = ip.Trim();
        return t.Length <= 45 ? t : t[..45];
    }
}
