using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BudgetWeb.Mcp.Client;
using BudgetWeb.Mcp.Options;
using Microsoft.Extensions.Options;

namespace BudgetWeb.Mcp.Auth;

public static class OAuthEndpoints
{
    public const string Scope = "budgetweb.read";

    public static void MapOAuth(this WebApplication app)
    {
        app.MapGet("/.well-known/oauth-authorization-server", Metadata).AllowAnonymous();
        app.MapGet("/.well-known/oauth-authorization-server/mcp", Metadata).AllowAnonymous();
        app.MapGet("/.well-known/oauth-protected-resource", ProtectedResource).AllowAnonymous();
        app.MapGet("/.well-known/oauth-protected-resource/mcp", ProtectedResource).AllowAnonymous();

        app.MapPost("/oauth/register", Register).AllowAnonymous();
        app.MapGet("/oauth/authorize", Authorize).AllowAnonymous();
        app.MapPost("/oauth/authorize/login", Login).AllowAnonymous().DisableAntiforgery();
        app.MapPost("/oauth/token", Token).AllowAnonymous().DisableAntiforgery();
    }

    private static IResult Metadata(HttpContext http, IOptions<McpOptions> options, IHostEnvironment env)
    {
        var issuer = PublicBase(http, options.Value, env);
        return Results.Json(new
        {
            issuer,
            authorization_endpoint = issuer + "/oauth/authorize",
            token_endpoint = issuer + "/oauth/token",
            registration_endpoint = issuer + "/oauth/register",
            token_endpoint_auth_methods_supported = new[] { "none" },
            grant_types_supported = new[] { "authorization_code" },
            response_types_supported = new[] { "code" },
            code_challenge_methods_supported = new[] { "S256" },
            scopes_supported = new[] { Scope },
            authorization_response_iss_parameter_supported = true
        });
    }

    private static IResult ProtectedResource(HttpContext http, IOptions<McpOptions> options, IHostEnvironment env)
    {
        var issuer = PublicBase(http, options.Value, env);
        return Results.Json(new
        {
            resource = issuer + "/mcp",
            authorization_servers = new[] { issuer },
            bearer_methods_supported = new[] { "header" },
            scopes_supported = new[] { Scope },
            resource_documentation = issuer + "/docs"
        });
    }

    private static async Task<IResult> Register(
        HttpContext http,
        OAuthClientStore clients,
        IOptions<McpOptions> options,
        IHostEnvironment env)
    {
        using var doc = await JsonDocument.ParseAsync(http.Request.Body);
        var redirectUris = new List<string>();
        var allowLoopback = env.IsDevelopment();
        if (doc.RootElement.TryGetProperty("redirect_uris", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var uri = item.GetString();
                if (!RedirectUriPolicy.IsAllowed(uri, options.Value.AdditionalRedirectUris, allowLoopback))
                    return Results.Json(new { error = "invalid_redirect_uri" }, statusCode: 400);
                if (uri is not null)
                    redirectUris.Add(uri);
            }
        }

        if (redirectUris.Count == 0)
            return Results.Json(new { error = "invalid_redirect_uri" }, statusCode: 400);

        var client = clients.Register(redirectUris);
        http.Response.StatusCode = StatusCodes.Status201Created;
        return Results.Json(new
        {
            client_id = client.ClientId,
            client_id_issued_at = client.IssuedAt.ToUnixTimeSeconds(),
            redirect_uris = client.RedirectUris,
            token_endpoint_auth_method = "none",
            grant_types = new[] { "authorization_code" },
            response_types = new[] { "code" }
        });
    }

