using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents.Experiments;

/// <summary>
/// Renderer PDF DPM expérimental — sélection de variante via constructeur (tests uniquement).
/// </summary>
public sealed class QuestPdfDemandePaiementExperimentRenderer : IDemandePaiementDocumentRenderer
{
    private readonly DpmPdfExperimentVariant _variant;

    static QuestPdfDemandePaiementExperimentRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public QuestPdfDemandePaiementExperimentRenderer(DpmPdfExperimentVariant variant)
    {
        _variant = variant;
    }

    public byte[] Render(DemandePaiementDocumentDto payload)
    {
        var flags = _variant.ToFlags();
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                DpmPdfExperimentComposer.ApplyPageChrome(page, payload, flags);
                page.Content().PaddingTop(8).Element(c => DemandePaiementPdfComposer.ComposeContent(c, payload));
            });
        }).GeneratePdf();
    }
}
