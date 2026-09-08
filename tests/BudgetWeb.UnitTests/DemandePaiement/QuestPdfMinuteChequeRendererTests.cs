using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Infrastructure.Documents;
using QuestPDF.Helpers;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class QuestPdfMinuteChequeRendererTests
{
    private static readonly QuestPdfMinuteChequeRenderer Renderer = new();

    [Fact]
    public void Render_MinuteCheque_ProduitPdfValide_UnePage_A4Portrait()
    {
        var pdf = Renderer.Render(SampleDocument());
        AssertPdfValide(pdf, minLength: 8_000);
        AssertSinglePage(pdf);
        AssertA4PortraitMediaBox(pdf);
    }

    [Fact]
    public void Render_MinuteCheque_MotifLong_BeneficiaireLong_UnePage()
    {
        var pdf = Renderer.Render(SampleDocument() with
        {
            Motif =
                "FRAIS DE PARTICIPATION AU SEMINAIRE INTERNATIONAL SUR LA GESTION DES RESSOURCES HUMAINES ET LE DEVELOPPEMENT DURABLE A RABAT MAROC POUR LE COMPTE DE LA DIRECTION DES FINANCES",
            BeneficiaireAffichage =
                "SOCIETE CONGOLESE DE CONSTRUCTION ET D'INGENIERIE INDUSTRIELLE SARL",
            BeneficiaireAdresse =
                "1234 AVENUE DES MARTYRS DE LA INDEPENDANCE NATIONALE, COMMUNE DE LA GOMBE, KINSHASA, REPUBLIQUE DEMOCRATIQUE DU CONGO",
            MontantEnLettres =
                "CENT VINGT-TROIS MILLIONS QUATRE CENT CINQUANTE-SIX MILLE SEPT CENT QUATRE-VINGT-NEUF FRANCS CONGOLAIS",
        });

        AssertPdfValide(pdf);
        AssertSinglePage(pdf);
    }

    [Fact]
    public void Render_MinuteCheque_ChampsOptionnelsVides_SansPlaceholder()
    {
        var pdf = Renderer.Render(SampleDocument() with
        {
            BeneficiaireAdresse = null,
            BeneficiaireBanque = null,
            BeneficiaireNumeroCompte = null,
            CompteGeneral = null,
            CpCa = null,
            Ls = null,
            SuiviExtraComptable = null,
            NumeroAppariement = null,
            MontantSuiviExtraComptable = null,
            EtabliPar = null,
        });

        AssertPdfValide(pdf);
        AssertSinglePage(pdf);

        var text = ExtractPdfText(pdf);
        Assert.DoesNotContain("N/A", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\u2014", text);
    }

    private static MinuteChequeDocumentDto SampleDocument()
        => new(
            TitreDocument: "MINUTE DE CHÈQUE",
            NumeroOp: "OP-2026-00042",
            DateDocument: new DateOnly(2026, 6, 19),
            ReferenceDemande: "DP-2026-00018",
            Motif: "REGLEMENT FACTURE FOURNITURE MATERIEL ELECTRIQUE POUR ENTRETIEN RESEAU",
            BeneficiaireAffichage: "ELECTRO-SERVICES KINSHASA SPRL",
            BeneficiaireAdresse: "45 AVENUE DU COMMERCE, LIMETE, KINSHASA",
            BeneficiaireBanque: "RAWBANK",
            BeneficiaireNumeroCompte: "001-0951851-03",
            MontantPaiement: 12_345_678.50m,
            DevisePaiement: "CDF",
            MontantEnLettres: "DOUZE MILLIONS TROIS CENT QUARANTE-CINQ MILLE SIX CENT SOIXANTE-DIX-HUIT FRANCS CONGOLAIS ET CINQUANTE CENTIMES",
            CompteGeneral: "47110000",
            CpCa: "PA",
            Ls: "L",
            SuiviExtraComptable: "SEC001234567",
            NumeroAppariement: "5D20022",
            MontantSuiviExtraComptable: 12_345_678.50m,
            IdentifiantVerification: "OP-2026-00042-1",
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

    private static void AssertA4PortraitMediaBox(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        var match = Regex.Match(raw, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
        Assert.True(match.Success, "MediaBox introuvable.");

        const float ptPerMm = 72f / 25.4f;
        var width = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var height = float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

        Assert.Equal(210f, width / ptPerMm, precision: 0);
        Assert.Equal(297f, height / ptPerMm, precision: 0);
        Assert.True(height > width, "A4 portrait : hauteur > largeur.");
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
