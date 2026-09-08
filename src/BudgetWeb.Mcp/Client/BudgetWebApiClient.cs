using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BudgetWeb.Mcp.Security;

namespace BudgetWeb.Mcp.Client;

public sealed class BudgetWebApiClient
{
    public const int MaxRows = 50;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContext;

    public BudgetWebApiClient(HttpClient http, IHttpContextAccessor httpContext)
    {
        _http = http;
        _httpContext = httpContext;
    }

    public bool HasBearer =>
        !string.IsNullOrWhiteSpace(_httpContext.HttpContext?.Request.Headers.Authorization.ToString());

    public string RequireAuthMessage() => "Utilisateur non authentifié. Connectez votre compte Budget Web via OAuth.";

    public async Task<string> GetJsonAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        if (!HasBearer)
            return RequireAuthMessage();

        using var response = await _http.GetAsync(relativeUrl, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ApiErrorMapper.ToUserMessage(response.StatusCode, body);
        return Truncate(body);
    }

    public async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return default;
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    public async Task<(bool Ok, string Body)> PostJsonAsync(string relativeUrl, object payload, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(relativeUrl, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, ApiErrorMapper.ToUserMessage(response.StatusCode, body));
        return (true, Truncate(body));
    }

    public async Task JournaliserAsync(string outil, object? parametres, int? nombre, CancellationToken cancellationToken)
    {
        if (!HasBearer)
            return;
        try
        {
            var safe = ParameterGuard.ToAuditPayload(parametres);
            await _http.PostAsJsonAsync(
                "/api/v1/mcp/journal",
                new { outil, parametres = safe, nombreResultats = nombre },
                cancellationToken);
        }
        catch
        {
            // L'audit ne doit jamais masquer le résultat métier.
        }
    }

    public static string Query(params (string Key, object? Value)[] pairs)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in pairs)
        {
            if (value is null) continue;
            var text = value.ToString();
            if (string.IsNullOrWhiteSpace(text)) continue;
            sb.Append(sb.Length == 0 ? '?' : '&');
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(text));
        }
        return sb.ToString();
    }

    public static string Truncate(string json, int maxChars = 24_000)
    {
        if (json.Length <= maxChars)
            return json;
        return json[..maxChars] + "\n… (réponse tronquée — affinez les filtres ou la pagination)";
    }
}
