using System.Globalization;
using BudgetWeb.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents.Experiments;

/// <summary>
/// Variante expérimentale du composer DPM — pagination simplifiée et/ou images optimisées.
/// Ne remplace pas <see cref="DemandePaiementPdfComposer"/> en production.
/// </summary>
internal static class DpmPdfExperimentComposer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly string AccentBlue = Colors.Blue.Darken3;
    private static readonly string AccentLine = Colors.Blue.Darken2;
    private static readonly string Muted = Colors.Grey.Darken2;
    private static readonly string Border = Colors.Grey.Lighten2;

    public static void ApplyPageChrome(
        PageDescriptor page,
        DemandePaiementDocumentDto payload,
        DpmPdfExperimentFlags flags)
    {
        page.Size(PageSizes.A4);
        page.MarginLeft(32);
        page.MarginRight(32);
        page.MarginTop(22);
        page.MarginBottom(38);
        page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Colors.Black));

        if (flags.HasFlag(DpmPdfExperimentFlags.OptimizedImages))
            DpmPdfExperimentChrome.ApplyDiscreteBackgroundWatermark(page);
        else
            SnelInstitutionalPdfChrome.ApplyDiscreteBackgroundWatermark(page);

        page.Header().Element(h => ComposeHeader(h, payload, flags));
        page.Footer().Element(f => ComposeFooter(f, payload, flags));
    }

    private static void ComposeHeader(
        IContainer container,
        DemandePaiementDocumentDto p,
        DpmPdfExperimentFlags flags)
    {
        var logo = flags.HasFlag(DpmPdfExperimentFlags.OptimizedImages)
            ? DpmPdfExperimentChrome.Logo
            : SnelInstitutionalPdfChrome.Logo;

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(46).Element(e =>
                {
                    if (logo is { Length: > 0 })
                        e.Height(38).Image(logo).FitArea();
                });
                row.RelativeItem().PaddingLeft(6).AlignMiddle().Column(c =>
                {
                    c.Item().Text("SNEL").Bold().FontSize(11).FontColor(AccentBlue);
                    c.Item().Text("Société Nationale d'Électricité").SemiBold().FontSize(8).FontColor(AccentBlue);
                    c.Item().Text("e-Finance").FontSize(7.5f).FontColor(Muted);
                });
                row.ConstantItem(118).AlignRight().AlignMiddle().Column(c =>
                {
                    c.Item().Text("Date d'émission").FontSize(7).FontColor(Muted);
                    c.Item().Text(p.DateEmission.ToString("dd/MM/yyyy", Fr)).Bold().FontSize(8.5f);
                    c.Item().PaddingTop(3).Text("Référence").FontSize(7).FontColor(Muted);
                    c.Item().Text(p.Reference).SemiBold().FontSize(8.5f);
                });
            });

            col.Item().PaddingTop(5).LineHorizontal(1f).LineColor(AccentLine);
            col.Item().PaddingTop(4).AlignCenter().Text("DEMANDE DE PAIEMENT").Bold().FontSize(12).FontColor(AccentBlue);
            col.Item().PaddingTop(2).AlignCenter().Text($"Référence : {p.Reference}").SemiBold().FontSize(9);
            col.Item().PaddingTop(3).AlignCenter().Text($"STATUT : {p.StatutLibelle.ToUpperInvariant()}").Bold().FontSize(8.5f);
            if (p.DocumentSignePhysiquePresent)
            {
                col.Item().PaddingTop(1).AlignCenter()
                    .Text("Document signé physique joint au dossier").FontSize(7).Italic().FontColor(Muted);
            }
            col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Border);
        });
    }

    private static void ComposeFooter(
        IContainer container,
        DemandePaiementDocumentDto p,
        DpmPdfExperimentFlags flags)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.6f).LineColor(AccentLine);
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        t.Span("Réf. ").FontSize(6.5f);
                        t.Span(p.Reference).FontSize(6.5f).SemiBold();
                    });
                    c.Item().Text("e-Finance — Société Nationale d'Électricité").FontSize(6.5f).FontColor(Muted);
                    c.Item().Text($"Document généré le {p.DateImpression.ToString("dd/MM/yyyy HH:mm", Fr)}")
                        .FontSize(6.5f).FontColor(Muted);
                });
                row.ConstantItem(120).AlignRight().Column(c =>
                {
                    if (!string.IsNullOrWhiteSpace(p.IdentifiantVerification))
                    {
                        c.Item().AlignRight().Text($"Vérification : {p.IdentifiantVerification}")
                            .FontSize(6f).FontColor(Muted);
                    }

                    if (flags.HasFlag(DpmPdfExperimentFlags.SimplifiedPagination))
                    {
                        c.Item().AlignRight().Text("Page 1 / 1").FontSize(6.5f);
                    }
                    else
                    {
                        c.Item().AlignRight().Text(text =>
                        {
                            text.Span("Page ").FontSize(6.5f);
                            text.CurrentPageNumber().FontSize(6.5f);
                            text.Span(" / ").FontSize(6.5f);
                            text.TotalPages().FontSize(6.5f);
                        });
                    }
                });
            });
        });
    }
}
