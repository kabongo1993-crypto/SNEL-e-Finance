using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BudgetWeb.Mcp.Tests;

public sealed class McpProductionFactory : WebApplicationFactory<Program>
{
    public const string TestSecret = "ProductionTest-BudgetWeb-SNEL-JwtSigningKey-UseOnlyInTests-64ch!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Mcp:ApiBaseUrl", "http://127.0.0.1:9");
        builder.UseSetting("Mcp:PublicBaseUrl", "https://mcp.test.example");
        builder.UseSetting("Jwt:Issuer", "BudgetWeb-SNEL");
        builder.UseSetting("Jwt:Audience", "BudgetWeb-Client");
        builder.UseSetting("Jwt:SecretKey", TestSecret);
    }
}

public class McpProductionHardeningTests : IAsyncLifetime
{
    private McpProductionFactory? _factory;
    private HttpClient? _client;

    public Task InitializeAsync()
    {
        _factory = new McpProductionFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Metadata_UtilisePublicBaseUrlHttps()
    {
        var meta = await _client!.GetAsync("/.well-known/oauth-authorization-server");
        meta.EnsureSuccessStatusCode();
        var body = await meta.Content.ReadAsStringAsync();
        Assert.Contains("https://mcp.test.example", body, StringComparison.Ordinal);
        Assert.DoesNotContain("client_id_metadata_document_supported", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"http://", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterLoopback_RefuseEnProduction()
    {
        var res = await _client!.PostAsync("/oauth/register", new StringContent(
            """{"redirect_uris":["http://127.0.0.1:9/callback"]}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("invalid_redirect_uri", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterChatGpt_AccepteEnProduction()
    {
        var res = await _client!.PostAsync("/oauth/register", new StringContent(
            """{"redirect_uris":["https://chatgpt.com/connector_platform_oauth_redirect"]}""",
            Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("client_id").GetString()));
    }
}
