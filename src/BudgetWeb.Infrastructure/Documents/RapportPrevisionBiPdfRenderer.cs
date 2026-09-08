using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

public sealed class RapportPrevisionBiPdfRenderer : IRapportPrevisionBiPdfRenderer
{
    static RapportPrevisionBiPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(RapportBiDto rapport) => BuildDocument(rapport).GeneratePdf();

    private static Document BuildDocument(RapportBiDto rapport)
    {
        var showMonths = rapport.LayoutColonnes is RapportDcLayoutColonnes.Mensuel or RapportDcLayoutColonnes.Mixte;
        var landscape = showMonths;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.MarginLeft(24);
                page.MarginRight(24);
                page.MarginTop(22);
                page.MarginBottom(64);
                page.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Colors.Grey.Darken3));

                SnelInstitutionalPdfChrome.ApplyBackgroundWatermark(page);
                page.Header().Element(h => ComposeHeader(h, rapport));
                page.Footer().Element(f => SnelInstitutionalPdfChrome.ComposeFooter(f, rapport.EnTete.ReferenceDocument));

                page.Content().PaddingTop(4).Column(col =>
                {
                    if (rapport.Departements.Count == 0)
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("Aucune prévision BI correspondant aux critères.").Italic();
                        return;
                    }

                    var firstDept = true;
                    foreach (var dept in rapport.Departements)
                    {
                        if (!firstDept) col.Item().PaddingTop(10);
                        firstDept = false;

                        col.Item().Background(Colors.Blue.Darken3).Padding(5)
                            .Text($"DÉPARTEMENT : {dept.CodeDepartement} — {dept.LibelleDepartement}")
                            .FontColor(Colors.White).Bold().FontSize(9);

                        foreach (var ub in dept.Ubs)
                        {
                            col.Item().PaddingTop(6).Background(Colors.Blue.Darken4).Padding(4)
                                .Text($"UB {ub.Identite.CodeUB} — {ub.Identite.LibelleUB}")
                                .FontColor(Colors.White).SemiBold().FontSize(8);

                            var showM = ub.LayoutColonnes is RapportDcLayoutColonnes.Mensuel or RapportDcLayoutColonnes.Mixte
                                        || showMonths;
                            ComposeUbTable(col, ub, showM);
                        }

                        col.Item().PaddingTop(4).AlignRight()
                            .Text($"TOTAL DÉPARTEMENT : {SnelUsdFormat.WithCurrency(dept.TotalDepartement)}")
                            .Bold().FontSize(8);
                    }

                    if (rapport.SyntheseEntite is not null)
                    {
                        col.Item().PageBreak();
                        ComposeSynthese(col, rapport.SyntheseEntite);
                    }
                });
            });
        });
    }

    private static void ComposeHeader(IContainer header, RapportBiDto rapport)
    {
        header.Column(col =>
        {
            SnelInstitutionalPdfChrome.ComposeHeader(col.Item(), rapport.EnTete.DateImpression);
            col.Item().PaddingTop(4).Text(rapport.EnTete.Titre).Bold().FontSize(11);
            col.Item().Text(
                $"Périmètre : {rapport.EnTete.Niveau} · Entité : {rapport.EnTete.CodeEntite} — {rapport.EnTete.LibelleEntite}");
            if (!string.IsNullOrWhiteSpace(rapport.EnTete.CodeDepartementStructure))
            {
                col.Item().Text(
                    $"Département : {rapport.EnTete.CodeDepartementStructure} — {rapport.EnTete.LibelleDepartementStructure}");
            }

            col.Item().Text(
                $"Version V{rapport.EnTete.NumeroVersion:00}" +
                (string.IsNullOrWhiteSpace(rapport.EnTete.LibelleVersion) ? "" : $" — {rapport.EnTete.LibelleVersion}") +
                $" · Statut {rapport.EnTete.StatutFiltre} · {rapport.EnTete.NbDepartements} dép. · {rapport.EnTete.NbUB} UB · " +
                SnelUsdFormat.WithCurrency(rapport.EnTete.MontantTotalBi));
        });
    }

    private static void ComposeUbTable(ColumnDescriptor col, RapportBiUbBlocDto ub, bool showMonths)
    {
        col.Item().PaddingTop(3).Table(t =>
        {
            if (showMonths)
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(42);
                    c.RelativeColumn(2.6f);
                    c.ConstantColumn(22);
                    c.ConstantColumn(48);
                    for (var i = 0; i < 12; i++) c.ConstantColumn(32);
                });
                Header(t, "ITEM");
                Header(t, "DÉSIGNATION");
                Header(t, "MODE");
                Header(t, "CUMUL");
                foreach (var m in new[] { "JAN", "FÉV", "MAR", "AVR", "MAI", "JUN", "JUL", "AOÛ", "SEP", "OCT", "NOV", "DÉC" })
                    Header(t, m);
            }
            else
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(50);
                    c.RelativeColumn(4);
                    c.ConstantColumn(28);
                    c.RelativeColumn(1.4f);
                });
                Header(t, "ITEM");
                Header(t, "DÉSIGNATION");
                Header(t, "MODE");
                Header(t, "CUMUL");
            }

            foreach (var l in ub.Lignes)
            {
                if (l.TypeLigne == "CATEGORIE")
                {
                    var span = showMonths ? (uint)16 : 4;
                    t.Cell().ColumnSpan(span).Background(Colors.Blue.Lighten3).Padding(2)
                        .Text($"{l.CodeAffichage} {l.Libelle}".Trim()).Bold().FontSize(7);
                    continue;
                }

                if (l.TypeLigne == "TOTAL_UB")
                {
                    Body(t, "", bold: true);
                    Body(t, "TOTAL UB", bold: true);
                    Body(t, "");
                    Body(t, SnelUsdFormat.Number(l.MontantCumul), right: true, bold: true);
                    if (showMonths)
                    {
                        foreach (var v in Mois(l))
                            Body(t, FormatMoisAgrege(l, v), right: true, tiny: true);
                    }

                    continue;
                }

                var label = l.TypeLigne == "DETAIL"
                    ? (l.CodeAffichage is null ? l.Libelle : $"{l.CodeAffichage}  {l.Libelle}")
                    : l.Libelle;
                var itemCol = l.TypeLigne == "DETAIL" ? "" : (l.CodeAffichage ?? l.CodeItem ?? "");
                Body(t, itemCol, bold: l.TypeLigne == "ITEM");
                Body(t, label, bold: l.TypeLigne == "ITEM");
                Body(t, l.TypeLigne == "DETAIL" ? RapportModeAffichage.Abbreviate(l.CodeMode) : "");
                Body(t, SnelUsdFormat.Number(l.MontantCumul), right: true, bold: l.TypeLigne == "ITEM");
                if (showMonths)
                {
                    foreach (var v in Mois(l))
                    {
                        if (l.TypeLigne == "DETAIL" && l.CodeMode is not null
                            && !string.Equals(l.CodeMode, "MENSUEL", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(l.CodeMode, "MENS", StringComparison.OrdinalIgnoreCase))
                        {
                            Body(t, "—", right: true, tiny: true);
                        }
                        else
                        {
                            Body(t, FormatMoisAgrege(l, v), right: true, tiny: true);
                        }
                    }
                }
            }
        });
    }

    private static void ComposeSynthese(ColumnDescriptor col, RapportBiSyntheseEntiteDto syn)
    {
        col.Item().Background(Colors.Blue.Darken4).Padding(6)
            .Text(syn.Titre).FontColor(Colors.White).Bold().FontSize(10);

        var nDept = syn.ColonnesDepartement.Count;
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.8f);
                for (var i = 0; i < nDept; i++) c.RelativeColumn(1);
                c.RelativeColumn(1.1f);
            });

            Header(t, "ITEM / DÉSIGNATION");
            foreach (var d in syn.ColonnesDepartement)
                Header(t, d.CodeDepartement);
            Header(t, "TOTAL");

            foreach (var l in syn.Lignes)
            {
                var label = string.IsNullOrWhiteSpace(l.CodeAffichage)
                    ? l.Libelle
                    : $"{l.CodeAffichage} {l.Libelle}";
                var bold = l.TypeLigne is "CATEGORIE" or "TOTAL";
                Body(t, label, bold: bold);
                for (var i = 0; i < nDept; i++)
                {
                    var v = i < l.MontantsParDepartement.Count ? l.MontantsParDepartement[i] : 0m;
                    Body(t, SnelUsdFormat.Number(v), right: true, bold: bold);
                }

                Body(t, SnelUsdFormat.Number(l.TotalLigne), right: true, bold: true);
            }
        });

        col.Item().PaddingTop(6).AlignRight()
            .Text($"TOTAL GÉNÉRAL BI : {SnelUsdFormat.WithCurrency(syn.TotalGeneral)}").Bold().FontSize(10);
    }

    private static IEnumerable<decimal?> Mois(RapportBiLigneDto l) =>
        [l.M01, l.M02, l.M03, l.M04, l.M05, l.M06, l.M07, l.M08, l.M09, l.M10, l.M11, l.M12];

    private static string FormatMoisAgrege(RapportBiLigneDto l, decimal? v)
    {
        if (v is null) return "—";
        if (l.EstAgrege && l.M01 is null && l.M02 is null) return "—";
        return SnelUsdFormat.Number(v.Value);
    }

    private static void Header(TableDescriptor t, string text)
        => t.Cell().Background(Colors.Blue.Darken3).Padding(1.5f)
            .Text(text).FontColor(Colors.White).Bold().FontSize(6);

    private static void Body(TableDescriptor t, string text, bool right = false, bool tiny = false, bool bold = false)
    {
        var cell = t.Cell().Border(0.3f).BorderColor(Colors.Grey.Lighten1).Padding(1.2f);
        if (right) cell = cell.AlignRight();
        var tx = cell.Text(text).FontSize(tiny ? 5.5f : 6.5f);
        if (bold) tx.SemiBold();
    }
}
