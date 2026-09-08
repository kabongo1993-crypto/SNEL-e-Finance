using System.Globalization;
using BudgetWeb.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

/// <summary>
/// État imprimable A5 paysage (210 mm × 148 mm) — Billet de conversion.
/// Reproduction fidèle de l'état de sortie institutionnel SNEL/DFI.
/// </summary>
internal static class BilletConversionPdfComposer
{
    /// <summary>QuestPDF A5 paysage = 210 mm (L) × 148 mm (H).</summary>
    public static readonly PageSize PageSizeA5Landscape = PageSizes.A5.Landscape();

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private const float Mm = 2.834645669f;

    private static readonly string HeaderBlue = "#0B3D7A";
    private static readonly string DateBlue = "#2E6DB4";
    private static readonly string AccentBlue = "#1565C0";
    private static readonly string FieldBorder = "#4A90D9";
    private static readonly string PanelBorder = "#3D7FBF";

    private static float MmToPt(float mm) => mm * Mm;

    public static void ComposeDocument(PageDescriptor page, BilletConversionDocumentDto p)
    {
        page.Size(PageSizeA5Landscape);
        page.Margin(MmToPt(3));
        page.DefaultTextStyle(x => x.FontSize(9f).FontColor(Colors.Black));

        ApplyWatermark(page);

        page.Content()
            .Border(1.2f).BorderColor(PanelBorder).CornerRadius(MmToPt(3.5f))
            .Background(Colors.White)
            .Padding(MmToPt(2.5f))
            .Column(col =>
            {
                col.Spacing(MmToPt(1.2f));
                col.Item().Element(c => ComposeHeader(c, p));
                col.Item().Element(ComposeTitle);
                col.Item().PaddingHorizontal(MmToPt(0.5f)).Element(c => ComposeFormBody(c, p));
                col.Item().Element(c => ComposeSignatures(c, p));
            });
    }

    private static void ApplyWatermark(PageDescriptor page)
    {
        var wm = SnelInstitutionalPdfChrome.Watermark;
        if (wm is not { Length: > 0 })
            return;

        page.Background()
            .AlignCenter()
            .AlignMiddle()
            .Width(MmToPt(125))
            .Image(wm)
            .FitArea();
    }

    private static void ComposeHeader(IContainer container, BilletConversionDocumentDto p)
    {
        var logo = SnelInstitutionalPdfChrome.Logo;
        var dateDoc = p.DateImpression.ToString("dd/MM/yyyy", Fr);

        container.MinHeight(MmToPt(16)).Background(HeaderBlue).CornerRadius(MmToPt(3)).Padding(MmToPt(2)).Row(row =>
        {
            row.ConstantItem(MmToPt(18)).AlignMiddle().Element(e =>
            {
                if (logo is { Length: > 0 })
                    e.Height(MmToPt(13)).Image(logo).FitArea();
            });

            row.RelativeItem().PaddingLeft(MmToPt(2)).AlignMiddle().Column(c =>
            {
                c.Item().Text("SOCIETE NATIONALE D'ELECTRICITE SA")
                    .Bold().FontSize(10.5f).FontColor(Colors.White);
                c.Item().Text("DEPARTEMENT DES FINANCES")
                    .SemiBold().FontSize(9.5f).FontColor(Colors.White);
                c.Item().Text("DFI/DBU")
                    .SemiBold().FontSize(9.5f).FontColor(Colors.White);
            });

            row.ConstantItem(MmToPt(26)).AlignRight().AlignMiddle()
                .Background(DateBlue).CornerRadius(MmToPt(2.5f))
                .PaddingVertical(MmToPt(1.2f)).PaddingHorizontal(MmToPt(2))
                .Column(c =>
                {
                    c.Item().AlignCenter().Text("\u2637").FontSize(11f).FontColor(Colors.White);
                    c.Item().AlignCenter().Text(dateDoc).Bold().FontSize(9.5f).FontColor(Colors.White);
                });
        });
    }

