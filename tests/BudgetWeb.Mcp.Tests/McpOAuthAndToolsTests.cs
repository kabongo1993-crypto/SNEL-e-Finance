using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using BudgetWeb.Mcp.Auth;
using Xunit;

namespace BudgetWeb.Mcp.Tests;

public sealed class FakeBudgetWebApi : IAsyncDisposable
{
    private WebApplication? _app;
    public string BaseUrl { get; private set; } = string.Empty;
    public const string JwtSecret = "DEV-ONLY-BudgetWeb-SNEL-JwtSigningKey-ChangeInProd-64chars!!";

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.MapPost("/api/v1/auth/login", async (HttpContext ctx) =>
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            var user = doc.RootElement.GetProperty("nomUtilisateur").GetString();
            var pwd = doc.RootElement.GetProperty("motDePasse").GetString();
            if (user == "inactive")
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsJsonAsync(new { message = "Ce compte utilisateur est désactivé." });
                return;
            }

            if (user == "missing" || pwd != "ok")
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsJsonAsync(new { message = "Nom d'utilisateur ou mot de passe incorrect." });
                return;
            }

            var perms = user == "admin"
                ? new[] { "admin.all" }
                : user == "controle"
                    ? new[] { "versions.controler" }
                    : new[] { "paiements.lire" };
            var uid = user == "admin" ? 99L : 7L;
            var jwt = CreateJwt(uid, user ?? "user", perms, user == "admin" ? ["User Admin Full"] : []);
            await ctx.Response.WriteAsJsonAsync(new
            {
                accessToken = jwt,
                tokenType = "Bearer",
                expiresAtUtc = DateTime.UtcNow.AddHours(8),
                utilisateur = new { idUtilisateur = uid, nomUtilisateur = user, permissions = perms }
            });
        });

        app.MapGet("/api/v1/auth/me", (HttpContext ctx) =>
        {
            if (!IsBearer(ctx)) return Results.Unauthorized();
            var perms = IsAdmin(ctx) ? new[] { "admin.all" } : new[] { "paiements.lire" };
            return Results.Json(new { utilisateur = new { idUtilisateur = 7, permissions = perms } });
        });

        app.MapGet("/api/v1/unites-budgetaires", (HttpContext ctx) =>
        {
            if (!IsBearer(ctx)) return Results.Unauthorized();
            return Results.Json(new[] { new { idUB = 10L, codeUB = "UB10" } });
        });

        app.MapGet("/api/v1/suivi-previsions/mes-previsions", (HttpContext ctx) =>
        {
            if (!IsBearer(ctx)) return Results.Unauthorized();
            return Results.Json(new { lignes = new[] { new { idUB = 10, montantTotal = 1000 } } });
        });

        app.MapGet("/api/v1/suivi-previsions/ub-detail", (HttpContext ctx) =>
        {
            if (!IsBearer(ctx)) return Results.Unauthorized();
            var idUb = ctx.Request.Query["idUB"].ToString();
            if (idUb == "99")
                return Results.Json(new { message = "Vous n'avez pas accès au détail de cette unité budgétaire." }, statusCode: 401);
            return Results.Json(new { idUB = 10, montantDC = 100m, montantAE = 50m, montantBI = 20m, montantTotal = 170m });
        });

        app.MapGet("/api/v1/demandes-paiement", (HttpContext ctx) =>
        {
            if (!IsBearer(ctx)) return Results.Unauthorized();
            var idUb = ctx.Request.Query["idUB"].ToString();
            if (idUb == "99")
                return Results.Json(new { message = "Vous n'avez pas accès à cette unité budgétaire." }, statusCode: 401);
            var statut = ctx.Request.Query["statut"].ToString();
            if (string.Equals(statut, "VIDE", StringComparison.OrdinalIgnoreCase))
                return Results.Json(Array.Empty<object>());

            var rows = new List<object>();
            for (var i = 1; i <= 60; i++)
            {
                rows.Add(new
                {
                    idDemandePaiement = i,
                    reference = $"DPM-{i:0000}",
                    dateEmission = $"2026-01-{(i % 28) + 1:D2}",
                    dateCreation = DateTime.UtcNow.AddDays(-i).ToString("o"),
                    objet = "Paiement " + new string('x', 200) + " " + i,
                    montantBrut = 1000m + i,
                    montantUsd = 1000m + i,
                    devise = "USD",
                    codeUB = "UB10",
                    libelleUB = "Direction Test",
                    statut = i == 1 ? "SOUMISE" : "BROUILLON",
                    libelleDemandeur = "Demandeur " + i
                });
            }

            return Results.Json(rows);
        });

        app.MapGet("/api/v1/demandes-paiement/{id:long}", (HttpContext ctx, long id) =>
        {
            if (!IsBearer(ctx)) return Results.Unauthorized();
            if (id == 99)
                return Results.Json(new { message = "Vous n'avez pas accès à cette demande de paiement." }, statusCode: 401);
            return Results.Json(new
            {
                idDemandePaiement = id,
                reference = $"DPM-{id:0000}",
                beneficiaires = new[]
                {
                    new { nomComplet = "ACME SARL", estPrincipal = true, raisonSociale = "ACME SARL" }
                }
            });
        });

        app.MapPost("/api/v1/mcp/journal", () => Results.NoContent());

        await app.StartAsync();
        _app = app;
        BaseUrl = app.Urls.First();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }

    public static string CreateJwt(long userId, string username, string[] permissions, string[] roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("uid", userId.ToString()),
            new("uname", username),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username)
        };
        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));
        foreach (var p in permissions)
            claims.Add(new Claim("permission", p));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var token = new JwtSecurityToken(
            issuer: "BudgetWeb-SNEL",
            audience: "BudgetWeb-Client",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool IsBearer(HttpContext ctx)
        => ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdmin(HttpContext ctx)
        => ctx.Request.Headers.Authorization.ToString().Contains("admin", StringComparison.OrdinalIgnoreCase);
}

