using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

public class QuestPdfBilletConversionRenderer : IBilletConversionDocumentRenderer
{
    static QuestPdfBilletConversionRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(BilletConversionDocumentDto payload)
        => Document.Create(doc =>
        {
            doc.Page(page => BilletConversionPdfComposer.ComposeDocument(page, payload));
        }).GeneratePdf();
}
