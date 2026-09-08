using System.Globalization;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

/// <summary>Document officiel PDF — Demande de Paiement e-Finance.</summary>
public class QuestPdfDemandePaiementRenderer : IDemandePaiementDocumentRenderer
{
    static QuestPdfDemandePaiementRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(DemandePaiementDocumentDto payload)
        => Document.Create(doc =>
        {
            doc.Page(page =>
            {
                DemandePaiementPdfComposer.ApplyPageChrome(page, payload);
                page.Content().PaddingTop(8).Element(c => DemandePaiementPdfComposer.ComposeContent(c, payload));
            });
        }).GeneratePdf();
}
