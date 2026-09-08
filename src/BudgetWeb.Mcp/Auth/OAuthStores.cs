using System.Collections.Concurrent;

namespace BudgetWeb.Mcp.Auth;

public sealed class RegisteredOAuthClient
{
    public required string ClientId { get; init; }
    public required IReadOnlyList<string> RedirectUris { get; init; }
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
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

public sealed class AuthorizationCodeRecord
{
    public required string Code { get; init; }
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string CodeChallenge { get; init; }
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public string Scope { get; init; } = "budgetweb.read";
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

/// <summary>
/// Store OAuth v1 en mémoire. Compatible uniquement avec une instance MCP unique.
/// Redémarrage = clients DCR, tickets et codes (JWT temporaire) perdus.
/// </summary>
public sealed class OAuthAuthorizationStore
{
    internal static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, PendingAuthorization> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AuthorizationCodeRecord> _codes = new(StringComparer.Ordinal);

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
        => _codes.TryRemove(code, out var c) ? c : null;

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
    }
}
