using BudgetWeb.Mcp.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BudgetWeb.Mcp.Tests;

public class McpStartupGuardTests
{
    [Fact]
    public void Production_RefuseCleDevConnue()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            McpStartupGuard.Validate(Env("Production"), Config(
                McpStartupGuard.KnownDevelopmentJwtSecret,
                "https://mcp.example.com")));
        Assert.Contains("développement", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_RefusePublicBaseUrlManquant()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            McpStartupGuard.Validate(Env("Production"), Config(
                "ProductionTest-BudgetWeb-SNEL-JwtSigningKey-UseOnlyInTests-64ch!",
                "")));
        Assert.Contains("PublicBaseUrl", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_RefusePublicBaseUrlHttp()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            McpStartupGuard.Validate(Env("Production"), Config(
                "ProductionTest-BudgetWeb-SNEL-JwtSigningKey-UseOnlyInTests-64ch!",
                "http://mcp.example.com")));
        Assert.Contains("HTTPS", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_AccepteHttpsEtSecretNonDev()
    {
        McpStartupGuard.Validate(Env("Production"), Config(
            "ProductionTest-BudgetWeb-SNEL-JwtSigningKey-UseOnlyInTests-64ch!",
            "https://mcp.example.com"));
    }

    [Fact]
    public void Development_AccepteCleDevEtHttpLocal()
    {
        McpStartupGuard.Validate(Env("Development"), Config(
            McpStartupGuard.KnownDevelopmentJwtSecret,
            "http://localhost:5260"));
    }

    private static IHostEnvironment Env(string name) => new StubHostEnvironment { EnvironmentName = name };

    private static IConfiguration Config(string secret, string publicBase)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = secret,
            ["Mcp:PublicBaseUrl"] = publicBase
        }).Build();

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "BudgetWeb.Mcp.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
