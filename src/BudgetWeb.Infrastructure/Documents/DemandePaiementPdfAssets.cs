using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

internal static class DemandePaiementPdfAssets
{
    private const int MaxPixelDimension = 256;

    private static readonly Lazy<byte[]?> LogoBytes = new(() =>
        PdfRasterAssetOptimizer.ResizeForPdfDisplay(
            SnelInstitutionalPdfChrome.Logo,
            MaxPixelDimension));

    private static readonly Lazy<byte[]?> WatermarkBytes = new(() =>
        PdfRasterAssetOptimizer.ResizeForPdfDisplay(
            SnelInstitutionalPdfChrome.Watermark,
            MaxPixelDimension)
        ?? PdfRasterAssetOptimizer.ResizeForPdfDisplay(
            SnelInstitutionalPdfChrome.Logo,
            MaxPixelDimension));

    public static byte[]? Logo => LogoBytes.Value;

    public static byte[]? Watermark => WatermarkBytes.Value;

    public static void Preload()
    {
        _ = Logo;
        _ = Watermark;
    }

    public static void ApplyDiscreteBackgroundWatermark(PageDescriptor page)
    {
        var wm = Watermark;
        if (wm is not { Length: > 0 })
            return;

        page.Background().AlignCenter().AlignMiddle()
            .Width(130).Height(130).Image(wm).FitArea();
    }
}