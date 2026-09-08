using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BudgetWeb.Mcp.Auth;

public sealed class RegisteredOAuthClient
{
    public required string ClientId { get; init; }
    public required IReadOnlyList<string> RedirectUris { get; init; }
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AuthorizationCodeRecord
{
    public required string Code { get; init; }
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string CodeChallenge { get; init; }
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required string Resource { get; init; }
    public string Scope { get; init; } = OAuthResource.ReadScope;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class PendingAuthorization
{
    public required string Ticket { get; init; }
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string State { get; init; }
    public required string CodeChallenge { get; init; }
    public string? Resource { get; init; }
    public string? Scope { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RefreshTokenRecord
{
    public required string TokenHash { get; init; }
    public required string ClientId { get; init; }
    public required string AccessToken { get; init; }
    public required DateTime JwtExpiresAtUtc { get; init; }
    public required string Resource { get; init; }
    public required string Scope { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Stockage OAuth v1. Implémentation mémoire ; remplaçable plus tard sans changer les endpoints.</summary>
public interface IOAuthAuthorizationStore
{
    PendingAuthorization CreatePending(PendingAuthorization pending);
    PendingAuthorization? TakePending(string ticket);
    AuthorizationCodeRecord StoreCode(AuthorizationCodeRecord record);
    AuthorizationCodeRecord? TakeCode(string code);
    string IssueRefresh(RefreshTokenRecord template);
    RefreshTokenRecord? ConsumeRefresh(string refreshToken);
    void PurgeExpired();
}

public sealed class OAuthClientStore
{
    private readonly ConcurrentDictionary<string, RegisteredOAuthClient> _clients = new(StringComparer.Ordinal);

    public RegisteredOAuthClient Register(IReadOnlyList<string> redirectUris)
    {
        var client = new RegisteredOAuthClient
        {
            ClientId = Guid.NewGuid().ToString("N"),
            RedirectUris = redirectUris,
        };
        _clients[client.ClientId] = client;
        return client;
    }

    public RegisteredOAuthClient? Find(string clientId)
        => _clients.TryGetValue(clientId, out var c) ? c : null;
}

public sealed class OAuthAuthorizationStore : IOAuthAuthorizationStore
{
    internal static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan RefreshTtl = TimeSpan.FromDays(14);

    private readonly ConcurrentDictionary<string, PendingAuthorization> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AuthorizationCodeRecord> _codes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RefreshTokenRecord> _refresh = new(StringComparer.Ordinal);

    public PendingAuthorization CreatePending(PendingAuthorization pending)
    {
        _pending[pending.Ticket] = pending;
        return pending;
    }

    public PendingAuthorization? TakePending(string ticket)
        => _pending.TryRemove(ticket, out var p) ? p : null;

    public AuthorizationCodeRecord StoreCode(AuthorizationCodeRecord record)
    {
        _codes[record.Code] = record;
        return record;
    }

    public AuthorizationCodeRecord? TakeCode(string code)
    {
        if (!_codes.TryRemove(code, out var c))
            return null;
        if (DateTimeOffset.UtcNow - c.CreatedAt > CodeTtl)
            return null;
        return c;
    }

    public string IssueRefresh(RefreshTokenRecord template)
    {
        var plaintext = CreateOpaqueToken();
        var hash = HashToken(plaintext);
        _refresh[hash] = new RefreshTokenRecord
        {
            TokenHash = hash,
            ClientId = template.ClientId,
            AccessToken = template.AccessToken,
            JwtExpiresAtUtc = template.JwtExpiresAtUtc,
            Resource = template.Resource,
            Scope = template.Scope,
            ExpiresAt = template.ExpiresAt == default ? DateTimeOffset.UtcNow.Add(RefreshTtl) : template.ExpiresAt
        };
        return plaintext;
    }

    public RefreshTokenRecord? ConsumeRefresh(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;
        var hash = HashToken(refreshToken);
        if (!_refresh.TryRemove(hash, out var record))
            return null;
        if (record.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;
        return record;
    }

    public void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _pending)
        {
            if (now - kv.Value.CreatedAt > PendingTtl)
                _pending.TryRemove(kv.Key, out _);
        }

        foreach (var kv in _codes)
        {
            if (now - kv.Value.CreatedAt > CodeTtl)
                _codes.TryRemove(kv.Key, out _);
        }

        foreach (var kv in _refresh)
        {
            if (kv.Value.ExpiresAt <= now)
                _refresh.TryRemove(kv.Key, out _);
        }
    }

    internal static string CreateOpaqueToken()
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    internal static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
