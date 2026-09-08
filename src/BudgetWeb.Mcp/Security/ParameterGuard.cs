using System.Text.Json;
using System.Text.RegularExpressions;

namespace BudgetWeb.Mcp.Security;

public static class ParameterGuard
{
    public const int MaxPageSize = 50;
    public const int MaxSearchLength = 200;
    private static readonly Regex SqlLike = new(
        @"\b(SELECT|INSERT|UPDATE|DELETE|DROP|ALTER|EXEC|UNION|MERGE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static int ClampTake(int? take)
    {
        var n = take ?? 20;
        if (n < 1) n = 1;
        if (n > MaxPageSize) n = MaxPageSize;
        return n;
    }

    public static int ClampSkip(int? skip)
    {
        var n = skip ?? 0;
        return n < 0 ? 0 : n;
    }

    public static string? SanitizeSearch(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        if (s.Length == 0) return null;
        if (s.Length > MaxSearchLength)
            s = s[..MaxSearchLength];
        if (SqlLike.IsMatch(s))
            throw new ArgumentException("Le filtre texte ne peut pas contenir d’instructions SQL.");
        return s;
    }

    public static long RequirePositive(long? id, string name)
    {
        if (id is null or <= 0)
            throw new ArgumentException($"Le paramètre {name} est obligatoire et doit être un identifiant positif.");
        return id.Value;
    }

    public static string ToAuditPayload(object? parametres)
    {
        if (parametres is null) return "{}";
        try
        {
            return JsonSerializer.Serialize(parametres);
        }
        catch
        {
            return "{}";
        }
    }
}
