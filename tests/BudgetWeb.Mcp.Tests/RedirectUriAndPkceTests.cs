using BudgetWeb.Mcp.Auth;
using Xunit;

namespace BudgetWeb.Mcp.Tests;

public class RedirectUriPolicyTests
{
    [Theory]
    [InlineData("https://chatgpt.com/connector_platform_oauth_redirect")]
    [InlineData("https://chatgpt.com/connector/oauth/abc123")]
    public void AccepteUrisChatGpt(string uri)
        => Assert.True(RedirectUriPolicy.IsAllowed(uri, []));

    [Theory]
    [InlineData("http://127.0.0.1:9/callback")]
    [InlineData("http://localhost:5000/callback")]
    public void AccepteLoopbackEnDevelopment(string uri)
        => Assert.True(RedirectUriPolicy.IsAllowed(uri, [], allowLoopback: true));

    [Theory]
    [InlineData("http://127.0.0.1:9/callback")]
    [InlineData("http://localhost:5000/callback")]
    public void RefuseLoopbackHorsDevelopment(string uri)
        => Assert.False(RedirectUriPolicy.IsAllowed(uri, [], allowLoopback: false));

    [Theory]
    [InlineData("https://evil.example/callback")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-uri")]
    public void RefuseUrisInconnues(string uri)
        => Assert.False(RedirectUriPolicy.IsAllowed(uri, []));

    [Fact]
    public void AccepteUriSupplementaireConfiguree()
        => Assert.True(RedirectUriPolicy.IsAllowed(
            "https://app.snel.cd/oauth/callback",
            ["https://app.snel.cd/oauth/callback"]));
}

public class OAuthResourceTests
{
    [Fact]
    public void AccepteResourceCanoniqueEtOrigine()
    {
        const string pub = "https://mcp-snel.christresfort.app";
        Assert.True(OAuthResource.IsAllowed(pub + "/mcp", pub));
        Assert.True(OAuthResource.IsAllowed(pub, pub));
        Assert.False(OAuthResource.IsAllowed("https://evil.example/mcp", pub));
    }

    [Fact]
    public void NormalizeScope_OfflineAccessSansPouvoirMetier()
    {
        Assert.Equal("budgetweb.read", OAuthResource.NormalizeScope(null));
        Assert.Equal("budgetweb.read", OAuthResource.NormalizeScope("budgetweb.read"));
        Assert.Equal("budgetweb.read offline_access", OAuthResource.NormalizeScope("offline_access budgetweb.read"));
        Assert.False(OAuthResource.WantsOfflineAccess("budgetweb.read"));
        Assert.True(OAuthResource.WantsOfflineAccess("budgetweb.read offline_access"));
    }
}

public class OAuthAuthorizationStoreTests
{
    [Fact]
    public void Refresh_HasheEtUsageUnique()
    {
        var store = new OAuthAuthorizationStore();
        var plaintext = store.IssueRefresh(new RefreshTokenRecord
        {
            TokenHash = string.Empty,
            ClientId = "c1",
            AccessToken = "jwt",
            JwtExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            Resource = "http://localhost/mcp",
            Scope = "budgetweb.read offline_access"
        });
        Assert.DoesNotContain('.', plaintext);
        var first = store.ConsumeRefresh(plaintext);
        Assert.NotNull(first);
        Assert.Null(store.ConsumeRefresh(plaintext));
    }
}

public class PkceTests
{
    [Fact]
    public void ValideS256()
    {
        var verifier = new string('a', 43);
        var challenge = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        Assert.True(Pkce.IsValidS256(verifier, challenge));
        Assert.False(Pkce.IsValidS256(verifier + "x", challenge));
        Assert.False(Pkce.IsValidS256("short", challenge));
    }
}
