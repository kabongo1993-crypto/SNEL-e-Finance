using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Infrastructure.Documents;
using QuestPDF.Helpers;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class QuestPdfPieceCaisseRendererTests
{
    private static readonly QuestPdfPieceCaisseRenderer Renderer = new();

    [Fact]
    public void Render_PieceCaisse_ProduitPdfValide_UnePage_A5Paysage()
    {
        var pdf = Renderer.Render(SampleDocument());
        AssertPdfValide(pdf, minLength: 8_000);
        AssertSinglePage(pdf);
        AssertA5LandscapeMediaBox(pdf);
    }

    [Fact]
    public void PageSizes_A5Landscape_CorrespondAuxDimensionsImposees()
    {
        var size = PageSizes.A5.Landscape();

        const float ptPerMm = 72f / 25.4f;
        Assert.Equal(210f, size.Width / ptPerMm, precision: 0);
        Assert.Equal(148f, size.Height / ptPerMm, precision: 0);
        Assert.True(size.Width > size.Height, "A5 paysage : largeur > hauteur.");
    }

    [Fact]
    public void Render_PieceCaisse_ChampsOptionnelsVides_SansPlaceholder()
    {
        var pdf = Renderer.Render(SampleDocument() with
        {
            PieceJustificative = null,
            BeneficiaireIdentite = null,
            Sr = null,
            Cpa = null,
        });

        AssertPdfValide(pdf);
        AssertSinglePage(pdf);

        var text = ExtractPdfText(pdf);
        Assert.DoesNotContain("N/A", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\u2014", text);
    }

    private static PieceCaisseDocumentDto SampleDocument()
        => new(
            TitreDocument: "PIÈCE DE CAISSE",
            NumeroPiece: "PC-4122-2026",
            DatePiece: new DateOnly(2026, 6, 19),
            ReferenceDemande: "R22",
            Motif:
                "FRAIS DE PARTICIPATION AU SEMINAIRE INTERNATIONAL SUR LA GESTION DES RESSOURCES HUMAINES A RABAT MAROC",
            PieceJustificative: "1956IP3164/025",
            BeneficiaireAffichage: "NESTORD",
            BeneficiaireMatricule: "38H38",
            BeneficiaireIdentite: null,
            MontantFc: 4_122_000m,
            MontantEnLettres: "QUATRE MILLIONS CENT VINGT-DEUX MILLE",
            RecuSnel: "RECU DE S.N.E.L",
            Sr: "78",
            ComptabiliteGenerale: "47110000",
            Cp: "P",
            Cpa: "A",
            NumeroAppariement: "5D20022",
            IdentifiantVerification: "PC-4122-2026-1",
            EtabliPar: "Charge DP Test",
            DateImpression: new DateTime(2026, 6, 19, 15, 0, 0));

    private static void AssertPdfValide(byte[] pdf, int minLength = 2_000)
    {
        Assert.True(pdf.Length > minLength);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static void AssertSinglePage(byte[] pdf)
    {
        Assert.Equal(1, CountPdfPages(pdf));
    }

    private static int CountPdfPages(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        var countMatch = Regex.Match(raw, @"/Type\s*/Pages[\s\S]{0,400}?/Count\s+(\d+)");
        if (countMatch.Success)
            return int.Parse(countMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        return Regex.Matches(raw, @"/Type\s*/Page\b").Count;
    }

    private static void AssertA5LandscapeMediaBox(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        var match = Regex.Match(raw, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
        Assert.True(match.Success, "MediaBox introuvable dans le PDF.");

        var widthPt = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var heightPt = float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

        const float ptPerMm = 72f / 25.4f;
        Assert.Equal(210f, widthPt / ptPerMm, precision: 0);
        Assert.Equal(148f, heightPt / ptPerMm, precision: 0);
        Assert.True(widthPt > heightPt);
    }

    private static string ExtractPdfText(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        var chunks = Regex.Matches(raw, @"\(([^\\)]*)\)");
        var sb = new StringBuilder();
        foreach (Match m in chunks)
            sb.Append(m.Groups[1].Value);
        return sb.ToString();
    }
}
