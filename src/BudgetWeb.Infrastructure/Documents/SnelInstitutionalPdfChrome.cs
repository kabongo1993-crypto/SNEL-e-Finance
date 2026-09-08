using System.Globalization;
using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

public static class SnelUsdFormat
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string Number(decimal n) => string.Format(Fr, "{0:N2}", n);

    public static string WithCurrency(decimal n) => Number(n) + " USD";

    public static string Cell(decimal? n, bool dashIfNull = true)
    {
        if (n is null) return dashIfNull ? "—" : "";
        if (n == 0 && dashIfNull) return "—";
        return Number(n.Value);
    }
}

/// <summary>Chrome institutionnel SNEL réutilisable (DC / BI / AE).</summary>
public static class SnelInstitutionalPdfChrome
{
    private static readonly Lazy<byte[]?> LogoBytes = new(() => LoadAsset("snel-logo.png"));
    private static readonly Lazy<byte[]?> WatermarkBytes = new(() =>
        LoadAsset("snel-logo-watermark.png") ?? LoadAsset("snel-logo.png"));

    public static byte[]? Logo => LogoBytes.Value;
    public static byte[]? Watermark => WatermarkBytes.Value;

    public static void ApplyBackgroundWatermark(PageDescriptor page)
    {
        ApplyBackgroundWatermark(page, size: 300);
    }

    /// <summary>Filigrane très discret (documents de circulation DPM).</summary>
    public static void ApplyDiscreteBackgroundWatermark(PageDescriptor page)
    {
        ApplyBackgroundWatermark(page, size: 130);
    }

    private static void ApplyBackgroundWatermark(PageDescriptor page, float size)
    {
        var wm = Watermark;
        if (wm is { Length: > 0 })
        {
            page.Background().AlignCenter().AlignMiddle()
                .Width(size).Height(size).Image(wm).FitArea();
        }
    }

    public static void ComposeHeader(IContainer container, DateTime dateImpression)
    {
        var logo = Logo;
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(56).Element(e =>
                {
                    if (logo is { Length: > 0 })
                        e.Height(48).Image(logo).FitArea();
                });
                row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(c =>
                {
                    c.Item().Text("Société Nationale d'Electricité").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                    c.Item().Text("SNEL").SemiBold().FontSize(10).FontColor(Colors.Blue.Darken2);
                });
                row.ConstantItem(120).AlignRight().AlignMiddle().Column(c =>
                {
                    c.Item().Text("Kinshasa, le").FontSize(8);
                    c.Item().Text(dateImpression.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"))).Bold().FontSize(9);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1.2f).LineColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(1).LineHorizontal(0.5f).LineColor(Colors.Yellow.Darken2);
        });
    }

    public static void ComposeFooter(IContainer container, string referenceDocument)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.8f).LineColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(4).DefaultTextStyle(x => x.FontSize(6.5f)).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("SIEGE SOCIAL : KINSHASA").Bold();
                    c.Item().Text("2381, Avenue de la Justice");
                    c.Item().Text("B.P. 500 KINSHASA / GOMBE");
                    c.Item().Text("N.R.C. N° 6976 Kinshasa");
                    c.Item().Text("ID. NAT. : A 03970 O");
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("TELEX : 21347 SNEL RDC");
                    c.Item().Text("Tél. : + (243 12) 33 684 / 33 665");
                    c.Item().Text("Fax : + 243 12 33735");
                    c.Item().Text("+ 243 871 682 622 677");
                    c.Item().Text("E-mail : sneldg@ic.cd");
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("UBC : 201 – 134 502 - 10");
                    c.Item().Text("B.C.D : 901 – 001 42 02 – 34");
                    c.Item().Text("BCDC : 101 – 095 18 51 – 03");
                    c.Item().Text("Citibank : 831 – 300 026 – 001");
                    c.Item().Text("BCCE : 301– 0019530 – 17");
                    c.Item().Text("Sbic Bank : 101 091 093 70 01");
                });
            });
            col.Item().PaddingTop(3).Row(r =>
            {
                r.RelativeItem().Text(t =>
                {
                    t.Span("Document généré par SNEL e-Finance · ").FontSize(7);
                    t.Span(referenceDocument).FontSize(7).SemiBold();
                });
                r.ConstantItem(70).AlignRight().Text(t =>
                {
                    t.Span("Page ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" / ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        });
    }

    private static byte[]? LoadAsset(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name is not null)
        {
            using var s = asm.GetManifestResourceStream(name);
            if (s is null) return null;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Documents", "Assets", fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
