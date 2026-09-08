namespace BudgetWeb.Infrastructure.Documents.Experiments;

/// <summary>
/// Variantes expérimentales PDF DPM — diagnostic perf uniquement, hors production.
/// Réversible : ne pas utiliser en dehors des tests de mesure.
/// </summary>
public enum DpmPdfExperimentVariant
{
    Baseline,
    OptimizedImages,
    SimplifiedPagination,
    OptimizedImagesAndPagination,
}

[Flags]
internal enum DpmPdfExperimentFlags
{
    None = 0,
    OptimizedImages = 1,
    SimplifiedPagination = 2,
}

internal static class DpmPdfExperimentVariantExtensions
{
    internal static DpmPdfExperimentFlags ToFlags(this DpmPdfExperimentVariant variant)
        => variant switch
        {
            DpmPdfExperimentVariant.OptimizedImages => DpmPdfExperimentFlags.OptimizedImages,
            DpmPdfExperimentVariant.SimplifiedPagination => DpmPdfExperimentFlags.SimplifiedPagination,
            DpmPdfExperimentVariant.OptimizedImagesAndPagination
                => DpmPdfExperimentFlags.OptimizedImages | DpmPdfExperimentFlags.SimplifiedPagination,
            _ => DpmPdfExperimentFlags.None,
        };
}
