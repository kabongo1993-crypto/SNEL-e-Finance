using BudgetWeb.Application.Options;
using Microsoft.Extensions.Options;

namespace BudgetWeb.Application.Services;

/// <summary>Validation des uploads de pièces (extension, taille, nom).</summary>
public sealed class PieceJointeUploadValidator
{
    private readonly PieceJointeStorageOptions _options;

    public PieceJointeUploadValidator(IOptions<PieceJointeStorageOptions> options)
    {
        _options = options.Value;
    }

    public long MaxFileSizeBytes => _options.MaxFileSizeBytes;

    public string SanitizeOriginalFileName(string? originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Le nom du fichier est obligatoire.");

        var name = Path.GetFileName(originalFileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Le nom du fichier est invalide.");

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Le nom du fichier contient des caractères invalides.");

        return name;
    }

    public string GetValidatedExtension(string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
            throw new ArgumentException("Le fichier doit avoir une extension.");

        var allowed = _options.AllowedExtensions
            .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!allowed.Contains(ext))
        {
            throw new ArgumentException(
                $"Extension non autorisée ({ext}). Autorisées : {string.Join(", ", allowed.OrderBy(x => x))}.");
        }

        return ext;
    }

    public void EnsureSizeWithinLimit(long sizeBytes)
    {
        if (sizeBytes < 0)
            throw new ArgumentException("La taille du fichier est invalide.");
        if (sizeBytes == 0)
            throw new ArgumentException("Le fichier est vide.");
        if (sizeBytes > _options.MaxFileSizeBytes)
        {
            var maxMo = _options.MaxFileSizeBytes / (1024d * 1024d);
            throw new ArgumentException($"Le fichier dépasse la taille maximale autorisée ({maxMo:0.##} Mo).");
        }
    }

    /// <summary>Clé relative de stockage : {{idDemande}}/{{guid}}{{ext}}.</summary>
    public string BuildRelativeStorageKey(long idDemande, string extension)
        => $"{idDemande}/{Guid.NewGuid():N}{extension}";
}