    private static IResult Authorize(
        HttpContext http,
        OAuthClientStore clients,
        OAuthAuthorizationStore store,
        IOptions<McpOptions> options,
        IHostEnvironment env)
    {
        store.PurgeExpired();
        var q = http.Request.Query;
        var clientId = q["client_id"].ToString();
        var redirectUri = q["redirect_uri"].ToString();
        var state = q["state"].ToString();
        var challenge = q["code_challenge"].ToString();
        var method = q["code_challenge_method"].ToString();
        var resource = q["resource"].ToString();
        var scope = q["scope"].ToString();
        var responseType = q["response_type"].ToString();

        if (!string.Equals(responseType, "code", StringComparison.Ordinal)
            || !string.Equals(method, "S256", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(challenge)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(redirectUri))
        {
            return Results.Text("Requête OAuth invalide (authorization_code + PKCE S256 requis).", statusCode: 400);
        }

        if (!RedirectUriPolicy.IsAllowed(redirectUri, options.Value.AdditionalRedirectUris, env.IsDevelopment()))
            return Results.Text("redirect_uri non autorisée.", statusCode: 400);

        var registered = clients.Find(clientId);
        if (registered is null)
            return Results.Text("client_id inconnu. Enregistrez le client via POST /oauth/register (DCR).", statusCode: 400);

        if (registered.RedirectUris.Count > 0
            && !registered.RedirectUris.Contains(redirectUri, StringComparer.OrdinalIgnoreCase))
            return Results.Text("redirect_uri ne correspond pas au client.", statusCode: 400);

        var ticket = Guid.NewGuid().ToString("N");
        store.CreatePending(new PendingAuthorization
        {
            Ticket = ticket,
            ClientId = clientId,
            RedirectUri = redirectUri,
            State = state,
            CodeChallenge = challenge,
            Resource = string.IsNullOrWhiteSpace(resource) ? null : resource,
            Scope = string.IsNullOrWhiteSpace(scope) ? Scope : scope
        });

        return Results.Content(OAuthLoginPage.Render(ticket), "text/html; charset=utf-8");
    }

    private static async Task<IResult> Login(
        HttpContext http,
        OAuthAuthorizationStore store,
        IHttpClientFactory httpFactory,
        IOptions<McpOptions> options,
        IHostEnvironment env)
    {
        store.PurgeExpired();
        var form = await http.Request.ReadFormAsync();
        var ticket = form["ticket"].ToString();
        var username = form["username"].ToString();
        var password = form["password"].ToString();
        var pending = store.TakePending(ticket);
        if (pending is null)
            return Results.Content(OAuthLoginPage.Render(ticket, "Session de connexion expirée. Relancez depuis ChatGPT."), "text/html; charset=utf-8");

        var loginClient = httpFactory.CreateClient("BudgetWebLogin");
        using var response = await loginClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { nomUtilisateur = username, motDePasse = password });
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            store.CreatePending(pending);
            var message = MapLoginError(response.StatusCode, body);
            return Results.Content(OAuthLoginPage.Render(pending.Ticket, message), "text/html; charset=utf-8");
        }

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.GetProperty("accessToken").GetString()
            ?? json.RootElement.GetProperty("AccessToken").GetString();
        if (string.IsNullOrWhiteSpace(token))
        {
            store.CreatePending(pending);
            return Results.Content(OAuthLoginPage.Render(pending.Ticket, "Réponse d’authentification inattendue."), "text/html; charset=utf-8");
        }

        var expires = DateTime.UtcNow.AddHours(8);
        if (json.RootElement.TryGetProperty("expiresAtUtc", out var expEl)
            && expEl.TryGetDateTime(out var exp))
            expires = DateTime.SpecifyKind(exp, DateTimeKind.Utc);

        var code = Guid.NewGuid().ToString("N");
        store.StoreCode(new AuthorizationCodeRecord
        {
            Code = code,
            ClientId = pending.ClientId,
            RedirectUri = pending.RedirectUri,
            CodeChallenge = pending.CodeChallenge,
            AccessToken = token,
            ExpiresAtUtc = expires,
            Scope = pending.Scope ?? Scope
        });

        var issuer = PublicBase(http, options.Value, env);
        var dest = pending.RedirectUri
                   + (pending.RedirectUri.Contains('?', StringComparison.Ordinal) ? "&" : "?")
                   + "code=" + Uri.EscapeDataString(code)
                   + "&state=" + Uri.EscapeDataString(pending.State ?? string.Empty)
                   + "&iss=" + Uri.EscapeDataString(issuer);
        return Results.Redirect(dest);
    }

    private static async Task<IResult> Token(
        HttpContext http,
        OAuthAuthorizationStore store)
    {
        store.PurgeExpired();
        var form = await http.Request.ReadFormAsync();
        var grant = form["grant_type"].ToString();
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var verifier = form["code_verifier"].ToString();
        var clientId = form["client_id"].ToString();

        if (!string.Equals(grant, "authorization_code", StringComparison.Ordinal))
            return Results.Json(new { error = "unsupported_grant_type" }, statusCode: 400);

        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(redirectUri)
            || string.IsNullOrWhiteSpace(verifier))
            return Results.Json(new { error = "invalid_grant" }, statusCode: 400);

        var record = store.TakeCode(code);
        if (record is null)
            return Results.Json(new { error = "invalid_grant" }, statusCode: 400);

        if (!string.Equals(record.RedirectUri, redirectUri, StringComparison.Ordinal)
            || !string.Equals(record.ClientId, clientId, StringComparison.Ordinal))
            return Results.Json(new { error = "invalid_grant" }, statusCode: 400);

        if (!Pkce.IsValidS256(verifier, record.CodeChallenge))
            return Results.Json(new { error = "invalid_grant" }, statusCode: 400);

        var expiresIn = Math.Max(60, (int)(record.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds);
        return Results.Json(new
        {
            access_token = record.AccessToken,
            token_type = "Bearer",
            expires_in = expiresIn,
            scope = record.Scope
        });
    }

    public static string PublicBase(HttpContext http, McpOptions options, IHostEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            return options.PublicBaseUrl.TrimEnd('/');

        if (env.IsProduction())
        {
            throw new InvalidOperationException(
                "Mcp:PublicBaseUrl est obligatoire en Production et ne peut pas être déduit de Host ou des Forwarded Headers.");
        }

        var req = http.Request;
        return $"{req.Scheme}://{req.Host.Value}".TrimEnd('/');
    }

    private static string MapLoginError(HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m) || doc.RootElement.TryGetProperty("Message", out m))
            {
                var text = m.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return WebUtility.HtmlEncode(ApiErrorMapper.Sanitize(text));
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return status == HttpStatusCode.Unauthorized
            ? "Identifiants invalides ou compte inactif."
            : "Connexion impossible. Réessayez.";
    }
}
