namespace BudgetWeb.Application.Interfaces;

/// <summary>
/// Abstraction de stockage de fichiers (indépendante du disque / cloud / partage).
/// Les chemins passés sont des clés relatives — jamais de chemin physique absolu.
/// </summary>
public interface IFileStorage
{
    /// <summary>Écrit le contenu sous la clé relative indiquée (écrase si existe).</summary>
    Task SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Ouvre un flux en lecture, ou null si absent.</summary>
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Supprime le fichier s'il existe (no-op sinon).</summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Indique si la clé relative existe.</summary>
    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);
}
