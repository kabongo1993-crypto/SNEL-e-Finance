using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using Xunit;

namespace BudgetWeb.Mcp.Tests;

public class McpAuditServiceTests
{
    [Fact]
    public async Task JournaliseSansSecret()
    {
        var repo = new FakeRepo();
        var user = new StubUser();
        var svc = new McpAuditService(repo, user);
        await svc.JournaliserLectureAsync(
            new McpJournalRequest("get_budget_situation", """{"idUB":10}""", 3),
            "127.0.0.1");
        Assert.Equal(7, repo.UserId);
        Assert.Equal("get_budget_situation", repo.Outil);
        Assert.DoesNotContain("password", repo.Parametres ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, repo.Nombre);
    }

    private sealed class FakeRepo : IMcpAuditRepository
    {
        public long UserId;
        public string? Outil;
        public string? Parametres;
        public int? Nombre;

        public Task AjouterLectureAsync(long idUtilisateur, string outil, string? parametresFonctionnels, int? nombreResultats, string? adresseIp, CancellationToken cancellationToken = default)
        {
            UserId = idUtilisateur;
            Outil = outil;
            Parametres = parametresFonctionnels;
            Nombre = nombreResultats;
            return Task.CompletedTask;
        }
    }

    private sealed class StubUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public long? UserId => 7;
        public string? Username => "alice";
        public string? DisplayName => "Alice";
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => ["paiements.lire"];
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public long RequireUserId() => 7;
    }
}