    private static void ComposeTitle(IContainer container)
    {
        const string title = "JUSTIFICATION MONTANT PAYE EN FC PAR REFERENCE A :";

        container.PaddingVertical(MmToPt(0.5f)).Row(row =>
        {
            row.RelativeItem().AlignMiddle().PaddingRight(MmToPt(1))
                .Row(line =>
                {
                    line.RelativeItem().AlignMiddle().LineHorizontal(0.8f).LineColor(AccentBlue);
                    line.AutoItem().PaddingHorizontal(MmToPt(0.8f))
                        .Text("\u25C6").FontSize(7f).FontColor(AccentBlue);
                    line.RelativeItem().AlignMiddle().LineHorizontal(0.8f).LineColor(AccentBlue);
                });

            row.AutoItem().PaddingHorizontal(MmToPt(2))
                .Text(title).Bold().FontSize(10.5f).FontColor(HeaderBlue);

            row.RelativeItem().AlignMiddle().PaddingLeft(MmToPt(1))
                .Row(line =>
                {
                    line.RelativeItem().AlignMiddle().LineHorizontal(0.8f).LineColor(AccentBlue);
                    line.AutoItem().PaddingHorizontal(MmToPt(0.8f))
                        .Text("\u25C6").FontSize(7f).FontColor(AccentBlue);
                    line.RelativeItem().AlignMiddle().LineHorizontal(0.8f).LineColor(AccentBlue);
                });
        });
    }

    // Grille fixe du formulaire (mm) — colonnes identiques sur toutes les lignes.
    private static readonly float GridMinus = 5.5f;
    private static readonly float GridSpacer = 2f;
    private static readonly float GridLabel = 46f;
    private static readonly float GridColon = 3f;
    private static readonly float GridSplitValue = 34f;
    private static readonly float GridSplitLabel = 28f;
    private static readonly float GridSplitColon = 3f;
    private static readonly float GridRowMinHeight = 7f;

