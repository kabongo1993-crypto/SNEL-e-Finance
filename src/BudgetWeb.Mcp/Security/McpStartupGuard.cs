using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BudgetWeb.Mcp.Security;

public static class McpStartupGuard
{
    public const string KnownDevelopmentJwtSecret =
        "DEV-ONLY-BudgetWeb-SNEL-JwtSigningKey-ChangeInProd-64chars!!";

    public static void Validate(IHostEnvironment environment, IConfiguration configuration)
    {
        var secret = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey doit être configuré (minimum 32 caractères), identique à BudgetWeb.API.");
        }

        if (!environment.IsProduction())
            return;

        if (IsDevelopmentSecret(secret))
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey de développement interdit en Production. " +
                "Définissez Jwt__SecretKey (secret HMAC identique à BudgetWeb.API) via l'environnement.");
        }

        var publicBase = configuration["Mcp:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(publicBase))
        {
            throw new InvalidOperationException(
                "Mcp:PublicBaseUrl est obligatoire en Production (ex. https://mcp.mon-domaine.com). " +
                "L'URL publique ne peut pas être déduite de Host ou des Forwarded Headers.");
        }

        if (!Uri.TryCreate(publicBase.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Mcp:PublicBaseUrl doit être une URL HTTPS absolue en Production (ex. https://mcp.mon-domaine.com).");
        }
    }

    public static bool IsDevelopmentSecret(string secret)
    {
        var t = secret.Trim();
        if (string.Equals(t, KnownDevelopmentJwtSecret, StringComparison.Ordinal))
            return true;
        if (t.Contains("DEV-ONLY", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.Contains("ChangeInProd", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
