using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Documents;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class QuestPdfDocumentRendererTests
{
    [Fact]
    public void Render_ProduitPdfValideAvecMontantsUsd()
    {
        var payload = new DocumentPrevisionPayloadDto(
            DocumentPrevisionType.Titre(DocumentPrevisionType.Soumission),
            "SNEL/DSI/BUD/2026/V01/SUB/00001",
            DocumentPrevisionType.Soumission,
            DocumentPrevisionPortee.Ub,
            1,
            2026,
            10,
            1,
            "Initiale",
            5,
            "DSI",
            "Direction SI",
            100,
            "UB-100",
            "Unité 100",
            DateTime.Now,
            42,
            "Alice Dupont",
            "BROUILLON",
            "SOUMISE",
            null,
            null,
            1000.5m,
            200m,
            50m,
            1250.5m,
            [new DocumentPrevisionUbLigneDto(100, "UB-100", "Unité 100", 1000.5m, 200m, 50m, 1250.5m)]);

        var pdf = new QuestPdfDocumentRenderer().Render(payload);

        Assert.True(pdf.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