    private static void ComposeFormBody(IContainer container, BilletConversionDocumentDto p)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(MmToPt(GridMinus));
                columns.ConstantColumn(MmToPt(GridSpacer));
                columns.ConstantColumn(MmToPt(GridLabel));
                columns.ConstantColumn(MmToPt(GridColon));
                columns.ConstantColumn(MmToPt(GridSplitValue));
                columns.ConstantColumn(MmToPt(GridSplitLabel));
                columns.ConstantColumn(MmToPt(GridSplitColon));
                columns.RelativeColumn();
            });

            FullWidthRow(table, "ID", p.IdDemandePaiement.ToString(Fr));
            FullWidthRow(table, "DEMANDE DE PAIEMENT N°", p.Reference);
            FullWidthRow(table, "DEMANDE DE CHEQUE N°", DisplayOptional(p.DemandeChequeNumero));
            FullWidthRow(table, "BENEFICIAIRE", p.Beneficiaire);

            SplitRow(table,
                "MONTANT A PAYER\n(TOTAL)", FormatMontant(p.MontantDeviseOrigine),
                "DEVISE", DisplayDevise(p.DeviseOrigine));

            SplitRow(table,
                "TAUX UTILISE", FormatTaux(p.TauxApplique),
                "DATE DE CONVERSION", p.DateConversion.ToString("dd/MM/yyyy", Fr));

            FullWidthRow(table, "COURS D'ECHANGE/\nBANQUE", DisplayOptional(p.CoursEchangeBanque));
            FullWidthRow(table, "MONTANT EN FC", FormatMontant(p.MontantCdf));
            FullWidthRow(table, "SOLDE A PAYER SUR LA DEMANDE\n(DEVISE)",
                DisplayOptionalMontant(p.SoldeAPayerDevise));
        });
    }

    private static void FullWidthRow(TableDescriptor table, string label, string value)
    {
        GridRowStart(table);
        LabelCell(table, label);
        ColonCell(table);
        table.Cell().ColumnSpan(4).AlignMiddle()
            .PaddingBottom(MmToPt(1.2f))
            .Element(c => ValueBox(c, value));
    }

    private static void SplitRow(
        TableDescriptor table,
        string labelLeft,
        string valueLeft,
        string labelRight,
        string valueRight)
    {
        GridRowStart(table);
        LabelCell(table, labelLeft);
        ColonCell(table);
        table.Cell().AlignMiddle().PaddingBottom(MmToPt(1.2f)).Element(c => ValueBox(c, valueLeft));
        table.Cell().AlignMiddle().PaddingBottom(MmToPt(1.2f)).Element(c => LabelText(c, labelRight));
        table.Cell().AlignMiddle().PaddingBottom(MmToPt(1.2f)).Element(ColonText);
        table.Cell().AlignMiddle().PaddingBottom(MmToPt(1.2f)).Element(c => ValueBox(c, valueRight));
    }

    private static void GridRowStart(TableDescriptor table)
    {
        var rowPad = MmToPt(1.2f);
        table.Cell().MinHeight(MmToPt(GridRowMinHeight)).AlignMiddle()
            .PaddingBottom(rowPad)
            .Element(MinusBox);
        table.Cell().MinHeight(MmToPt(GridRowMinHeight)).PaddingBottom(rowPad);
    }

    private static void LabelCell(TableDescriptor table, string label)
    {
        table.Cell().MinHeight(MmToPt(GridRowMinHeight)).AlignMiddle()
            .PaddingBottom(MmToPt(1.2f))
            .Element(c => LabelText(c, label));
    }

    private static void ColonCell(TableDescriptor table)
    {
        table.Cell().MinHeight(MmToPt(GridRowMinHeight)).AlignMiddle()
            .PaddingBottom(MmToPt(1.2f))
            .Element(ColonText);
    }

    private static void MinusBox(IContainer container)
    {
        container.Width(MmToPt(GridMinus)).Height(MmToPt(GridMinus))
            .Background(AccentBlue).CornerRadius(1.8f)
            .AlignCenter().AlignMiddle()
            .Text("-").Bold().FontSize(9f).FontColor(Colors.White);
    }

    private static void LabelText(IContainer container, string label)
    {
        container.Text(label).SemiBold().FontSize(9.5f).LineHeight(1.05f);
    }

    private static void ColonText(IContainer container)
    {
        container.AlignCenter().Text(":").FontSize(9.5f);
    }

    private static void ValueBox(IContainer container, string value)
    {
        var isEmpty = string.IsNullOrEmpty(value);

        container.MinHeight(MmToPt(5.5f))
            .Border(0.7f).BorderColor(FieldBorder).CornerRadius(MmToPt(2))
            .Background(Colors.White)
            .PaddingHorizontal(MmToPt(2))
            .PaddingVertical(MmToPt(1))
            .AlignMiddle()
            .Element(inner =>
            {
                if (isEmpty)
                {
                    inner.AlignBottom().PaddingBottom(MmToPt(0.5f))
                        .BorderBottom(0.6f).BorderColor(Colors.Grey.Lighten1)
                        .Height(MmToPt(4));
                }
                else
                {
                    inner.Text(value).FontSize(9.5f);
                }
            });
    }

    private static void ComposeSignatures(IContainer container, BilletConversionDocumentDto p)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => SignatureBlock(c, "ETABLI PAR", p.EtabliPar));
            row.ConstantItem(MmToPt(3));
            row.RelativeItem().Element(c => SignatureBlock(c, "APPROUVE PAR", p.ApprouvePar));
            row.ConstantItem(MmToPt(3));
            row.RelativeItem().Element(c => SignatureBlock(c, "VISA", p.Visa));
        });
    }

    private static void SignatureBlock(IContainer container, string label, string? name)
    {
        container.MinHeight(MmToPt(20)).Border(0.8f).BorderColor(PanelBorder).CornerRadius(MmToPt(2.5f))
            .Column(col =>
            {
                col.Item().Background(HeaderBlue).CornerRadiusTopLeft(MmToPt(2.5f)).CornerRadiusTopRight(MmToPt(2.5f))
                    .PaddingVertical(MmToPt(1.5f)).AlignCenter()
                    .Text(label).Bold().FontSize(8.5f).FontColor(Colors.White);

                col.Item().PaddingHorizontal(MmToPt(2)).PaddingTop(MmToPt(1.5f)).Column(inner =>
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        inner.Item().AlignCenter().Text(name.Trim())
                            .SemiBold().FontSize(9f);
                    }

                    inner.Item().PaddingTop(MmToPt(3))
                        .AlignBottom()
                        .BorderBottom(0.6f).BorderColor(Colors.Grey.Lighten1)
                        .Height(MmToPt(8));
                });
            });
    }

    private static string DisplayOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string DisplayOptionalMontant(decimal? value)
        => value is null ? string.Empty : FormatMontant(value.Value);

    private static string FormatMontant(decimal montant)
        => string.Format(Fr, "{0:N2}", montant);

    private static string FormatTaux(decimal taux)
        => string.Format(Fr, "{0:N2}", taux);

    private static string DisplayDevise(string devise)
    {
        var code = devise.Trim().ToUpperInvariant();
        return code == "CDF" ? "FCFA" : code;
    }
}
