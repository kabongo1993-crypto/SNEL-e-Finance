using System.Diagnostics;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

public static class QuestPdfWarmup
{
    public static long Run()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var sw = Stopwatch.StartNew();

        DemandePaiementPdfAssets.Preload();

        _ = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                DemandePaiementPdfAssets.ApplyDiscreteBackgroundWatermark(page);
                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Item().Element(c =>
                    {
                        var logo = DemandePaiementPdfAssets.Logo;
                        if (logo is { Length: > 0 })
                            c.Height(38).Image(logo).FitArea();
                    });
                    col.Item().Text("QuestPDF warmup").FontSize(8);
                });
            });
        }).GeneratePdf();

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }
}