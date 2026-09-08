using System.Text;
using System.Text.RegularExpressions;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Documents;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class QuestPdfDemandePaiementRendererTests
{
    private static readonly QuestPdfDemandePaiementRenderer Renderer = new();

    [Fact]
    public void Render_Brouillon_ProduitPdfValide_UnePage()
    {
        var pdf = Renderer.Render(SampleBrouillon());
        AssertPdfValide(pdf, minLength: 8_000);
        AssertSinglePage(pdf);
        Assert.True(pdf.Length < 140_000, "PDF DPM avec assets optimises (~128 KB attendu)");
    }

    [Fact]
    public void Render_Validation_Electronique_ProduitPdfValide_UnePage()
    {
        var pdf = Renderer.Render(SampleAvecValidations(
            n1: Electronique("Alice N1"),
            n2: null));
        AssertPdfValide(pdf);
        AssertSinglePage(pdf);
    }

    [Fact]
    public void Render_Validation_Physique_ProduitPdfValide_UnePage()
    {
        var pdf = Renderer.Render(SampleAvecValidations(
            n1: Physique("Jean Signataire", "Chef service", "Bob Declarant"),
            n2: null));
        AssertPdfValide(pdf);
        AssertSinglePage(pdf);
    }

    [Fact]
    public void Render_Mode_Mixte_N1_Electronique_N2_Physique_ProduitPdfValide_UnePage()
    {
        var pdf = Renderer.Render(SampleAvecValidations(
            n1: Electronique("Resp N1"),
            n2: Physique("Resp N2 Sign", "Directeur", "Agent Declarant", ValidationEntiteNiveau.N2)));
        AssertPdfValide(pdf);
        AssertSinglePage(pdf);
    }

    [Fact]
    public void Render_Soumise_ProduitPdfValide_UnePage()
    {
        var pdf = Renderer.Render(SampleBrouillon() with
        {
            Statut = StatutDemandePaiement.Soumise,
            StatutLibelle = DemandePaiementStatutLabels.Libelle(StatutDemandePaiement.Soumise),
            DateSoumission = new DateTime(2026, 8, 27, 14, 30, 0),
        });
        AssertPdfValide(pdf);
        AssertSinglePage(pdf);
    }

    [Fact]
    public void Render_Pieces_Jointes_Ne_Sont_Plus_Imprimees()
    {
        var dto = SampleBrouillon() with
        {
            PiecesJustificatives =
            [
                new DemandePaiementPieceDocumentDto("Facture proforma", true, true, "facture.pdf"),
                new DemandePaiementPieceDocumentDto("Bon de commande", true, false, null),
            ],
        };
        Assert.Equal(2, dto.PiecesJustificatives.Count);

        var pdf = Renderer.Render(dto);
        AssertPdfValide(pdf);
        AssertSinglePage(pdf);
        Assert.DoesNotContain("PIÈCES JUSTIFICATIVES", ExtractPdfText(pdf), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_Multiples_Beneficiaires_ProduitPdfValide_UnePage()
    {
        var pdf = Renderer.Render(SampleBrouillon() with
        {
            Beneficiaires =
            [
                Beneficiaire("Jean Beneficiaire", principal: true, ordre: 1),
                Beneficiaire("SARL Fournisseur", type: "TIERS", ordre: 2, rccm: "CD/KIN/RCCM/123"),
            ],
        });
        AssertPdfValide(pdf);
        AssertSinglePage(pdf);
    }

    [Fact]
    public void Render_Objet_Long_ProduitPdfValide()
    {
        var objetLong = string.Join(" ", Enumerable.Repeat(
            "Objet détaillé de la demande de paiement pour couvrir les frais de mission et fournitures.",
            8));
        var pdf = Renderer.Render(SampleBrouillon() with { Objet = objetLong });
        AssertPdfValide(pdf);
        Assert.True(pdf.Length > 10_000);
    }

    [Fact]
    public void Render_DocumentDto_Conserve_Donnees_Metier()
    {
        var dto = SampleBrouillon();
        Assert.Equal("DP-2026-00025", dto.Reference);
        Assert.Equal(12_000m, dto.MontantBrut);
        Assert.Single(dto.Beneficiaires);
        Assert.Equal("Circuit test PDF officiel e-Finance", dto.Objet);
        Assert.Equal("Brouillon", dto.StatutLibelle);
    }

    private static DemandePaiementDocumentDto SampleBrouillon()
        => new(
            IdDemandePaiement: 29,
            Reference: "DP-2026-00025",
            DateEmission: new DateOnly(2026, 8, 27),
            LieuEmission: "Kinshasa",
            Objet: "Circuit test PDF officiel e-Finance",
            MontantBrut: 12_000m,
            Devise: "USD",
            DestinationSolliciteeAffichage: "DC",
            TypeBudgetSollicite: TypeBudgetCode.DepensesCourantes,
            ItemSollicite: null,
            ModePaiementSollicite: ModePaiementDpm.Caisse,
            CompteSection: "62.01",
            Statut: StatutDemandePaiement.Brouillon,
            StatutLibelle: DemandePaiementStatutLabels.Libelle(StatutDemandePaiement.Brouillon),
            LibelleDemandeur: "DAM/APPRO",
            LibelleCasDossier: "Fournitures courantes",
            DateSoumission: null,
            DateImpression: new DateTime(2026, 8, 27, 20, 0, 0),
            IdentifiantVerification: "DP-2026-00025 · DPM-000029",
            Beneficiaires: [Beneficiaire("Jean Beneficiaire")],
            ValidationsEntite: [],
            PiecesJustificatives:
            [
                new DemandePaiementPieceDocumentDto("Facture proforma", true, true, "facture.pdf"),
            ],
            DocumentSignePhysiquePresent: false);

    private static DemandePaiementDocumentDto SampleAvecValidations(
        ValidationEntiteDto? n1,
        ValidationEntiteDto? n2)
    {
        var list = new List<ValidationEntiteDto>();
        if (n1 is not null) list.Add(n1);
        if (n2 is not null) list.Add(n2);
        return SampleBrouillon() with
        {
            Statut = StatutDemandePaiement.EnValidationN2,
            StatutLibelle = DemandePaiementStatutLabels.Libelle(StatutDemandePaiement.EnValidationN2),
            ValidationsEntite = list,
        };
    }

    private static ValidationEntiteDto Electronique(string nom, byte niveau = ValidationEntiteNiveau.N1)
        => new(
            niveau,
            niveau,
            StatutValidationEntite.Validee,
            ModeValidationEntite.Electronique,
            10,
            nom,
            null,
            null,
            null,
            null,
            null,
            new DateTime(2026, 8, 27, 9, 15, 0),
            null);

    private static ValidationEntiteDto Physique(
        string signataire,
        string fonction,
        string declarant,
        byte niveau = ValidationEntiteNiveau.N1)
        => new(
            niveau,
            niveau,
            StatutValidationEntite.Validee,
            ModeValidationEntite.Physique,
            null,
            null,
            20,
            declarant,
            signataire,
            fonction,
            new DateOnly(2026, 8, 26),
            new DateTime(2026, 8, 27, 10, 0, 0),
            null);

    private static BeneficiaireDto Beneficiaire(
        string nom,
        string type = "AGENT",
        bool principal = false,
        int ordre = 1,
        string? rccm = null)
        => new(1, type, nom, "M-001", "Comptable", null, rccm, "Kinshasa", "Rawbank", "00123456789", principal, ordre);

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
        var matches = Regex.Matches(raw, @"/Type\s*/Page\b");
        return matches.Count;
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
