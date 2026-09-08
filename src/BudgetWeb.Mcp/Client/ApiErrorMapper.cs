using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BudgetWeb.Mcp.Client;

public static class ApiErrorMapper
{
    private static readonly Regex JwtLike = new(
        @"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string ToUserMessage(HttpStatusCode status, string? body)
    {
        var message = TryReadMessage(body);
        return status switch
        {
            HttpStatusCode.Unauthorized => string.IsNullOrWhiteSpace(message)
                ? "Utilisateur non authentifié ou session expirée."
                : Sanitize(message),
            HttpStatusCode.Forbidden => "Permission insuffisante pour cette opération.",
            HttpStatusCode.NotFound => string.IsNullOrWhiteSpace(message)
                ? "Donnée introuvable."
                : Sanitize(message),
            HttpStatusCode.BadRequest => string.IsNullOrWhiteSpace(message)
                ? "Paramètre invalide."
                : Sanitize(message),
            HttpStatusCode.Conflict => Sanitize(message ?? "Opération refusée par les règles métier."),
            _ => "L’API Budget Web a refusé la demande. Vérifiez vos droits et les filtres.",
        };
    }

    public static string Sanitize(string message)
    {
        var t = message.Trim();
        if (LooksTechnical(t))
            return "Erreur interne Budget Web.";
        if (t.Length > 400)
            t = t[..400];
        return t;
    }

    private static bool LooksTechnical(string t)
    {
        if (t.Contains("at ", StringComparison.Ordinal) && t.Contains(".cs:", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("connection string", StringComparison.OrdinalIgnoreCase)
            || t.Contains("SqlException", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            || t.Contains("SELECT ", StringComparison.OrdinalIgnoreCase)
            || t.Contains("INSERT ", StringComparison.OrdinalIgnoreCase)
            || t.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase)
            || t.Contains("DELETE ", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("Jwt:SecretKey", StringComparison.OrdinalIgnoreCase)
            || t.Contains("SecretKey", StringComparison.OrdinalIgnoreCase)
            || t.Contains("motDePasse", StringComparison.OrdinalIgnoreCase)
            || t.Contains("password", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Authorization", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Bearer ", StringComparison.OrdinalIgnoreCase))
            return true;
        if (JwtLike.IsMatch(t))
            return true;
        return false;
    }

    private static string? TryReadMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (TryString(root, "message", out var m) || TryString(root, "Message", out m))
                return m;
            if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                return d.GetString();
            if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString();
        }
        catch (JsonException)
        {
            // Corps non JSON : ne pas le renvoyer brut.
        }

        return null;
    }

    private static bool TryString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
            return false;
        value = prop.GetString();
        return true;
    }
}
