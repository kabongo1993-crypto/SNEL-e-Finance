using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

public sealed class RapportPrevisionAePdfRenderer : IRapportPrevisionAePdfRenderer
{
    static RapportPrevisionAePdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(RapportAeDto rapport) => BuildDocument(rapport).GeneratePdf();

    public IReadOnlyList<string> RenderPreviewImages(RapportAeDto rapport, string outputDirectory)
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

    private static Document BuildDocument(RapportAeDto rapport)
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
                page.Header().Element(h => ComposePersistentHeader(h, rapport));
                page.Footer().Element(f =>
                    SnelInstitutionalPdfChrome.ComposeFooter(f, rapport.EnTete.ReferenceDocument));

                page.Content().PaddingTop(4).Column(col =>
                {
                    if (rapport.BlocsUb.Count == 0)
                    {
                        col.Item().PaddingTop(16).AlignCenter()
                            .Text("Aucune prévision AE correspondant aux critères.").Italic();
                        return;
                    }

                    var firstUb = true;
                    foreach (var bloc in rapport.BlocsUb)
                    {
                        if (!firstUb) col.Item().PageBreak();
                        firstUb = false;
                        ComposeUbBloc(col, bloc, rapport.EstTousLesMois);
                    }

                    if (rapport.EstTousLesMois && rapport.SyntheseItem is not null)
                    {
                        col.Item().PageBreak();
                        ComposeSyntheseItem(col, rapport.SyntheseItem);
                    }

                    if (rapport.EstTousLesMois && rapport.SyntheseRb is not null)
                    {
                        col.Item().PageBreak();
                        ComposeSyntheseRb(col, rapport.SyntheseRb);
                    }
                });
            });
        });
    }

    private static void ComposePersistentHeader(IContainer container, RapportAeDto rapport)
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
                        $"Périmètre : {rapport.EnTete.Niveau} · Entité : {rapport.EnTete.CodeEntite} — {rapport.EnTete.LibelleEntite}");
                    if (!string.IsNullOrWhiteSpace(rapport.EnTete.CodeDepartementStructure))
                    {
                        box.Item().Text(
                            $"Département : {rapport.EnTete.CodeDepartementStructure} — {rapport.EnTete.LibelleDepartementStructure}");
                    }

                    box.Item().Text(
                        $"Exercice {rapport.EnTete.Annee} · V{rapport.EnTete.NumeroVersion:00}" +
                        (string.IsNullOrWhiteSpace(rapport.EnTete.LibelleVersion) ? "" : $" — {rapport.EnTete.LibelleVersion}") +
                        $" · Mois {rapport.EnTete.LibelleMois} · Statut {rapport.EnTete.StatutFiltre} · {rapport.EnTete.NbUB} UB");
                });
        });
    }

    private static void ComposeUbBloc(ColumnDescriptor col, RapportAeUbBlocDto bloc, bool estTous)
    {
        var id = bloc.Identite;
        col.Item().Background(Colors.Blue.Darken3).Padding(5)
            .Text($"UB {id.CodeUB} — {id.LibelleUB}").FontColor(Colors.White).Bold().FontSize(10);

        col.Item().PaddingVertical(3).Border(0.5f).BorderColor(Colors.Blue.Lighten2).Padding(5).Column(box =>
        {
            box.Spacing(1);
            box.Item().Text($"Entité : {id.CodeEntite} — {id.LibelleEntite}");
            box.Item().Text($"Département : {(id.CodeDepartementStructure ?? "—")} — {(id.LibelleDepartementStructure ?? "—")}");
            box.Item().Text($"Type {id.TypeBudget} · Statut {id.Statut} · Exercice {id.Annee} · V{id.NumeroVersion:00}");
        });

        var firstDetail = true;
        foreach (var detail in bloc.DetailsMensuels)
        {
            if (estTous)
            {
                if (!firstDetail) col.Item().PaddingTop(8);
                col.Item().Background(Colors.Blue.Darken2).Padding(4)
                    .Text(detail.LibelleMois).FontColor(Colors.White).Bold().FontSize(9);
            }

            firstDetail = false;
            ComposeDetailTable(col, detail);
            col.Item().AlignRight().PaddingTop(3)
                .Text($"T O T A L : {SnelUsdFormat.WithCurrency(detail.TotalGeneral)}").Bold().FontSize(8);
        }
    }

    private static void ComposeDetailTable(ColumnDescriptor col, RapportAeDetailMensuelDto detail)
    {
        var nbRb = detail.ColonnesRb.Count;
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(26);
                c.RelativeColumn(2.4f);
                for (var i = 0; i < nbRb; i++) c.ConstantColumn(Math.Max(36, 48 - Math.Min(nbRb, 8)));
                c.ConstantColumn(48);
            });

            Header(t, "N°");
            Header(t, "DÉSIGNATION");
            foreach (var rb in detail.ColonnesRb) Header(t, rb.CodeRB);
            Header(t, "TOTAL");

            foreach (var l in detail.Lignes)
            {
                if (string.Equals(l.TypeLigne, "GROUPE", StringComparison.OrdinalIgnoreCase))
                {
                    t.Cell().ColumnSpan((uint)(nbRb + 3)).Background(Colors.Blue.Lighten3).Padding(3)
                        .Text(l.LibelleGroupe ?? "").Bold().FontSize(8);
                    continue;
                }

                Body(t, l.Numero ?? "");
                Body(t, l.LibelleItemAE ?? "");
                for (var i = 0; i < nbRb; i++)
                {
                    var v = i < l.MontantsParRb.Count ? l.MontantsParRb[i] : null;
                    Body(t, v is null ? "—" : SnelUsdFormat.Number(v.Value), right: true, tiny: true);
                }

                Body(t, SnelUsdFormat.Number(l.Total ?? 0), right: true);
            }

            // Ligne totaux RB
            t.Cell().ColumnSpan(2).Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(2)
                .AlignRight().Text("TOTAL").SemiBold().FontSize(7);
            foreach (var tot in detail.TotauxParRb)
            {
                Body(t, SnelUsdFormat.Number(tot), right: true, tiny: true);
            }

            Body(t, SnelUsdFormat.Number(detail.TotalGeneral), right: true);
        });
    }

    private static void ComposeSyntheseItem(ColumnDescriptor col, RapportAeSyntheseItemDto synthese)
    {
        col.Item().Background(Colors.Blue.Darken4).Padding(6)
            .Text(synthese.Titre).FontColor(Colors.White).Bold().FontSize(10);

        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(26);
                c.RelativeColumn(2.2f);
                for (var i = 0; i < 12; i++) c.ConstantColumn(34);
                c.ConstantColumn(48);
            });

            Header(t, "N°");
            Header(t, "DÉSIGNATION");
            foreach (var m in RapportAeMoisCodes.LibellesCourts) Header(t, m);
            Header(t, "TOTAL");

            foreach (var l in synthese.Lignes)
            {
                if (string.Equals(l.TypeLigne, "GROUPE", StringComparison.OrdinalIgnoreCase))
                {
                    t.Cell().ColumnSpan(15).Background(Colors.Blue.Lighten3).Padding(3)
                        .Text(l.LibelleGroupe ?? "").Bold().FontSize(8);
                    continue;
                }

                Body(t, l.Numero ?? "");
                Body(t, l.LibelleItemAE ?? "");
                foreach (var v in new[] { l.M01, l.M02, l.M03, l.M04, l.M05, l.M06, l.M07, l.M08, l.M09, l.M10, l.M11, l.M12 })
                {
                    Body(t, v is null ? "—" : SnelUsdFormat.Number(v.Value), right: true, tiny: true);
                }

                Body(t, SnelUsdFormat.Number(l.Total ?? 0), right: true);
            }
        });

        col.Item().AlignRight().PaddingTop(4)
            .Text($"TOTAL : {SnelUsdFormat.WithCurrency(synthese.TotalGeneral)}").Bold().FontSize(9);
    }

    private static void ComposeSyntheseRb(ColumnDescriptor col, RapportAeSyntheseRbDto synthese)
    {
        col.Item().Background(Colors.Blue.Darken4).Padding(6)
            .Text(synthese.Titre).FontColor(Colors.White).Bold().FontSize(10);

        col.Item().PaddingTop(4).Table(t =>
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
            foreach (var m in RapportAeMoisCodes.LibellesCourts) Header(t, m);

            foreach (var g in synthese.Groupes)
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
                    foreach (var v in new[] { l.M01, l.M02, l.M03, l.M04, l.M05, l.M06, l.M07, l.M08, l.M09, l.M10, l.M11, l.M12 })
                    {
                        Body(t, mensuel ? SnelUsdFormat.Cell(v) : "—", right: true, tiny: true);
                    }
                }

                t.Cell().ColumnSpan(3).Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(2)
                    .AlignRight().Text("SOUS-TOTAL GROUPE").SemiBold().FontSize(7);
                t.Cell().Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(2)
                    .AlignRight().Text(SnelUsdFormat.Number(g.SousTotal)).SemiBold().FontSize(7);
                for (var i = 0; i < 12; i++)
                {
                    t.Cell().Border(0.35f).BorderColor(Colors.Grey.Lighten1).Padding(1);
                }
            }
        });

        col.Item().AlignRight().PaddingTop(6)
            .Text($"TOTAL GÉNÉRAL AE : {SnelUsdFormat.WithCurrency(synthese.TotalGeneral)}").Bold().FontSize(10);
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
