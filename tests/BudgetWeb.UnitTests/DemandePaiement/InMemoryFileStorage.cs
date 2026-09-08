using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Stockage fichier en mémoire pour les tests unitaires DPM.</summary>
internal sealed class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _files[Normalize(relativePath)] = ms.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (!_files.TryGetValue(Normalize(relativePath), out var bytes))
            return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(new MemoryStream(bytes, writable: false));
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        _files.Remove(Normalize(relativePath));
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
        => Task.FromResult(_files.ContainsKey(Normalize(relativePath)));

    private static string Normalize(string relativePath)
        => relativePath.Replace('\\', '/').TrimStart('/');
}
