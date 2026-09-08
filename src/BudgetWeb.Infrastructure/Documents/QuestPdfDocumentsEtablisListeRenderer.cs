using System.Globalization;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

public sealed class QuestPdfDocumentsEtablisListeRenderer : IDocumentsEtablisListePdfRenderer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    static QuestPdfDocumentsEtablisListeRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(DocumentsEtablisQuery query, IReadOnlyList<DocumentEtabliListItemDto> rows)
    {
        var periode =
            $"Du {query.DateDebut.ToString("dd/MM/yyyy", Fr)} au {query.DateFin.ToString("dd/MM/yyyy", Fr)}";
        var typeFiltre = string.IsNullOrWhiteSpace(query.TypeDocument)
            ? "Tous les types"
            : TypeDocumentEtabli.Libelle(query.TypeDocument);
        var reference = $"DOC-ETABLIS-{query.DateDebut:yyyyMMdd}-{query.DateFin:yyyyMMdd}";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken3));
                SnelInstitutionalPdfChrome.ApplyBackgroundWatermark(page);
                page.Header().Column(col =>
                {
                    SnelInstitutionalPdfChrome.ComposeHeader(col.Item(), DateTime.Now);
                    col.Item().PaddingTop(8).AlignCenter()
                        .Text("Liste des documents établis").Bold().FontSize(14).FontColor(Colors.Blue.Darken3);
                    col.Item().AlignCenter().Text($"{periode} — {typeFiltre} — {rows.Count} document(s)")
                        .FontSize(9);
                });
                page.Footer().Element(f => SnelInstitutionalPdfChrome.ComposeFooter(f, reference));

                page.Content().PaddingTop(8).Column(col =>
                {
                    if (rows.Count == 0)
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("Aucun document établi sur cette période.").Italic();
                        return;
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.3f);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(1.1f);
                            c.RelativeColumn(0.9f);
                            c.RelativeColumn(0.9f);
                            c.RelativeColumn(1.1f);
                            c.RelativeColumn(1.6f);
                            c.RelativeColumn(1.2f);
                        });

                        table.Header(h =>
                        {
                            static void Cell(IContainer c, string text)
                                => c.Background(Colors.Blue.Darken3).Padding(4)
                                    .Text(text).FontColor(Colors.White).SemiBold().FontSize(7.5f);

                            Cell(h.Cell(), "Référence");
                            Cell(h.Cell(), "Document");
                            Cell(h.Cell(), "N°");
                            Cell(h.Cell(), "Date établ.");
                            Cell(h.Cell(), "Date doc.");
                            Cell(h.Cell(), "Montant");
                            Cell(h.Cell(), "Bénéficiaire");
                            Cell(h.Cell(), "Établi par");
                        });

                        foreach (var row in rows)
                        {
                            BodyCell(table, row.Reference);
                            BodyCell(table, row.LibelleTypeDocument);
                            BodyCell(table, string.IsNullOrWhiteSpace(row.NumeroDocument) ? "—" : row.NumeroDocument);
                            BodyCell(table, row.DateEtabli.ToString("dd/MM/yyyy HH:mm", Fr));
                            BodyCell(table, row.DateDocument.ToString("dd/MM/yyyy", Fr));
                            BodyCell(table, $"{row.Montant.ToString("N2", Fr)} {row.Devise}");
                            BodyCell(table, string.IsNullOrWhiteSpace(row.BeneficiaireAffichage) ? "—" : row.BeneficiaireAffichage);
                            BodyCell(table, string.IsNullOrWhiteSpace(row.NomUtilisateurEtabli) ? "—" : row.NomUtilisateurEtabli);
                        }
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void BodyCell(TableDescriptor table, string text)
        => table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).Padding(3)
            .Text(text).FontSize(7.5f);
}
