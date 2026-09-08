using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Documents;
using QuestPDF.Helpers;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class QuestPdfFicheImputationRendererTests
{
    private static readonly QuestPdfFicheImputationBudgetaireRenderer Renderer = new();

    [Fact]
    public void Render_FicheImputation_Definitive_ProduitPdfValide_UnePage_A5Paysage()
    {
        var pdf = Renderer.Render(SampleDefinitive());
        AssertPdfValide(pdf, minLength: 4_000);
        AssertSinglePage(pdf);
        AssertA5LandscapeMediaBox(pdf);

        var outDir = Path.Combine(FindRepoRoot(), "artifacts");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "fiche-imputation-a5-test.pdf");
        File.WriteAllBytes(outPath, pdf);
    }

    [Fact]
    public void Render_FicheImputation_Travail_ProduitPdfValide_UnePage_A5Paysage()
    {
        var pdf = Renderer.Render(SampleTravail());
        AssertPdfValide(pdf);
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

    private static FicheImputationBudgetaireDto SampleDefinitive()
    {
        var lignes = new[]
        {
            new FicheImputationLigneDto(
                1, "A00300", "1", "00100",
                10_000m, 2_400m, 7_600m, 2_600m,
                120_000m, 28_800m, 91_200m, 28_800m),
            new FicheImputationLigneDto(
                2, "A00300", "2", "00122",
                5_000m, 600m, 4_400m, 600m,
                60_000m, 7_200m, 52_800m, 7_200m),
        };

        var totaux = FicheImputationBudgetaireBuilder.CalculerTotaux(
            FicheImputationMode.Definitive,
            lignes,
            12_000m);

        return new FicheImputationBudgetaireDto(
            FicheImputationMode.Definitive,
            IdDemandePaiement: 442,
            Reference: "DP-442",
            AnneeExercice: 2026,
            CodeTypeDepenses: "DC",
            DateEngagement: new DateTime(2026, 9, 6),
            DateGeneration: new DateTime(2026, 9, 6, 12, 0, 0),
            Lignes: lignes,
            Totaux: totaux,
            GestionnaireJunior: new("Jean Junior", "GESTIONNAIRE JUNIOR", new DateTime(2026, 9, 5)),
            GestionnaireSenior: new("Marie Senior", "GESTIONNAIRE SENIOR", new DateTime(2026, 9, 5)),
            ChefDivision: new("Paul Chef", "CHEF DE DIVISION", new DateTime(2026, 9, 6)));
    }

    private static FicheImputationBudgetaireDto SampleTravail()
    {
        var lignes = new[]
        {
            new FicheImputationLigneDto(
                1, "A00300", "1", "00100",
                null, null, 7_600m, null,
                null, null, 7_600m, null),
        };

        var totaux = FicheImputationBudgetaireBuilder.CalculerTotaux(
            FicheImputationMode.Travail,
            lignes,
            7_600m);

        return new FicheImputationBudgetaireDto(
            FicheImputationMode.Travail,
            IdDemandePaiement: 442,
            Reference: "DP-442",
            AnneeExercice: 2026,
            CodeTypeDepenses: "DC",
            DateEngagement: null,
            DateGeneration: new DateTime(2026, 9, 6, 12, 0, 0),
            Lignes: lignes,
            Totaux: totaux,
            GestionnaireJunior: new("Jean Junior", "GESTIONNAIRE JUNIOR", null),
            GestionnaireSenior: new(null, "GESTIONNAIRE SENIOR", null),
            ChefDivision: new(null, "CHEF DE DIVISION", null));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "BudgetWeb.sln")) || Directory.Exists(Path.Combine(dir, "src")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return AppContext.BaseDirectory;
    }

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
}
