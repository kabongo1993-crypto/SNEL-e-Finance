using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BudgetWeb.IntegrationTests;

public class DatabaseConnectionTests
{
    [Fact]
    public async Task BudgetDbContext_PeutLireTypeBudget()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddDbContext<BudgetDbContext>(options => options.UseSqlServer(connectionString));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();

        var canConnect = await context.Database.CanConnectAsync();
        Assert.True(canConnect, "Impossible de se connecter à SQL Server.");

        var types = await context.TypesBudget.AsNoTracking().ToListAsync();
        Assert.NotEmpty(types);
    }
}
