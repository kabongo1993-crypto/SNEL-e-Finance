namespace BudgetWeb.Application.Options;

/// <summary>Options de stockage des pièces justificatives DPM (section Storage:PiecesJustificatives).</summary>
public sealed class PieceJointeStorageOptions
{
    public const string SectionName = "Storage:PiecesJustificatives";

    /// <summary>Racine locale (relative à BaseDirectory ou chemin absolu). Jamais utilisée hors Infrastructure.</summary>
    public string RootPath { get; set; } = "App_Data/pieces-justificatives";

    /// <summary>Taille max d'un fichier (octets).</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Extensions autorisées (avec point, minuscules).</summary>
    public string[] AllowedExtensions { get; set; } =
    [
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".tif",
        ".tiff",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
    ];
}
