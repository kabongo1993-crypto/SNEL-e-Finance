namespace BudgetWeb.Mcp.Auth;

public static class RedirectUriPolicy
{
    public static readonly string[] ChatGptExact =
    [
        "https://chatgpt.com/connector_platform_oauth_redirect"
    ];

    public static readonly string[] ChatGptPrefixes =
    [
        "https://chatgpt.com/connector/oauth/"
    ];

    public static bool IsAllowed(string? redirectUri, IEnumerable<string>? additional, bool allowLoopback = false)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
            return false;

        var value = uri.GetLeftPart(UriPartial.Query).TrimEnd('/');
        var full = uri.AbsoluteUri;

        foreach (var exact in ChatGptExact)
        {
            if (string.Equals(full, exact, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, exact.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var prefix in ChatGptPrefixes)
        {
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (additional is not null)
        {
            foreach (var extra in additional)
            {
                if (string.IsNullOrWhiteSpace(extra)) continue;
                if (string.Equals(full, extra.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return allowLoopback && IsLoopback(uri);
    }

    public static bool IsLoopback(Uri uri)
        => uri.IsLoopback
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
