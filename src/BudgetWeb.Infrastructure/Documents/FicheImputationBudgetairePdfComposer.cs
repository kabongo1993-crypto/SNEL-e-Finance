using System.Globalization;
using BudgetWeb.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

/// <summary>Mise en page A5 paysage — Fiche d'Imputation Budgétaire SNEL.</summary>
internal static class FicheImputationBudgetairePdfComposer
{
    /// <summary>210 mm (L) × 148 mm (H).</summary>
    public static readonly PageSize PageSizeA5Landscape = PageSizes.A5.Landscape();

    private const float Mm = 72f / 25.4f;
    private static float MmPt(float mm) => mm * Mm;

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly string AccentBlue = Colors.Blue.Darken4;
    private static readonly string HeaderBg = Colors.Blue.Darken3;
    private static readonly string GroupBg = Colors.Blue.Lighten4;
    private static readonly string SubHeaderBg = Colors.Blue.Lighten3;
    private static readonly string TotalBg = Colors.Blue.Lighten5;
    private static readonly string Border = Colors.Blue.Darken2;

    private const float DataFontSize = 7f;
    private const float HeaderFontSize = 6.8f;
    private const float GroupFontSize = 7.2f;

    public static void ComposeDocument(PageDescriptor page, FicheImputationBudgetaireDto fiche)
    {
        var definitive = fiche.Mode == FicheImputationMode.Definitive;

        page.Size(PageSizeA5Landscape);
        page.MarginLeft(MmPt(4));
        page.MarginRight(MmPt(4));
        page.MarginTop(MmPt(3));
        page.MarginBottom(MmPt(26));
        page.DefaultTextStyle(x => x.FontSize(DataFontSize).FontColor(Colors.Black));

        ApplyCenteredWatermark(page);
        page.Header().Element(ComposeInstitutionalHeader);
        page.Footer().Element(f =>
            SnelInstitutionalPdfChrome.ComposeFooter(
                f,
                $"FIB-{fiche.IdDemandePaiement}-{fiche.Mode.ToString().ToUpperInvariant()}"));

        page.Content().Column(col =>
        {
            col.Spacing(MmPt(2));
            col.Item().Element(c => ComposeTitleBlock(c, fiche));
            col.Item().Element(c => ComposeTable(c, fiche, definitive));
            col.Item().PaddingTop(MmPt(2.5f)).Element(c => ComposeSignatures(c, fiche));
        });
    }

    private static void ApplyCenteredWatermark(PageDescriptor page)
    {
        var wm = SnelInstitutionalPdfChrome.Watermark;
        if (wm is not { Length: > 0 })
            return;

        page.Background()
            .AlignCenter()
            .AlignMiddle()
            .Width(MmPt(82))
            .Height(MmPt(82))
            .Image(wm)
            .FitArea();
    }

