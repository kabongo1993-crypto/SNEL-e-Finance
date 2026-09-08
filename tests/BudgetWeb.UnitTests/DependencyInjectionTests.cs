using BudgetWeb.Application;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.SnelComptes.Import.Interfaces;
using BudgetWeb.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BudgetWeb.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void ApplicationEtInfrastructure_EnregistrentLesServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=BD_SNEL;Trusted_Connection=True;TrustServerCertificate=True;"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ICurrentUserService, StubCurrentUser>();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IReferentielService>());
        Assert.NotNull(provider.GetService<IReferentielRepository>());
        Assert.NotNull(provider.GetService<ISnelComptesImportService>());
        Assert.NotNull(provider.GetService<IWorkflowPrevisionUbService>());
        Assert.NotNull(provider.GetService<IWorkflowPrevisionUbRepository>());
        Assert.NotNull(provider.GetService<IMcpAuditService>());
        Assert.NotNull(provider.GetService<IMcpAuditRepository>());
    }

    private sealed class StubCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public long? UserId => 1;
        public string? Username => "stub";
        public string? DisplayName => "Stub";
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => [];
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission) => false;
        public long RequireUserId() => 1;
    }
}
