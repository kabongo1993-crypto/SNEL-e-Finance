using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Infrastructure.Documents;
using QuestPDF.Helpers;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class QuestPdfBonProvisoireRendererTests
{
    private static readonly QuestPdfBonProvisoireRenderer Renderer = new();

    [Fact]
    public void Render_BonProvisoire_ProduitPdfValide_UnePage_A5Paysage()
    {
        var pdf = Renderer.Render(SampleDocument());
        AssertPdfValide(pdf, minLength: 4_000);
        AssertSinglePage(pdf);
        AssertA5LandscapeMediaBox(pdf);
    }

    [Fact]
    public void Render_BonProvisoire_ChampsOptionnelsVides_SansPlaceholder()
    {
        var pdf = Renderer.Render(SampleDocument() with
        {
            MentionJustificationRetrait = null,
            BeneficiaireIdentite = null,
            DirectionBeneficiaire = null,
        });

        AssertPdfValide(pdf);
        AssertSinglePage(pdf);

        var text = ExtractPdfText(pdf);
        Assert.DoesNotContain("N/A", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\u2014", text);
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

    private static BonProvisoireDocumentDto SampleDocument()
        => new(
            TitreDocument: "BON PROVISOIRE",
            NumeroBon: "346-2026",
            DateBon: new DateOnly(2026, 6, 25),
            ReferenceDemande: "R343",
            Motif: "COLLATION DES AGENTS DE LA TRESORERIE SNEL DEUXIEMEMENT DIVISION DE FOOTBALL",
            MentionJustificationRetrait: null,
            BeneficiaireAffichage: "MONICA",
            BeneficiaireMatricule: "9038",
            BeneficiaireIdentite: null,
            DirectionBeneficiaire: null,
            MontantFc: 27_480_000m,
            MontantEnLettres:
                "FRANCS CONGOLAIS VINGT-SEPT MILLIONS QUATRE CENT QUATRE-VINGT MILLE",
            RecuCaisseCentrale: "RECU DE LA CAISSE CENTRALE SNEL SOMME DE FC",
            CompteGeneral: "47110000000",
            CompteParticulier: "P",
            NumeroAppariement: "5D20022",
            IdentifiantVerification: "BP-346-2026-1",
            EtabliPar: "Charge DP Test",
            DateImpression: new DateTime(2026, 6, 25, 15, 0, 0));

    private static void AssertPdfValide(byte[] pdf, int minLength = 2_000)
    {
        Assert.True(pdf.Length > minLength);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static void AssertSinglePage(byte[] pdf)
    {
        var pageCount = CountPdfPages(pdf);
        Assert.Equal(1, pageCount);
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
        var widthMm = widthPt / ptPerMm;
        var heightMm = heightPt / ptPerMm;

        Assert.Equal(210f, widthMm, precision: 0);
        Assert.Equal(148f, heightMm, precision: 0);
        Assert.True(widthPt > heightPt, "Le PDF doit être en orientation paysage.");
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
