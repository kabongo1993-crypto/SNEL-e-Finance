using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

public sealed class RapportPrevisionDcConsolidePdfRenderer : IRapportPrevisionDcConsolidePdfRenderer
{
    static RapportPrevisionDcConsolidePdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(RapportDcConsolideDto rapport) => BuildDocument(rapport).GeneratePdf();

    public IReadOnlyList<string> RenderPreviewImages(RapportDcConsolideDto rapport, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var paths = new List<string>();
        BuildDocument(rapport).GenerateImages(index =>
        {
            var path = Path.Combine(outputDirectory, $"page-{index + 1:00}.png");
            paths.Add(path);
            return path;
        });
        return paths;
    }

    private static Document BuildDocument(RapportDcConsolideDto rapport)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginLeft(24);
                page.MarginRight(24);
                page.MarginTop(20);
                page.MarginBottom(64);
                page.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Colors.Grey.Darken3));

                SnelInstitutionalPdfChrome.ApplyBackgroundWatermark(page);
                page.Header().Element(h => ComposeHeader(h, rapport));
                page.Footer().Element(f =>
                    SnelInstitutionalPdfChrome.ComposeFooter(f, rapport.EnTete.ReferenceDocument));

                page.Content().PaddingTop(4).Column(col =>
                {
                    if (rapport.Groupes.Count == 0)
                    {
                        col.Item().PaddingTop(16).AlignCenter()
                            .Text("Aucune prévision DC consolidée pour ces critères.").Italic();
                        return;
                    }

                    ComposeTable(col, rapport);
                    col.Item().PaddingTop(6).AlignRight()
                        .Text($"TOTAL GÉNÉRAL DC : {SnelUsdFormat.WithCurrency(rapport.EnTete.MontantTotalDc)}")
                        .Bold().FontSize(10);
                });
            });
        });
    }

    private static void ComposeHeader(IContainer container, RapportDcConsolideDto rapport)
    {
        container.Column(col =>
        {
            SnelInstitutionalPdfChrome.ComposeHeader(col.Item(), rapport.EnTete.DateImpression);
            col.Item().PaddingTop(4).AlignCenter().Text(rapport.EnTete.Titre)
                .Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
            col.Item().AlignCenter().Text($"Référence : {rapport.EnTete.ReferenceDocument}").FontSize(7);
            col.Item().PaddingTop(2).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4)
                .DefaultTextStyle(x => x.FontSize(7)).Column(box =>
                {
                    box.Item().Text(
                        $"Exercice {rapport.EnTete.Annee} · V{rapport.EnTete.NumeroVersion:00}" +
                        (string.IsNullOrWhiteSpace(rapport.EnTete.LibelleVersion)
                            ? ""
                            : $" — {rapport.EnTete.LibelleVersion}") +
                        $" · Statut {rapport.EnTete.StatutFiltre} · {rapport.EnTete.Devise}");
                    box.Item().Text(
                        $"Périmètre : {(rapport.EnTete.CodeEntite is null ? "TOUTES LES UB" : $"Entité {rapport.EnTete.CodeEntite} — {rapport.EnTete.LibelleEntite}")}" +
                        (rapport.EnTete.CodeDepartementStructure is null
                            ? ""
                            : $" · Dépt. {rapport.EnTete.CodeDepartementStructure}") +
                        (rapport.EnTete.CodeDivision is null
                            ? ""
                            : $" · Div. {rapport.EnTete.CodeDivision}"));
                    box.Item().Text(
                        $"{rapport.EnTete.NbUB} UB consolidée(s) · TOTAL {SnelUsdFormat.WithCurrency(rapport.EnTete.MontantTotalDc)}")
                        .Bold();
                });
        });
    }

    private static void ComposeTable(ColumnDescriptor col, RapportDcConsolideDto rapport)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(40);
                c.RelativeColumn(2.6f);
                c.ConstantColumn(28);
                c.ConstantColumn(52);
                for (var i = 0; i < 12; i++) c.ConstantColumn(36);
            });

            Header(t, "ITEM");
            Header(t, "DESIGNATION");
            Header(t, "MODE");
            Header(t, "CUMUL");
            foreach (var m in new[] { "JAN", "FÉV", "MAR", "AVR", "MAI", "JUN", "JUL", "AOÛ", "SEP", "OCT", "NOV", "DÉC" })
                Header(t, m);

            foreach (var g in rapport.Groupes)
            {
                t.Cell().ColumnSpan(16).Background(Colors.Blue.Lighten3).Padding(3)
                    .Text($"{g.CodeGroupe} — {g.Libelle}").Bold().FontSize(8);

                foreach (var l in g.Lignes)
                {
                    var mensuel = string.Equals(l.CodeMode, "MENSUEL", StringComparison.OrdinalIgnoreCase);
                    Body(t, l.CodeRB);
                    Body(t, l.Libelle);
                    Body(t, RapportModeAffichage.Abbreviate(l.CodeMode));
                    Body(t, SnelUsdFormat.Number(l.MontantCumul), right: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M01) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M02) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M03) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M04) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M05) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M06) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M07) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M08) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M09) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M10) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M11) : "—", right: true, tiny: true);
                    Body(t, mensuel ? SnelUsdFormat.Cell(l.M12) : "—", right: true, tiny: true);
                }

                t.Cell().ColumnSpan(3).Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(2)
                    .AlignRight().Text("SOUS-TOTAL GROUPE").SemiBold().FontSize(7);
                t.Cell().Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(2)
                    .AlignRight().Text(SnelUsdFormat.Number(g.SousTotalDc)).SemiBold().FontSize(7);
                for (var i = 0; i < 12; i++)
                {
                    t.Cell().Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(1);
                }
            }
        });
    }

    private static void Header(TableDescriptor t, string text)
        => t.Cell().Background(Colors.Blue.Darken3).Padding(2)
            .Text(text).FontColor(Colors.White).Bold().FontSize(6.5f);

    private static void Body(TableDescriptor t, string text, bool right = false, bool tiny = false)
    {
        var cell = t.Cell().Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(1.5f);
        if (right) cell = cell.AlignRight();
        cell.Text(text).FontSize(tiny ? 6f : 7f);
    }
}