    private static void ComposeInstitutionalHeader(IContainer container)
    {
        var logo = SnelInstitutionalPdfChrome.Logo;
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(MmPt(14)).Element(e =>
                {
                    if (logo is { Length: > 0 })
                        e.Height(MmPt(12)).Image(logo).FitArea();
                });
                row.RelativeItem().PaddingLeft(MmPt(2)).AlignMiddle().Column(c =>
                {
                    c.Item().Text("SOCIETE NATIONALE DE L'ELECTRICITE SA").Bold().FontSize(8.5f).FontColor(AccentBlue);
                    c.Item().Text("DIRECTION DES FINANCES").SemiBold().FontSize(7.5f).FontColor(AccentBlue);
                    c.Item().Text("DIRECTION DES COMPTABILITES").SemiBold().FontSize(7.5f).FontColor(AccentBlue);
                });
            });
            col.Item().PaddingTop(MmPt(0.8f)).LineHorizontal(0.8f).LineColor(Border);
        });
    }

    private static void ComposeTitleBlock(IContainer container, FicheImputationBudgetaireDto fiche)
    {
        container.Column(col =>
        {
            col.Item().Background(HeaderBg).PaddingVertical(MmPt(1.8f)).AlignCenter()
                .Text("FICHE D'IMPUTATION BUDGETAIRE")
                .Bold().FontSize(10f).FontColor(Colors.White);

            col.Item().PaddingTop(MmPt(1.8f)).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        t.Span("EXERCICE BUDGETAIRE : ").SemiBold();
                        t.Span(fiche.AnneeExercice.ToString(Fr));
                    });
                    c.Item().PaddingTop(MmPt(0.8f)).Text(t =>
                    {
                        t.Span("DATE D'ENGAGEMENT : ").SemiBold();
                        t.Span(FormaterDate(fiche.DateEngagement));
                    });
                });
                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        t.Span("DOSSIER N° : ").SemiBold();
                        t.Span(fiche.IdDemandePaiement.ToString(Fr));
                    });
                    c.Item().PaddingTop(MmPt(0.8f)).Text(t =>
                    {
                        t.Span("TYPE DE DEPENSES (*) : ").SemiBold();
                        t.Span(fiche.CodeTypeDepenses ?? "—");
                    });
                });
            });

            if (fiche.Mode == FicheImputationMode.Travail)
            {
                col.Item().PaddingTop(MmPt(0.8f)).Text("Document de travail — engagements en cours (non définitifs)")
                    .Italic().FontSize(6.5f).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static void ComposeTable(IContainer container, FicheImputationBudgetaireDto fiche, bool definitive)
    {
        container.Table(table =>
        {
            if (definitive)
            {
                DefinirColonnesDefinitive(table);
                ComposeEntetesDefinitive(table);
            }
            else
            {
                DefinirColonnesTravail(table);
                ComposeEntetesTravail(table);
            }

            foreach (var ligne in fiche.Lignes)
            {
                if (definitive)
                    ComposeLigneDefinitive(table, ligne);
                else
                    ComposeLigneTravail(table, ligne);
            }

            if (definitive)
                ComposeTotalDefinitive(table, fiche.Totaux);
            else
                ComposeTotalTravail(table, fiche.Totaux);
        });
    }

    private static void DefinirColonnesTravail(TableDescriptor table)
    {
        table.ColumnsDefinition(c =>
        {
            c.ConstantColumn(MmPt(7));
            c.RelativeColumn(1.15f);
            c.RelativeColumn(1f);
            c.RelativeColumn(2.4f);
            c.RelativeColumn(1.35f);
            c.RelativeColumn(1.35f);
        });
    }

    private static void DefinirColonnesDefinitive(TableDescriptor table)
    {
        table.ColumnsDefinition(c =>
        {
            c.ConstantColumn(MmPt(7));
            c.RelativeColumn(1.1f);
            c.RelativeColumn(0.95f);
            c.RelativeColumn(2.1f);
            c.RelativeColumn(1f);
            c.RelativeColumn(1f);
            c.RelativeColumn(1f);
            c.RelativeColumn(1f);
            c.RelativeColumn(1f);
            c.RelativeColumn(1f);
            c.RelativeColumn(1f);
            c.RelativeColumn(1f);
        });
    }

    private static void ComposeEntetesTravail(TableDescriptor table)
    {
        Entete(table, "ITEM", 2, 1);
        Entete(table, "UB", 2, 1);
        Entete(table, "N° SUIVI\nBGTAIRE", 2, 1);
        Entete(table, "RUBRIQUE\nBUDGETAIRE", 2, 1);
        EnteteGroup(table, "ENVELOPPE MENSUELLE EN USD", 1);
        EnteteGroup(table, "ENVELOPPE ANNUELLE EN USD", 1);
        Entete(table, "ENGT EN\nCOURS (4)", 1, 1, alignRight: true);
        Entete(table, "ENGT EN\nCOURS (8)", 1, 1, alignRight: true);
    }

    private static void ComposeEntetesDefinitive(TableDescriptor table)
    {
        Entete(table, "ITEM", 2, 1);
        Entete(table, "UB", 2, 1);
        Entete(table, "N° SUIVI\nBGTAIRE", 2, 1);
        Entete(table, "RUBRIQUE\nBUDGETAIRE", 2, 1);
        EnteteGroup(table, "ENVELOPPE MENSUELLE EN USD", 4);
        EnteteGroup(table, "ENVELOPPE ANNUELLE EN USD", 4);

        Entete(table, "BUDGET\n(2)", 1, 1, alignRight: true);
        Entete(table, "CREDIT\nENGAGE (3)", 1, 1, alignRight: true);
        Entete(table, "ENGT EN\nCOURS (4)", 1, 1, alignRight: true);
        Entete(table, "CREDIT\nDISP. (5)", 1, 1, alignRight: true);
        Entete(table, "BUDGET\n(6)", 1, 1, alignRight: true);
        Entete(table, "CREDIT\nENGAGE (7)", 1, 1, alignRight: true);
        Entete(table, "ENGT EN\nCOURS (8)", 1, 1, alignRight: true);
        Entete(table, "CREDIT\nDISP. (9)", 1, 1, alignRight: true);
    }

    private static void ComposeLigneTravail(TableDescriptor table, FicheImputationLigneDto l)
    {
        Cell(table, l.Item.ToString(Fr), alignRight: true);
        Cell(table, l.CodeUB);
        Cell(table, l.NumeroSuiviBudgetaire ?? "—", alignCenter: true);
        Cell(table, l.RubriqueBudgetaire ?? "—");
        Cell(table, SnelUsdFormat.Cell(l.EngagementEnCoursMensuel), alignRight: true);
        Cell(table, SnelUsdFormat.Cell(l.EngagementEnCoursAnnuel), alignRight: true);
    }

    private static void ComposeLigneDefinitive(TableDescriptor table, FicheImputationLigneDto l)
    {
        Cell(table, l.Item.ToString(Fr), alignRight: true);
        Cell(table, l.CodeUB);
        Cell(table, l.NumeroSuiviBudgetaire ?? "—", alignCenter: true);
        Cell(table, l.RubriqueBudgetaire ?? "—");
        Cell(table, SnelUsdFormat.Cell(l.BudgetMensuel), alignRight: true);
        Cell(table, SnelUsdFormat.Cell(l.CreditEngageMensuel), alignRight: true);
        Cell(table, SnelUsdFormat.Cell(l.EngagementEnCoursMensuel), alignRight: true);
        Cell(table, SnelUsdFormat.Cell(l.CreditDisponibleMensuel), alignRight: true);
        Cell(table, SnelUsdFormat.Cell(l.BudgetAnnuel), alignRight: true);
        Cell(table, SnelUsdFormat.Cell(l.CreditEngageAnnuel), alignRight: true);
        Cell(table, SnelUsdFormat.Cell(l.EngagementEnCoursAnnuel), alignRight: true);
        Cell(table, SnelUsdFormat.Cell(l.CreditDisponibleAnnuel), alignRight: true);
    }

    private static void ComposeTotalTravail(TableDescriptor table, FicheImputationTotauxDto t)
    {
        Cell(table, "", isTotal: true);
        Cell(table, "", isTotal: true);
        Cell(table, "", isTotal: true);
        Cell(table, "TOTAL", bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalEngagementEnCoursMensuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalEngagementEnCoursAnnuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
    }

    private static void ComposeTotalDefinitive(TableDescriptor table, FicheImputationTotauxDto t)
    {
        Cell(table, "", isTotal: true);
        Cell(table, "", isTotal: true);
        Cell(table, "", isTotal: true);
        Cell(table, "TOTAL", bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalBudgetMensuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalCreditEngageMensuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalEngagementEnCoursMensuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalCreditDisponibleMensuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalBudgetAnnuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalCreditEngageAnnuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalEngagementEnCoursAnnuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
        Cell(table, SnelUsdFormat.Cell(t.TotalCreditDisponibleAnnuel, dashIfNull: false), alignRight: true, bold: true, isTotal: true);
    }

    private static void ComposeSignatures(IContainer container, FicheImputationBudgetaireDto fiche)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => ComposeSignatureBox(c, fiche.GestionnaireJunior));
            row.ConstantItem(MmPt(2));
            row.RelativeItem().Element(c => ComposeSignatureBox(c, fiche.GestionnaireSenior));
            row.ConstantItem(MmPt(2));
            row.RelativeItem().Element(c => ComposeSignatureBox(c, fiche.ChefDivision));
        });
    }

    private static void ComposeSignatureBox(IContainer container, FicheImputationSignataireDto signataire)
    {
        container.Border(0.6f).BorderColor(Border).Column(col =>
        {
            col.Item().Background(HeaderBg).Padding(MmPt(1.2f)).AlignCenter()
                .Text(signataire.Fonction ?? "—").Bold().FontSize(7f).FontColor(Colors.White);

            col.Item().Padding(MmPt(1.8f)).MinHeight(MmPt(14)).Column(inner =>
            {
                if (!string.IsNullOrWhiteSpace(signataire.NomComplet))
                    inner.Item().Text(signataire.NomComplet).SemiBold().FontSize(7.5f);

                if (signataire.Date is DateTime date)
                {
                    inner.Item().PaddingTop(MmPt(0.6f)).Text($"Date : {date.ToString("dd/MM/yyyy", Fr)}")
                        .FontSize(6.8f).FontColor(Colors.Grey.Darken1);
                }

                inner.Item().PaddingTop(MmPt(2.5f)).Text("Signature / Paraphe")
                    .Italic().FontSize(6.5f).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private static void Entete(
        TableDescriptor table,
        string text,
        uint rowSpan,
        uint colSpan,
        bool alignRight = false)
    {
        table.Cell().RowSpan(rowSpan).ColumnSpan(colSpan)
            .Background(SubHeaderBg).Border(0.45f).BorderColor(Border)
            .MinHeight(MmPt(9))
            .PaddingVertical(MmPt(1))
            .PaddingHorizontal(MmPt(0.6f))
            .AlignMiddle().Element(e =>
            {
                var cell = alignRight ? e.AlignRight() : e.AlignCenter();
                cell.Text(text).Bold().FontSize(HeaderFontSize).LineHeight(1.05f);
            });
    }

    private static void EnteteGroup(TableDescriptor table, string text, uint colSpan)
    {
        table.Cell().ColumnSpan(colSpan)
            .Background(HeaderBg).Border(0.45f).BorderColor(Border)
            .MinHeight(MmPt(6.5f))
            .PaddingVertical(MmPt(1))
            .PaddingHorizontal(MmPt(0.6f))
            .AlignMiddle().AlignCenter()
            .Text(text).Bold().FontSize(GroupFontSize).FontColor(Colors.White);
    }

    private static void Cell(
        TableDescriptor table,
        string text,
        bool alignRight = false,
        bool alignCenter = false,
        bool bold = false,
        bool isTotal = false)
    {
        table.Cell()
            .Background(isTotal ? TotalBg : Colors.White)
            .Border(0.45f).BorderColor(Border)
            .MinHeight(isTotal ? MmPt(6.5f) : MmPt(5.8f))
            .PaddingVertical(MmPt(0.9f))
            .PaddingHorizontal(MmPt(0.7f))
            .Element(e =>
            {
                IContainer cell = e.AlignMiddle();
                if (alignRight)
                    cell = cell.AlignRight();
                else if (alignCenter)
                    cell = cell.AlignCenter();
                else
                    cell = cell.AlignLeft();

                var t = cell.Text(text).FontSize(DataFontSize);
                if (bold) t.Bold();
            });
    }

    private static string FormaterDate(DateTime? date)
        => date?.ToString("dd/MM/yyyy", Fr) ?? "—";
}
