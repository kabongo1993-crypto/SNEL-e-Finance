using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace BudgetWeb.Mcp.Options;

public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    /// <summary>URL interne de BudgetWeb.API (ex. http://localhost:5257).</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5257";

    /// <summary>URL publique du MCP. Obligatoire et HTTPS en Production.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public int ApiTimeoutSeconds { get; set; } = 30;

    /// <summary>URI de redirection OAuth autorisées (en plus des URI ChatGPT connues).</summary>
    public string[] AdditionalRedirectUris { get; set; } = [];

    /// <summary>Adresses IP des reverse-proxies de confiance (Forwarded Headers).</summary>
    public string[] TrustedProxies { get; set; } = [];

    /// <summary>Réseaux CIDR des reverse-proxies de confiance (ex. 10.0.0.0/8).</summary>
    public string[] TrustedNetworks { get; set; } = [];

    public static void ApplyTrustedForwarders(ForwardedHeadersOptions options, McpOptions mcp)
    {
        foreach (var raw in mcp.TrustedProxies)
        {
            if (IPAddress.TryParse(raw.Trim(), out var ip))
                options.KnownProxies.Add(ip);
        }

        foreach (var raw in mcp.TrustedNetworks)
        {
            if (TryParseCidr(raw.Trim(), out var network))
                options.KnownNetworks.Add(network);
        }
    }

    private static bool TryParseCidr(string cidr, out Microsoft.AspNetCore.HttpOverrides.IPNetwork network)
    {
        network = null!;
        var parts = cidr.Split('/', 2);
        if (parts.Length != 2)
            return false;
        if (!IPAddress.TryParse(parts[0], out var prefix))
            return false;
        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0)
            return false;
        try
        {
            network = new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
