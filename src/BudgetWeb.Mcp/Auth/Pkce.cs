using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BudgetWeb.Mcp.Auth;

public static class Pkce
{
    public static bool IsValidS256(string codeVerifier, string codeChallenge)
    {
        if (string.IsNullOrWhiteSpace(codeVerifier) || string.IsNullOrWhiteSpace(codeChallenge))
            return false;
        if (codeVerifier.Length is < 43 or > 128)
            return false;

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var computed = Base64UrlEncoder.Encode(hash);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(codeChallenge));
    }
}
