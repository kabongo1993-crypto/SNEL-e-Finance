using System.Globalization;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

internal static class DocumentInstrumentPdfComposer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static void ComposeDocument(
        PageDescriptor page,
        string titre,
        string numero,
        DateOnly dateDocument,
        DateTime dateImpression,
        IReadOnlyList<(string Label, string Value)> champs,
        string identifiantVerification,
        string? etabliPar)
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(x => x.FontSize(10f).FontColor(Colors.Black));

        SnelInstitutionalPdfChrome.ApplyDiscreteBackgroundWatermark(page);

        page.Content().Column(col =>
        {
            col.Spacing(8);
            SnelInstitutionalPdfChrome.ComposeHeader(col.Item(), dateImpression);

            col.Item().PaddingTop(8).AlignCenter()
                .Text(titre).Bold().FontSize(14f).FontColor(Colors.Blue.Darken3);
            col.Item().AlignCenter()
                .Text($"N° {numero} — {dateDocument.ToString("dd/MM/yyyy", Fr)}")
                .SemiBold().FontSize(11f);

            col.Item().PaddingTop(12).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(150);
                    columns.RelativeColumn();
                });

                foreach (var (label, value) in champs)
                {
                    table.Cell().PaddingVertical(4).Text(label).SemiBold();
                    table.Cell().PaddingVertical(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
                }
            });

            col.Item().PaddingTop(16).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Établi par").SemiBold().FontSize(9f);
                    c.Item().PaddingTop(24).BorderBottom(0.6f).BorderColor(Colors.Grey.Darken1)
                        .Text(etabliPar ?? string.Empty).FontSize(10f);
                });

                row.ConstantItem(120).Border(1f).BorderColor(Colors.Grey.Lighten1)
                    .MinHeight(100).Padding(6).Column(qr =>
                    {
                        qr.Item().AlignCenter().Text("Vérification").Bold().FontSize(8f);
                        qr.Item().PaddingTop(6).AlignCenter()
                            .Text("QR").FontSize(22f).FontColor(Colors.Grey.Lighten1);
                        qr.Item().PaddingTop(4).AlignCenter()
                            .Text(identifiantVerification).FontSize(6.5f);
                    });
            });
        });

        page.Footer().Element(f =>
            SnelInstitutionalPdfChrome.ComposeFooter(f, numero));
    }

    public static string FormatMontant(decimal montant, string? devise = null)
    {
        var formatted = string.Format(Fr, "{0:N2}", montant);
        return string.IsNullOrWhiteSpace(devise) ? formatted : $"{formatted} {devise.Trim().ToUpperInvariant()}";
    }
}

public class QuestPdfPieceCaisseRenderer : IPieceCaisseDocumentRenderer
{
    static QuestPdfPieceCaisseRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(PieceCaisseDocumentDto payload)
        => Document.Create(doc =>
        {
            doc.Page(page => PieceCaissePdfComposer.ComposeDocument(page, payload));
        }).GeneratePdf();
}

public class QuestPdfBonProvisoireRenderer : IBonProvisoireDocumentRenderer
{
    static QuestPdfBonProvisoireRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(BonProvisoireDocumentDto payload)
        => Document.Create(doc =>
        {
            doc.Page(page => BonProvisoirePdfComposer.ComposeDocument(page, payload));
        }).GeneratePdf();
}

public class QuestPdfMinuteChequeRenderer : IMinuteChequeDocumentRenderer
{
    static QuestPdfMinuteChequeRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(MinuteChequeDocumentDto payload)
        => Document.Create(doc =>
        {
            doc.Page(page => MinuteChequePdfComposer.ComposeDocument(page, payload));
        }).GeneratePdf();

    public byte[] RenderPreviewPng(MinuteChequeDocumentDto payload, int dpi = 150)
        => Document.Create(doc =>
        {
            doc.Page(page => MinuteChequePdfComposer.ComposeDocument(page, payload));
        }).GenerateImages(new ImageGenerationSettings { RasterDpi = dpi }).First();
}
