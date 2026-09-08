using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace BudgetWeb.Infrastructure.Documents;

internal static class PdfRasterAssetOptimizer
{
    internal static byte[]? ResizeForPdfDisplay(byte[]? source, int maxPixelDimension)
    {
        if (source is not { Length: > 0 })
            return null;

        try
        {
            using var inputStream = new MemoryStream(source);
            using var input = Image.FromStream(inputStream);
            if (input.Width <= maxPixelDimension && input.Height <= maxPixelDimension)
                return source;

            var scale = Math.Min(
                (double)maxPixelDimension / input.Width,
                (double)maxPixelDimension / input.Height);
            var targetW = Math.Max(1, (int)Math.Round(input.Width * scale));
            var targetH = Math.Max(1, (int)Math.Round(input.Height * scale));

            using var bitmap = new Bitmap(targetW, targetH);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(input, 0, 0, targetW, targetH);
            }

            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            return output.ToArray();
        }
        catch
        {
            return source;
        }
    }
}