public sealed class McpFactory : WebApplicationFactory<Program>
{
    private readonly string _apiBase;
    public ConcurrentBag<string> Logs { get; } = new();

    public McpFactory(string apiBase) => _apiBase = apiBase;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Mcp:ApiBaseUrl", _apiBase);
        builder.UseSetting("Mcp:PublicBaseUrl", "http://localhost");
        builder.UseSetting("Jwt:Issuer", "BudgetWeb-SNEL");
        builder.UseSetting("Jwt:Audience", "BudgetWeb-Client");
        builder.UseSetting("Jwt:SecretKey", FakeBudgetWebApi.JwtSecret);
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new CapturingLoggerProvider(Logs));
        });
    }
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentBag<string> _logs;
    public CapturingLoggerProvider(ConcurrentBag<string> logs) => _logs = logs;
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_logs, categoryName);
    public void Dispose() { }
}

internal sealed class CapturingLogger : ILogger
{
    private readonly ConcurrentBag<string> _logs;
    private readonly string _category;
    public CapturingLogger(ConcurrentBag<string> logs, string category)
    {
        _logs = logs;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var line = $"{_category}: {formatter(state, exception)}";
        if (exception is not null)
            line += " " + exception;
        _logs.Add(line);
    }
}

public class McpOAuthAndToolsTests : IAsyncLifetime
{
    private readonly FakeBudgetWebApi _api = new();
    private McpFactory? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _api.StartAsync();
        _factory = new McpFactory(_api.BaseUrl);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task HealthEtWellKnown_SontPublics()
    {
        var health = await _client!.GetAsync("/health");
        health.EnsureSuccessStatusCode();
        var meta = await _client.GetAsync("/.well-known/oauth-authorization-server");
        meta.EnsureSuccessStatusCode();
        Assert.Equal("application/json", meta.Content.Headers.ContentType?.MediaType);
        var pr = await _client.GetAsync("/.well-known/oauth-protected-resource");
        pr.EnsureSuccessStatusCode();
        Assert.Equal("application/json", pr.Content.Headers.ContentType?.MediaType);
        var mcpPath = await _client.GetAsync("/.well-known/oauth-authorization-server/mcp");
        mcpPath.EnsureSuccessStatusCode();
        var prMcp = await _client.GetAsync("/.well-known/oauth-protected-resource/mcp");
        prMcp.EnsureSuccessStatusCode();
        var asUnderMcp = await _client.GetAsync("/mcp/.well-known/oauth-authorization-server");
        asUnderMcp.EnsureSuccessStatusCode();
        var prUnderMcp = await _client.GetAsync("/mcp/.well-known/oauth-protected-resource");
        prUnderMcp.EnsureSuccessStatusCode();
        var metaBody = await meta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("client_id_metadata_document_supported", metaBody, StringComparison.Ordinal);
        Assert.DoesNotContain("openid", metaBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("offline_access", metaBody, StringComparison.Ordinal);
        Assert.Contains("refresh_token", metaBody, StringComparison.Ordinal);
        Assert.Contains("budgetweb.read", metaBody, StringComparison.Ordinal);
        var prBody = await pr.Content.ReadAsStringAsync();
        Assert.Contains("\"resource\":\"http://localhost/mcp\"", prBody.Replace(" ", ""), StringComparison.Ordinal);
        Assert.Contains("offline_access", prBody, StringComparison.Ordinal);
        var oidc = await _client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.NotFound, oidc.StatusCode);
    }

