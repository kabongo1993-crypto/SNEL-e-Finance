using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

public sealed class QuestPdfFicheImputationBudgetaireRenderer : IFicheImputationBudgetaireRenderer
{
    static QuestPdfFicheImputationBudgetaireRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(FicheImputationBudgetaireDto payload)
        => Document.Create(doc =>
        {
            doc.Page(page => FicheImputationBudgetairePdfComposer.ComposeDocument(page, payload));
        }).GeneratePdf();
}
