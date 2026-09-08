using BudgetWeb.Infrastructure.Documents;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents.Experiments;

/// <summary>
/// Chrome expérimental — redimensionnement mémoire des assets SNEL (~256 px max).
/// Diagnostic uniquement ; dérivé des bytes production au premier accès.
/// </summary>
internal static class DpmPdfExperimentChrome
{
    private const int MaxPixelDimension = 256;

    private static readonly Lazy<byte[]?> LogoBytes256 = new(() =>
        PdfRasterAssetOptimizer.ResizeForPdfDisplay(SnelInstitutionalPdfChrome.Logo, MaxPixelDimension));

    private static readonly Lazy<byte[]?> WatermarkBytes256 = new(() =>
        PdfRasterAssetOptimizer.ResizeForPdfDisplay(SnelInstitutionalPdfChrome.Watermark, MaxPixelDimension)
        ?? PdfRasterAssetOptimizer.ResizeForPdfDisplay(SnelInstitutionalPdfChrome.Logo, MaxPixelDimension));

    public static byte[]? Logo => LogoBytes256.Value;
    public static byte[]? Watermark => WatermarkBytes256.Value;

    public static void ApplyDiscreteBackgroundWatermark(PageDescriptor page)
    {
        var wm = Watermark;
        if (wm is not { Length: > 0 })
            return;

        page.Background().AlignCenter().AlignMiddle()
            .Width(130).Height(130).Image(wm).FitArea();
    }

}
