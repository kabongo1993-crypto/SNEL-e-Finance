using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Infrastructure.Documents;

var renderer = new QuestPdfBilletConversionRenderer();
var dto = new BilletConversionDocumentDto(
    34, "DP-2026-00030", "JOSEPH MUSUNGAYI", 30_000m, "EUR", 3_450m,
    new DateOnly(2026, 8, 28), null, null, 103_500_000m, null,
    "Charge DPM Test", null, null, new DateTime(2026, 8, 28));
var pdf = renderer.Render(dto);

var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "test-output"));
Directory.CreateDirectory(dir);
var path = Path.Combine(dir, "billet-conversion-v2-sample.pdf");
await File.WriteAllBytesAsync(path, pdf);

var raw = Encoding.Latin1.GetString(pdf);
Console.WriteLine($"Bytes: {pdf.Length}");
Console.WriteLine($"Saved: {path}");

foreach (Match m in Regex.Matches(raw, @"/Count\s+(\d+)"))
    Console.WriteLine($"Count: {m.Groups[1].Value} at {m.Index}");

foreach (Match m in Regex.Matches(raw, @"/Type\s*/Page\b"))
    Console.WriteLine($"Page obj at {m.Index}");

var media = Regex.Match(raw, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
if (media.Success)
{
    const float ptPerMm = 72f / 25.4f;
    Console.WriteLine($"MediaBox pt: {media.Groups[1].Value} x {media.Groups[2].Value}");
    Console.WriteLine($"MediaBox mm: {float.Parse(media.Groups[1].Value, CultureInfo.InvariantCulture) / ptPerMm:F1} x {float.Parse(media.Groups[2].Value, CultureInfo.InvariantCulture) / ptPerMm:F1}");
}