    [Fact]
    public async Task McpSansToken_Retourne401AvecResourceMetadata()
    {
        var res = await _client!.PostAsync("/mcp", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Contains("resource_metadata", res.Headers.WwwAuthenticate.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/.well-known/oauth-protected-resource", res.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);

        var get = await _client.GetAsync("/mcp");
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Contains("resource_metadata", get.Headers.WwwAuthenticate.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("budgetweb.read", get.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_AccepteClientPublicPkce()
    {
        var res = await _client!.PostAsync("/oauth/register", new StringContent(
            """
            {"client_name":"ChatGPT","redirect_uris":["http://127.0.0.1:9/callback"],"grant_types":["authorization_code","refresh_token"],"response_types":["code"],"token_endpoint_auth_method":"none"}
            """, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("client_id").GetString()));
        Assert.Equal("none", doc.RootElement.GetProperty("token_endpoint_auth_method").GetString());
        Assert.Contains("offline_access", doc.RootElement.GetProperty("scope").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizationCode_UsageUniqueEtPkce()
    {
        var (verifier, challenge) = MakePkce();
        var code = await TryLoginAsync("alice", "ok", verifier, challenge);
        Assert.NotNull(code);

        var first = await ExchangeCodeAsync(code!, verifier, _lastClientId);
        first.EnsureSuccessStatusCode();
        using var firstDoc = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.False(firstDoc.RootElement.TryGetProperty("refresh_token", out _));

        var reuse = await ExchangeCodeAsync(code!, verifier, _lastClientId);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        var (verifier2, challenge2) = MakePkce();
        var code2 = await TryLoginAsync("alice", "ok", verifier2, challenge2);
        var badPkce = await ExchangeCodeAsync(code2!, verifier, _lastClientId);
        Assert.Equal(HttpStatusCode.BadRequest, badPkce.StatusCode);
        Assert.Contains("invalid_grant", await badPkce.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizationCode_Expire()
    {
        var (verifier, challenge) = MakePkce();
        await TryLoginAsync("alice", "ok", verifier, challenge);
        var store = _factory!.Services.GetRequiredService<IOAuthAuthorizationStore>();
        var expiredCode = "expiredcode" + Guid.NewGuid().ToString("N");
        store.StoreCode(new AuthorizationCodeRecord
        {
            Code = expiredCode,
            ClientId = _lastClientId,
            RedirectUri = "http://127.0.0.1:9/callback",
            CodeChallenge = challenge,
            AccessToken = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(8),
            Resource = "http://localhost/mcp",
            Scope = "budgetweb.read",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        var res = await ExchangeCodeAsync(expiredCode, verifier, _lastClientId);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_EmetEtTourne()
    {
        var (verifier, challenge) = MakePkce();
        var code = await TryLoginAsync("alice", "ok", verifier, challenge, "budgetweb.read offline_access");
        var tokenRes = await ExchangeCodeAsync(code!, verifier, _lastClientId);
        tokenRes.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync());
        var access = doc.RootElement.GetProperty("access_token").GetString()!;
        var refresh = doc.RootElement.GetProperty("refresh_token").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(refresh));
        Assert.DoesNotContain('.', refresh);

        var mcp = await McpAsync(access, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"tests","version":"1"}}}""");
        mcp.EnsureSuccessStatusCode();

        var refreshed = await _client!.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
            ["client_id"] = _lastClientId
        }));
        refreshed.EnsureSuccessStatusCode();
        using var doc2 = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var access2 = doc2.RootElement.GetProperty("access_token").GetString();
        var refresh2 = doc2.RootElement.GetProperty("refresh_token").GetString();
        Assert.Equal(access, access2);
        Assert.False(string.IsNullOrWhiteSpace(refresh2));
        Assert.NotEqual(refresh, refresh2);

        var reused = await _client.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
            ["client_id"] = _lastClientId
        }));
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_ExpireOuMauvaisClient_Refuse()
    {
        var store = _factory!.Services.GetRequiredService<IOAuthAuthorizationStore>();
        var jwt = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []);
        var expired = store.IssueRefresh(new RefreshTokenRecord
        {
            TokenHash = string.Empty,
            ClientId = "client-x",
            AccessToken = jwt,
            JwtExpiresAtUtc = DateTime.UtcNow.AddHours(8),
            Resource = "http://localhost/mcp",
            Scope = "budgetweb.read offline_access",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        var res = await _client!.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = expired,
            ["client_id"] = "client-x"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var live = store.IssueRefresh(new RefreshTokenRecord
        {
            TokenHash = string.Empty,
            ClientId = "client-x",
            AccessToken = jwt,
            JwtExpiresAtUtc = DateTime.UtcNow.AddHours(8),
            Resource = "http://localhost/mcp",
            Scope = "budgetweb.read offline_access"
        });
        var wrongClient = await _client.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = live,
            ["client_id"] = "autre-client"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, wrongClient.StatusCode);
    }

    [Fact]
    public async Task Token_MauvaisRedirectOuResource_Refuse()
    {
        var (verifier, challenge) = MakePkce();
        var code = await TryLoginAsync("alice", "ok", verifier, challenge);
        var badRedirect = await _client!.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1:9/other",
            ["code_verifier"] = verifier,
            ["client_id"] = _lastClientId
        }));
        Assert.Equal(HttpStatusCode.BadRequest, badRedirect.StatusCode);

        var (verifier2, challenge2) = MakePkce();
        var code2 = await TryLoginAsync("alice", "ok", verifier2, challenge2);
        var badResource = await _client.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code2!,
            ["redirect_uri"] = "http://127.0.0.1:9/callback",
            ["code_verifier"] = verifier2,
            ["client_id"] = _lastClientId,
            ["resource"] = "https://evil.example/mcp"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, badResource.StatusCode);
        Assert.Contains("invalid_target", await badResource.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_MauvaisResource_Refuse()
    {
        var (verifier, challenge) = MakePkce();
        await TryLoginAsync("alice", "ok", verifier, challenge);
        var authorize = await _client!.GetAsync(
            $"/oauth/authorize?response_type=code&client_id={_lastClientId}&redirect_uri={Uri.EscapeDataString("http://127.0.0.1:9/callback")}&state=st&code_challenge={challenge}&code_challenge_method=S256&resource={Uri.EscapeDataString("https://evil.example/mcp")}");
        Assert.Equal(HttpStatusCode.BadRequest, authorize.StatusCode);
    }

    [Fact]
    public async Task OAuth_NeJournalisePasSecrets()
    {
        var (verifier, challenge) = MakePkce();
        await CompleteOAuthAsync("alice", "ok", verifier, challenge);
        var joined = string.Join('\n', _factory!.Logs);
        Assert.DoesNotContain("eyJ", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("motDePasse", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jwt:SecretKey", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", joined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TokenExpireOuSignatureInvalide_Refuse()
    {
        var bad = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        bad.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-jwt");
        var res = await _client!.SendAsync(bad);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);

        var expired = CreateExpiredJwt();
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expired);
        var res2 = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, res2.StatusCode);
    }

    [Fact]
    public async Task LoginInactifEtInconnu_Bloques()
    {
        var (verifier, challenge) = MakePkce();
        var codeInactif = await TryLoginAsync("inactive", "ok", verifier, challenge);
        Assert.Null(codeInactif);
        var codeMissing = await TryLoginAsync("missing", "ok", verifier, challenge);
        Assert.Null(codeMissing);
    }

    [Fact]
    public async Task OAuthPkce_ProduitJwtBudgetWeb()
    {
        var (verifier, challenge) = MakePkce();
        var token = await CompleteOAuthAsync("alice", "ok", verifier, challenge);
        Assert.False(string.IsNullOrWhiteSpace(token));
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Equal("7", jwt.Claims.First(c => c.Type == "uid").Value);
    }

    [Fact]
    public async Task TokenSansClientId_Refuse()
    {
        var (verifier, challenge) = MakePkce();
        var code = await TryLoginAsync("alice", "ok", verifier, challenge);
        Assert.NotNull(code);
        var tokenRes = await _client!.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1:9/callback",
            ["code_verifier"] = verifier
        }));
        Assert.Equal(HttpStatusCode.BadRequest, tokenRes.StatusCode);
        var body = await tokenRes.Content.ReadAsStringAsync();
        Assert.Contains("invalid_grant", body, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenClientIdIncoherent_Refuse()
    {
        var (verifier, challenge) = MakePkce();
        var code = await TryLoginAsync("alice", "ok", verifier, challenge);
        Assert.NotNull(code);
        var tokenRes = await _client!.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1:9/callback",
            ["code_verifier"] = verifier,
            ["client_id"] = "client-inconnu"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, tokenRes.StatusCode);
        var body = await tokenRes.Content.ReadAsStringAsync();
        Assert.Contains("invalid_grant", body, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", body, StringComparison.Ordinal);
    }


    [Fact]
    public async Task ToolsList_ExposeOutilsMetierSansSql()
    {
        var token = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []);
        var init = await McpAsync(token, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"tests","version":"1"}}}""");
        init.EnsureSuccessStatusCode();
        var list = await McpAsync(token, """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
        list.EnsureSuccessStatusCode();
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains("get_budget_situation", body, StringComparison.Ordinal);
        Assert.Contains("search_demandes_paiement", body, StringComparison.Ordinal);
        Assert.Contains("get_latest_demande_paiement", body, StringComparison.Ordinal);
        Assert.Contains("get_demande_paiement", body, StringComparison.Ordinal);
        Assert.Contains("list_referentiels", body, StringComparison.Ordinal);
        Assert.DoesNotContain("execute_sql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("query_database", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("run_sql", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UbHorsPerimetre_EstRefuseeParApi()
    {
        var token = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []);
        var call = await McpAsync(token, """
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_budget_situation","arguments":{"idVersion":1,"idUB":99}}}
""");
        call.EnsureSuccessStatusCode();
        var body = await call.Content.ReadAsStringAsync();
        Assert.DoesNotContain("montantDC", body, StringComparison.Ordinal);
        Assert.True(
            body.Contains("authentifi", StringComparison.OrdinalIgnoreCase)
            || body.Contains("avez", StringComparison.OrdinalIgnoreCase)
            || body.Contains("isError", StringComparison.OrdinalIgnoreCase)
            || body.Contains("refus", StringComparison.OrdinalIgnoreCase),
            "Une UB hors périmètre ne doit pas renvoyer les totaux budgétaires.");

    }

    [Fact]
    public async Task SearchDemandesPaiement_RetournePageCompacteSansTroncature()
    {
        var token = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []);
        await InitializeMcpAsync(token);
        var text = await CallToolTextAsync(token, "search_demandes_paiement", """{"take":5,"skip":0}""");
        Assert.DoesNotContain("réponse tronquée", text, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(text);
        var pag = doc.RootElement.GetProperty("pagination");
        Assert.Equal(60, pag.GetProperty("total").GetInt32());
        Assert.Equal(5, pag.GetProperty("returned").GetInt32());
        Assert.Equal("dateCreation DESC", pag.GetProperty("orderBy").GetString());
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(5, items.GetArrayLength());
        Assert.Equal(1, items[0].GetProperty("id").GetInt64());
        Assert.Equal("DPM-0001", items[0].GetProperty("reference").GetString());
        Assert.True(text.Length < 8000);
    }

    [Fact]
    public async Task SearchDemandesPaiement_PaginationPagesDistinctes()
    {
        var token = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []);
        await InitializeMcpAsync(token);
        var p1 = await CallToolTextAsync(token, "search_demandes_paiement", """{"take":5,"skip":0}""");
        var p2 = await CallToolTextAsync(token, "search_demandes_paiement", """{"take":5,"skip":5}""");
        var p3 = await CallToolTextAsync(token, "search_demandes_paiement", """{"take":5,"skip":10}""");
        using var d1 = JsonDocument.Parse(p1);
        using var d2 = JsonDocument.Parse(p2);
        using var d3 = JsonDocument.Parse(p3);
        var id1 = d1.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64();
        var id2 = d2.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64();
        var id3 = d3.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64();
        Assert.Equal(1, id1);
        Assert.Equal(6, id2);
        Assert.Equal(11, id3);
        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id2, id3);
    }

    [Fact]
    public async Task GetLatestDemandePaiement_RetourneLaPlusRecente()
    {
        var token = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []);
        await InitializeMcpAsync(token);
        var text = await CallToolTextAsync(token, "get_latest_demande_paiement", "{}");
        using var doc = JsonDocument.Parse(text);
        Assert.True(doc.RootElement.GetProperty("found").GetBoolean());
        var dpm = doc.RootElement.GetProperty("demandePaiement");
        Assert.Equal(1, dpm.GetProperty("id").GetInt64());
        Assert.Equal("DPM-0001", dpm.GetProperty("reference").GetString());
        Assert.Equal("SOUMISE", dpm.GetProperty("statut").GetString());
        Assert.Equal("ACME SARL", dpm.GetProperty("beneficiaire").GetString());
        Assert.False(string.IsNullOrWhiteSpace(dpm.GetProperty("dateEnregistrement").GetString()));
        Assert.DoesNotContain("\"items\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestDemandePaiement_AucunResultat_FoundFalse()
    {
        var token = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []);
        await InitializeMcpAsync(token);
        var text = await CallToolTextAsync(token, "get_latest_demande_paiement", """{"statut":"VIDE"}""");
        using var doc = JsonDocument.Parse(text);
        Assert.False(doc.RootElement.GetProperty("found").GetBoolean());
        Assert.False(doc.RootElement.TryGetProperty("demandePaiement", out _));
    }

    [Fact]
    public async Task SearchDemandesPaiement_UbHorsPerimetre_RefuseeParApi()
    {
        var token = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []);
        await InitializeMcpAsync(token);
        var text = await CallToolTextAsync(token, "search_demandes_paiement", """{"idUB":99}""");
        Assert.DoesNotContain("DPM-", text, StringComparison.Ordinal);
        Assert.True(
            text.Contains("avez", StringComparison.OrdinalIgnoreCase)
            || text.Contains("authentifi", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unité", StringComparison.OrdinalIgnoreCase),
            "Une UB hors périmètre ne doit pas lister de DPM.");
    }

    [Fact]
    public async Task GetLatestDemandePaiement_UbHorsPerimetre_RefuseeParApi()
    {
        var token = FakeBudgetWebApi.CreateJwt(7, "alice", ["paiements.lire"], []);
        await InitializeMcpAsync(token);
        var text = await CallToolTextAsync(token, "get_latest_demande_paiement", """{"idUB":99}""");
        Assert.DoesNotContain("\"found\":true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DPM-", text, StringComparison.Ordinal);
    }

    private async Task InitializeMcpAsync(string token)
    {
        var init = await McpAsync(token, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"tests","version":"1"}}}""");
        init.EnsureSuccessStatusCode();
    }

    private async Task<string> CallToolTextAsync(string token, string name, string argumentsJson)
    {
        var payload = "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\""
            + name + "\",\"arguments\":" + argumentsJson + "}}";
        var call = await McpAsync(token, payload);
        call.EnsureSuccessStatusCode();
        return ExtractMcpText(await call.Content.ReadAsStringAsync());
    }

    private static string ExtractMcpText(string body)
    {
        var json = body;
        if (body.Contains("data:", StringComparison.Ordinal))
        {
            var sb = new StringBuilder();
            foreach (var line in body.Split('\n'))
            {
                var t = line.TrimEnd('\r');
                if (t.StartsWith("data:", StringComparison.Ordinal))
                    sb.Append(t[5..].Trim());
            }
            if (sb.Length > 0)
                json = sb.ToString();
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("result", out var result)
            && result.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array
            && content.GetArrayLength() > 0
            && content[0].TryGetProperty("text", out var textEl))
        {
            return textEl.GetString() ?? json;
        }

        return json;
    }

    private async Task<HttpResponseMessage> McpAsync(string token, string json)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.ParseAdd("application/json");
        req.Headers.Accept.ParseAdd("text/event-stream");
        return await _client!.SendAsync(req);
    }

    private async Task<string> CompleteOAuthAsync(string user, string password, string verifier, string challenge)
    {
        var code = await TryLoginAsync(user, password, verifier, challenge);
        Assert.NotNull(code);
        var tokenRes = await _client!.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1:9/callback",
            ["code_verifier"] = verifier,
            ["client_id"] = _lastClientId
        }));
        tokenRes.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private string _lastClientId = string.Empty;

    private async Task<HttpResponseMessage> ExchangeCodeAsync(string code, string verifier, string clientId)
        => await _client!.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = "http://127.0.0.1:9/callback",
            ["code_verifier"] = verifier,
            ["client_id"] = clientId
        }));

    private async Task<string?> TryLoginAsync(
        string user,
        string password,
        string verifier,
        string challenge,
        string? scope = null)
    {
        var reg = await _client!.PostAsync("/oauth/register", new StringContent(
            """{"redirect_uris":["http://127.0.0.1:9/callback"],"token_endpoint_auth_method":"none"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        using var regDoc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        _lastClientId = regDoc.RootElement.GetProperty("client_id").GetString()!;

        var authorizeUrl =
            $"/oauth/authorize?response_type=code&client_id={_lastClientId}&redirect_uri={Uri.EscapeDataString("http://127.0.0.1:9/callback")}&state=st&code_challenge={challenge}&code_challenge_method=S256&resource=http://localhost/mcp";
        if (!string.IsNullOrWhiteSpace(scope))
            authorizeUrl += "&scope=" + Uri.EscapeDataString(scope);
        var authorize = await _client.GetAsync(authorizeUrl);
        var html = await authorize.Content.ReadAsStringAsync();
        var ticket = Regex.Match(html, "name=\"ticket\" value=\"([^\"]+)\"").Groups[1].Value;
        if (string.IsNullOrEmpty(ticket))
            return null;

        var login = await _client.PostAsync("/oauth/authorize/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ticket"] = ticket,
            ["username"] = user,
            ["password"] = password
        }));
        if (login.StatusCode != HttpStatusCode.Redirect)
            return null;
        var location = login.Headers.Location?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(location))
            return null;
        if (!Uri.TryCreate(location, UriKind.Absolute, out var locUri)
            && !Uri.TryCreate(new Uri("http://localhost"), location, out locUri))
            return null;
        return GetQuery(locUri, "code");
    }

    private static string? GetQuery(Uri uri, string key)
    {
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.Ordinal))
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }

    private static (string Verifier, string Challenge) MakePkce()
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        if (verifier.Length < 43)
            verifier = verifier.PadRight(43, 'a');
        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string CreateExpiredJwt()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(FakeBudgetWebApi.JwtSecret));
        var token = new JwtSecurityToken(
            issuer: "BudgetWeb-SNEL",
            audience: "BudgetWeb-Client",
            claims: [new Claim("uid", "7")],
            notBefore: DateTime.UtcNow.AddHours(-5),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
