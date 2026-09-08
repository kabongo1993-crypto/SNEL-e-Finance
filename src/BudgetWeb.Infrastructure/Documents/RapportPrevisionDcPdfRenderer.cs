using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

public sealed class RapportPrevisionDcPdfRenderer : IRapportPrevisionDcPdfRenderer
{
    static RapportPrevisionDcPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(RapportDcDto rapport) => BuildDocument(rapport).GeneratePdf();

    public IReadOnlyList<string> RenderPreviewImages(RapportDcDto rapport, string outputDirectory)
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

    private static Document BuildDocument(RapportDcDto rapport)
    {
        var showMonths = rapport.LayoutColonnes is RapportDcLayoutColonnes.Mensuel or RapportDcLayoutColonnes.Mixte;
        var landscape = showMonths;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.MarginLeft(28);
                page.MarginRight(28);
                page.MarginTop(24);
                page.MarginBottom(68);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken3));

                SnelInstitutionalPdfChrome.ApplyBackgroundWatermark(page);
                page.Header().Element(h => ComposePersistentHeader(h, rapport));
                page.Footer().Element(f => SnelInstitutionalPdfChrome.ComposeFooter(f, rapport.EnTete.ReferenceDocument));

                page.Content().PaddingTop(4).Column(col =>
                {
                    if (rapport.BlocsUb.Count == 0)
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("Aucune prévision DC correspondant aux critères.").Italic();
                        return;
                    }

                    var first = true;
                    foreach (var bloc in rapport.BlocsUb)
                    {
                        if (!first) col.Item().PageBreak();
                        first = false;
                        ComposeUbBloc(col, bloc, showMonths, rapport.EnTete);
                    }

                    if (rapport.GroupesConsolides.Count > 0)
                    {
                        col.Item().PageBreak();
                        ComposeSyntheseConsolidee(col, rapport, showMonths);
                    }
                });
            });
        });
    }

    private static void ComposeSyntheseConsolidee(ColumnDescriptor col, RapportDcDto rapport, bool showMonths)
    {
        col.Item().Background(Colors.Blue.Darken4).Padding(6)
            .Text("SYNTHÈSE CONSOLIDÉE DU PÉRIMÈTRE — TOUTES LES UB DU RAPPORT")
            .FontColor(Colors.White).Bold().FontSize(11);
        col.Item().PaddingTop(3).Text(
            $"{rapport.EnTete.NbUB} UB · TOTAL {SnelUsdFormat.WithCurrency(rapport.EnTete.MontantTotalDc)} · {rapport.EnTete.Devise}")
            .SemiBold();

        col.Item().PaddingTop(4).Table(t =>
        {
            if (showMonths)
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(40);
                    c.RelativeColumn(2.4f);
                    c.ConstantColumn(28);
                    c.ConstantColumn(50);
                    for (var i = 0; i < 12; i++) c.ConstantColumn(34);
                });
                Header(t, "ITEM");
                Header(t, "DESIGNATION");
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
                    c.ConstantColumn(36);
                    c.RelativeColumn(1.5f);
                });
                Header(t, "ITEM");
                Header(t, "DESIGNATION");
                Header(t, "MODE");
                Header(t, "MONTANT ANNUEL (USD)");
            }

            foreach (var g in rapport.GroupesConsolides)
            {
                t.Cell().ColumnSpan(showMonths ? (uint)16 : 4).Background(Colors.Blue.Lighten3).Padding(3)
                    .Text($"{g.CodeGroupe} — {g.Libelle}").Bold().FontSize(8);

                foreach (var l in g.Lignes)
                {
                    var mensuel = string.Equals(l.CodeMode, "MENSUEL", StringComparison.OrdinalIgnoreCase);
                    Body(t, l.CodeRB);
                    Body(t, l.Libelle);
                    Body(t, RapportModeAffichage.Abbreviate(l.CodeMode));
                    Body(t, SnelUsdFormat.Number(l.MontantCumul), right: true);
                    if (showMonths)
                    {
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
                }

                if (showMonths)
                {
                    t.Cell().ColumnSpan(3).Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(2)
                        .AlignRight().Text("SOUS-TOTAL GROUPE").SemiBold().FontSize(7);
                    t.Cell().Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(2)
                        .AlignRight().Text(SnelUsdFormat.Number(g.SousTotalDc)).SemiBold().FontSize(7);
                    for (var i = 0; i < 12; i++)
                        t.Cell().Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(1);
                }
                else
                {
                    t.Cell().ColumnSpan(3).Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(2)
                        .AlignRight().Text("SOUS-TOTAL GROUPE").SemiBold().FontSize(7);
                    t.Cell().Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(2)
                        .AlignRight().Text(SnelUsdFormat.Number(g.SousTotalDc)).SemiBold().FontSize(7);
                }
            }
        });

        col.Item().PaddingTop(6).AlignRight()
            .Text($"TOTAL CONSOLIDÉ DC : {SnelUsdFormat.WithCurrency(rapport.EnTete.MontantTotalDc)}")
            .Bold().FontSize(10);
    }

    private static void ComposePersistentHeader(IContainer container, RapportDcDto rapport)
    {
        container.Column(col =>
        {
            SnelInstitutionalPdfChrome.ComposeHeader(col.Item(), rapport.EnTete.DateImpression);
            col.Item().PaddingTop(4).AlignCenter().Text(rapport.EnTete.Titre).Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
            col.Item().AlignCenter().Text($"Référence : {rapport.EnTete.ReferenceDocument}").FontSize(7);
            col.Item().PaddingTop(2).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4).DefaultTextStyle(x => x.FontSize(7)).Column(box =>
            {
                box.Item().Text(
                    $"Périmètre : {rapport.EnTete.Niveau} · Entité : {rapport.EnTete.CodeEntite} — {rapport.EnTete.LibelleEntite}");
                if (!string.IsNullOrWhiteSpace(rapport.EnTete.CodeDepartementStructure))
                {
                    box.Item().Text(
                        $"Département (structure) : {rapport.EnTete.CodeDepartementStructure} — {rapport.EnTete.LibelleDepartementStructure}");
                }

                if (!string.IsNullOrWhiteSpace(rapport.EnTete.CodeDivision))
                {
                    box.Item().Text($"Filtre Division : {rapport.EnTete.CodeDivision} — {rapport.EnTete.LibelleDivision}");
                }

                box.Item().Text(
                    $"Exercice {rapport.EnTete.Annee} · V{rapport.EnTete.NumeroVersion:00}" +
                    (string.IsNullOrWhiteSpace(rapport.EnTete.LibelleVersion) ? "" : $" — {rapport.EnTete.LibelleVersion}") +
                    $" · Statut {rapport.EnTete.StatutFiltre} · {rapport.EnTete.Devise} · {rapport.EnTete.NbUB} UB · TOTAL {SnelUsdFormat.WithCurrency(rapport.EnTete.MontantTotalDc)}");
            });
        });
    }

    private static void ComposeUbBloc(ColumnDescriptor col, RapportDcUbBlocDto bloc, bool showMonths, RapportDcEnTeteDto enTete)
    {
        var id = bloc.Identite;
        col.Item().Background(Colors.Blue.Darken3).Padding(5).Column(band =>
        {
            band.Item().Text($"UB {id.CodeUB} — {id.LibelleUB}").FontColor(Colors.White).Bold().FontSize(10);
        });

        col.Item().PaddingVertical(3).Border(0.5f).BorderColor(Colors.Blue.Lighten2).Padding(5).Column(box =>
        {
            box.Spacing(1);
            box.Item().Text($"Entité : {id.CodeEntite} — {id.LibelleEntite}");
            box.Item().Text(
                $"Département (structure) : {(id.CodeDepartementStructure ?? "—")} — {(id.LibelleDepartementStructure ?? "—")}");
            box.Item().Text($"Département (métier) : {id.CodeDepartementTable} — {id.LibelleDepartementTable}");
            box.Item().Text($"Division : {(id.CodeDivision ?? "—")} — {(id.LibelleDivision ?? "—")}");
            box.Item().Text(
                $"Type {id.TypeBudget} · Statut {id.Statut} · Devise {id.Devise} · Exercice {id.Annee} · V{id.NumeroVersion:00}" +
                (string.IsNullOrWhiteSpace(id.LibelleVersion) ? "" : $" — {id.LibelleVersion}"));
            _ = enTete;
        });

        ComposeTable(col, bloc, showMonths);
        col.Item().AlignRight().PaddingTop(4)
            .Text($"TOTAL UB {id.CodeUB} : {SnelUsdFormat.WithCurrency(bloc.TotalDc)}").Bold().FontSize(9);
    }

    private static void ComposeTable(ColumnDescriptor col, RapportDcUbBlocDto ub, bool showMonths)
    {
        col.Item().Table(t =>
        {
            if (showMonths)
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(42);
                    c.RelativeColumn(2.4f);
                    c.ConstantColumn(48);
                    for (var i = 0; i < 12; i++) c.ConstantColumn(34);
                });
                Header(t, "ITEM");
                Header(t, "DESIGNATION");
                Header(t, "CUMUL");
                foreach (var m in new[] { "JAN", "FÉV", "MAR", "AVR", "MAI", "JUN", "JUL", "AOÛ", "SEP", "OCT", "NOV", "DÉC" })
                    Header(t, m);
            }
            else
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(55);
                    c.RelativeColumn(4);
                    c.RelativeColumn(1.6f);
                });
                Header(t, "ITEM");
                Header(t, "DESIGNATION");
                Header(t, "MONTANT ANNUEL (USD)");
            }

            foreach (var g in ub.Groupes)
            {
                if (showMonths)
                {
                    t.Cell().ColumnSpan(15).Background(Colors.Blue.Lighten3).Padding(3)
                        .Text($"{g.CodeGroupe} — {g.Libelle}").Bold().FontSize(7);
                }
                else
                {
                    t.Cell().ColumnSpan(3).Background(Colors.Blue.Lighten3).Padding(3)
                        .Text($"{g.CodeGroupe} — {g.Libelle}").Bold().FontSize(8);
                }

                foreach (var l in g.Lignes)
                {
                    Body(t, l.CodeRB);
                    Body(t, l.Libelle);
                    Body(t, SnelUsdFormat.Number(l.MontantAnnuel), right: true);
                    if (showMonths)
                    {
                        var mensuel = string.Equals(l.CodeMode, "MENSUEL", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(l.CodeMode, "MENS", StringComparison.OrdinalIgnoreCase);
                        // ANNUEL : tirets — jamais de ventilation artificielle
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M01, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M02, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M03, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M04, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M05, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M06, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M07, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M08, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M09, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M10, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M11, dashIfNull: true) : "—", right: true, tiny: true);
                        Body(t, mensuel ? SnelUsdFormat.Cell(l.M12, dashIfNull: true) : "—", right: true, tiny: true);
                    }
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
