using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace BudgetWeb.Infrastructure.Storage;

/// <summary>
/// Stockage local des pièces justificatives.
/// Racine lue depuis la configuration — jamais codée en dur dans le métier.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<PieceJointeStorageOptions> options, IConfiguration configuration)
    {
        var configured = options.Value.RootPath
            ?? configuration["Storage:PiecesJustificatives:RootPath"]
            ?? "App_Data/pieces-justificatives";

        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));

        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveSafeAbsolutePath(relativePath);
        var dir = Path.GetDirectoryName(absolute);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var fs = new FileStream(
            absolute,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await content.CopyToAsync(fs, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveSafeAbsolutePath(relativePath);
        if (!File.Exists(absolute))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(
            absolute,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveSafeAbsolutePath(relativePath);
        if (File.Exists(absolute))
            File.Delete(absolute);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveSafeAbsolutePath(relativePath);
        return Task.FromResult(File.Exists(absolute));
    }

    private string ResolveSafeAbsolutePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Le chemin relatif de stockage est obligatoire.");

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(normalized)
            || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Chemin de stockage relatif invalide.");
        }

        var absolute = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootFull = Path.GetFullPath(_root);
        if (!absolute.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chemin de stockage hors racine autorisée.");

        return absolute;
    }
}
