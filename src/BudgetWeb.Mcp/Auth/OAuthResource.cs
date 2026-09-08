namespace BudgetWeb.Mcp.Auth;

public static class OAuthResource
{
    public const string ReadScope = "budgetweb.read";
    public const string OfflineAccess = "offline_access";

    public static string McpResource(string publicBase)
        => publicBase.TrimEnd('/') + "/mcp";

    public static bool IsAllowed(string? resource, string publicBase)
    {
        if (string.IsNullOrWhiteSpace(resource))
            return false;

        var value = resource.Trim().TrimEnd('/');
        var mcp = McpResource(publicBase).TrimEnd('/');
        var origin = publicBase.TrimEnd('/');
        return string.Equals(value, mcp, StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, origin, StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeScope(string? raw)
    {
        var parts = (raw ?? ReadScope).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var offline = parts.Contains(OfflineAccess, StringComparer.Ordinal);
        return offline ? $"{ReadScope} {OfflineAccess}" : ReadScope;
    }

    public static bool WantsOfflineAccess(string scope)
        => scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(OfflineAccess, StringComparer.Ordinal);
}
