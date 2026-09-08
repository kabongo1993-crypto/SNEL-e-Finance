using System.Globalization;
using System.Text;
using BudgetWeb.Application.Diagnostics;
using BudgetWeb.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZXing;
using ZXing.QrCode;
using ZXing.Rendering;

namespace BudgetWeb.Infrastructure.Documents;

/// <summary>
/// État A5 paysage (210 × 148 mm) — Bon provisoire SNEL/DFI.
/// Reproduction visuelle de la maquette historique administratif.
/// </summary>
internal static class BonProvisoirePdfComposer
{
    /// <summary>210 mm × 148 mm — paysage.</summary>
    public static readonly PageSize PageSizeA5Landscape = PageSizes.A5.Landscape();

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const float Mm = 72f / 25.4f;

    private static readonly string HeaderBlue = "#0B3D7A";
    private static readonly string DateBlue = "#2E6DB4";
    private static readonly string LabelBlue = "#1E5AA8";
    private static readonly string FieldBorder = "#4A90D9";
    private static readonly string OuterBorder = "#3D7FBF";
    private static readonly string NbBlue = "#5C4DB2";

    private const string RecuInstitutionnelDefaut = "RECU DE LA CAISSE CENTRALE SNEL SOMME DE FC";
    private const string MentionJustificationDefaut =
        "NB : CE MONTANT EST A JUSTIFIER DANS LES 24 HEURES DU RETRAIT";

    // Grilles de la maquette historique.
    private const int BoxesCompteGeneral = 10;
    private const int BoxesCompteParticulier = 5;
    private const int BoxesNumeroAppariement = 10;

    private static float MmPt(float mm) => mm * Mm;

    public static void ComposeDocument(PageDescriptor page, BonProvisoireDocumentDto p)
    {
        DocumentInstrumentPerf.TimeComposeDocument(() =>
        {
        page.Size(PageSizeA5Landscape);
        page.Margin(MmPt(2));
        page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Colors.Black));

        SnelInstitutionalPdfChrome.ApplyDiscreteBackgroundWatermark(page);

        // Pas de fond blanc opaque sur tout le contenu : le filigrane reste visible.
        // Partie haute : contenu descriptif + espace souple.
        // Partie basse ancrée : NB + bloc comptable + QR / ordonnancement / acquit.
        const float corpsBlockSpacingMm = 2.8f;

        page.Content()
            .Border(0.8f).BorderColor(OuterBorder)
            .Layers(layers =>
            {
                layers.PrimaryLayer().Column(main =>
                {
                    main.Spacing(0);

                    // ── Zone 1 : en-tête ──
                    main.Item().Element(c => ComposeHeader(c, p));

                    // ── Zone 2 : corps descriptif (montant, lettres, demande) ──
                    main.Item().PaddingHorizontal(MmPt(2.5f)).PaddingTop(MmPt(3))
                        .Column(zoneDesc =>
                        {
                            zoneDesc.Spacing(MmPt(corpsBlockSpacingMm));
                            zoneDesc.Item().Element(c => ComposeLigneRecuMontant(c, p));
                            zoneDesc.Item().Element(c => ComposeLigneEnLettres(c, p));
                            zoneDesc.Item().Element(c => ComposeLigneDemande(c, p));
                        });

                    // Espace souple : répartit la hauteur A5 entre corps descriptif et zone basse
                    main.Item().ExtendVertical();
                });

                // ── Zones 3 + 4 : NB, comptable et bas de page (ancrés en bas) ──
                layers.Layer()
                    .AlignBottom()
                    .PaddingHorizontal(MmPt(2.5f))
                    .PaddingBottom(MmPt(2))
                    .Column(zoneBasse =>
                    {
                        zoneBasse.Spacing(0);
                        zoneBasse.Item().Element(c => ComposeLigneNb(c, p));
                        zoneBasse.Item().PaddingTop(MmPt(4.5f))
                            .Element(c => ComposeBlocComptable(c, p));
                        zoneBasse.Item().PaddingTop(MmPt(5))
                            .Element(c => ComposeZoneFinale(c, p));
                    });
            });
        });
    }

    // ── Zone 1 : en-tête institutionnel compact ──────────────────────────────

    private static void ComposeHeader(IContainer container, BonProvisoireDocumentDto p)
    {
        var logo = SnelInstitutionalPdfChrome.Logo;
        var dateDoc = p.DateBon.ToString("dd/MM/yyyy", Fr);

        container.Background(HeaderBlue).Padding(MmPt(1.5f)).Column(band =>
        {
            band.Spacing(MmPt(0.8f));

            band.Item().Row(row =>
            {
                row.ConstantItem(MmPt(14)).AlignMiddle().Element(e =>
                {
                    if (logo is { Length: > 0 })
                        e.Height(MmPt(10)).Image(logo).FitArea();
                });

                row.RelativeItem().PaddingLeft(MmPt(1.5f)).AlignMiddle().Column(c =>
                {
                    c.Item().Text("SOCIETE NATIONALE D'ELECTRICITE SA")
                        .Bold().FontSize(9f).FontColor(Colors.White);
                    c.Item().Text("DEPARTEMENT DES FINANCES")
                        .SemiBold().FontSize(8f).FontColor(Colors.White);
                    c.Item().Text("DFI/DBU")
                        .SemiBold().FontSize(8f).FontColor(Colors.White);
                });

                row.ConstantItem(MmPt(20)).AlignRight().AlignTop()
                    .Background(DateBlue)
                    .PaddingVertical(MmPt(0.6f)).PaddingHorizontal(MmPt(1.5f))
                    .Column(c =>
                    {
                        c.Item().AlignCenter().Text("\u2637").FontSize(8.5f).FontColor(Colors.White);
                        c.Item().AlignCenter().Text(dateDoc).Bold().FontSize(8f).FontColor(Colors.White);
                    });
            });

            band.Item().PaddingBottom(MmPt(0.4f)).AlignCenter()
                .Element(c => ComposeTitreBon(c, p.NumeroBon));
        });
    }

    private static void ComposeTitreBon(IContainer container, string numeroBon)
    {
        var numero = numeroBon.Trim();

        container.Text(text =>
        {
            text.Span("BON PROVISOIRE N° ").Bold().FontSize(9.5f).FontColor(Colors.White);
            text.Span(new string('.', 10)).FontSize(7.5f).FontColor(Colors.White);
            text.Span($" {numero} ").Bold().FontSize(9.5f).FontColor(Colors.White);
            text.Span(new string('.', 10)).FontSize(7.5f).FontColor(Colors.White);
        });
    }

    // ── Zone 2 : corps du bon (une ligne / bloc par item) ─────────────────

    private static void ComposeLigneRecuMontant(IContainer container, BonProvisoireDocumentDto p)
    {
        var recu = ResolveRecuLabel(p.RecuCaisseCentrale);

        container.PaddingBottom(MmPt(0.4f)).Row(row =>
        {
            row.AutoItem().AlignMiddle()
                .Text(recu).SemiBold().FontSize(8.5f).FontColor(LabelBlue).LineHeight(1.2f);
            row.AutoItem().PaddingLeft(MmPt(1.2f)).AlignMiddle()
                .Border(0.6f).BorderColor(FieldBorder)
                .Background(Colors.White)
                .PaddingHorizontal(MmPt(1.8f)).PaddingVertical(MmPt(0.5f))
                .Text(FormatMontantFc(p.MontantFc)).Bold().FontSize(9.5f).FontColor(Colors.Black);
        });
    }

    private static void ComposeLigneEnLettres(IContainer container, BonProvisoireDocumentDto p)
    {
        container.PaddingVertical(MmPt(0.3f)).Text(text =>
        {
            text.DefaultTextStyle(x => x.LineHeight(1.25f));
            text.Span("( EN LETTRES ) : ").SemiBold().FontSize(8.5f).FontColor(LabelBlue);
            text.Span(p.MontantEnLettres.Trim().ToUpperInvariant())
                .FontSize(8.5f).FontColor(Colors.Black);
        });
    }

    private static void ComposeLigneDemande(IContainer container, BonProvisoireDocumentDto p)
    {
        var reference = p.ReferenceDemande.Trim();
        var motif = p.Motif.Trim();

        container.PaddingVertical(MmPt(0.3f)).Text(text =>
        {
            text.DefaultTextStyle(x => x.LineHeight(1.25f));
            text.Span("SUIVANT LA NOTE DE DEMANDE DE PAIEMENT N° ")
                .SemiBold().FontSize(8.5f).FontColor(LabelBlue);
            text.Span(reference).Bold().FontSize(8.5f).FontColor(Colors.Black);
            if (!string.IsNullOrWhiteSpace(motif))
            {
                text.Span(" (").SemiBold().FontSize(8.5f).FontColor(LabelBlue);
                text.Span(motif.ToUpperInvariant()).FontSize(8.5f).FontColor(Colors.Black);
                text.Span(")").SemiBold().FontSize(8.5f).FontColor(LabelBlue);
            }
        });
    }

    private static void ComposeLigneNb(IContainer container, BonProvisoireDocumentDto p)
    {
        var mention = string.IsNullOrWhiteSpace(p.MentionJustificationRetrait)
            ? MentionJustificationDefaut
            : p.MentionJustificationRetrait.Trim().ToUpperInvariant();

        container.PaddingTop(MmPt(0.2f)).Text(mention)
            .Bold().FontSize(8.5f).FontColor(NbBlue).LineHeight(1.2f);
    }

    // ── Zone 3 : bloc comptable ─────────────────────────────────────────────

    private static void ComposeBlocComptable(IContainer container, BonProvisoireDocumentDto p)
    {
        container
            .Border(0.7f).BorderColor(FieldBorder).CornerRadius(MmPt(2))
            .PaddingVertical(MmPt(1.2f)).PaddingHorizontal(MmPt(1))
            .Row(row =>
            {
                row.RelativeItem().Element(c => ColonneComptable(
                    c, "COMPTE GENERAL", p.CompteGeneral, BoxesCompteGeneral));
                row.ConstantItem(0.6f).LineVertical(0.5f).LineColor(FieldBorder);
                row.RelativeItem().Element(c => ColonneComptable(
                    c, "COMPTE PARTICULIER", p.CompteParticulier, BoxesCompteParticulier));
                row.ConstantItem(0.6f).LineVertical(0.5f).LineColor(FieldBorder);
                row.RelativeItem().Element(c => ColonneComptable(
                    c, "N° APPARIEMENT", p.NumeroAppariement, BoxesNumeroAppariement));
            });
    }

    private static void ColonneComptable(
        IContainer container,
        string label,
        string? value,
        int boxCount)
    {
        container.PaddingHorizontal(MmPt(0.5f)).Column(col =>
        {
            col.Item().AlignCenter()
                .Text(label).Bold().FontSize(7.5f).FontColor(LabelBlue);
            col.Item().PaddingTop(MmPt(0.8f)).AlignCenter()
                .Element(c => GrilleCaracteres(c, value, boxCount));
        });
    }

    private static void GrilleCaracteres(IContainer container, string? value, int boxCount)
    {
        var chars = NormalizeAccountingValue(value);
        var cell = MmPt(4.1f);

        container.Row(row =>
        {
            for (var i = 0; i < boxCount; i++)
            {
                var ch = i < chars.Length ? chars[i].ToString() : string.Empty;
                row.ConstantItem(cell).Height(cell)
                    .BorderLeft(i == 0 ? 0.55f : 0f).BorderColor(FieldBorder)
                    .BorderRight(0.55f).BorderColor(FieldBorder)
                    .BorderTop(0.55f).BorderBottom(0.55f).BorderColor(FieldBorder)
                    .Background(Colors.White)
                    .AlignCenter().AlignMiddle()
                    .Text(ch).Bold().FontSize(7f).FontColor(Colors.Black);
            }
        });
    }

    // ── Zone 4 : QR / ordonnancement / acquit — une seule Row ───────────────

    private static void ComposeZoneFinale(IContainer container, BonProvisoireDocumentDto p)
    {
        container.AlignTop().Row(row =>
        {
            // Colonne QR (gauche)
            row.ConstantItem(MmPt(24)).AlignTop()
                .Element(c => ComposeQr(c, p.IdentifiantVerification));

            // Colonne ordonnancement (centre)
            row.RelativeItem().AlignTop().PaddingHorizontal(MmPt(2))
                .Column(c =>
                {
                    c.Item().AlignCenter().Text("POUR ORDONNANCEMENT")
                        .Bold().FontSize(8.5f).FontColor(LabelBlue).LineHeight(1.15f);
                    c.Item().PaddingTop(MmPt(0.8f)).AlignCenter()
                        .Text("L'ADMINISTRATEUR - DIRECTEUR")
                        .SemiBold().FontSize(8f).FontColor(LabelBlue).LineHeight(1.15f);
                });

            // Colonne POUR ACQUIT (droite — rectangle horizontal large)
            row.ConstantItem(MmPt(100)).AlignTop()
                .Element(c => ComposePourAcquit(c, p));
        });
    }

    private static void ComposeQr(IContainer container, string identifiantVerification)
    {
        DocumentInstrumentPerf.TimeQr(() =>
        {
        var payload = string.IsNullOrWhiteSpace(identifiantVerification)
            ? "SNEL-BON-PROVISOIRE"
            : identifiantVerification.Trim();

        container.Column(col =>
        {
            col.Item()
                .Width(MmPt(18)).Height(MmPt(18))
                .Background(Colors.White)
                .Svg(size =>
                {
                    var writer = new QRCodeWriter();
                    var matrix = writer.encode(
                        payload,
                        BarcodeFormat.QR_CODE,
                        Math.Max(64, (int)size.Width),
                        Math.Max(64, (int)size.Height));
                    var renderer = new SvgRenderer();
                    return renderer.Render(matrix, BarcodeFormat.QR_CODE, null).Content;
                });

            if (!string.IsNullOrWhiteSpace(identifiantVerification))
            {
                col.Item().PaddingTop(MmPt(0.4f))
                    .Text(identifiantVerification.Trim())
                    .FontSize(4.8f).FontColor(Colors.Grey.Darken1).LineHeight(1.1f);
            }
        });
        });
    }

    private static void ComposePourAcquit(IContainer container, BonProvisoireDocumentDto p)
    {
        var nom = DisplayOptional(p.BeneficiaireAffichage);
        var matricule = ComposeIdentiteMatricule(p.BeneficiaireMatricule, p.BeneficiaireIdentite);
        var direction = DisplayOptional(p.DirectionBeneficiaire);

        container
            .Border(0.6f).BorderColor(FieldBorder)
            .Column(col =>
            {
                col.Item().Background(HeaderBlue)
                    .PaddingVertical(MmPt(0.25f)).AlignCenter()
                    .Text("POUR ACQUIT").Bold().FontSize(7f).FontColor(Colors.White).LineHeight(1f);

                col.Item().PaddingHorizontal(MmPt(1f)).PaddingVertical(MmPt(0.15f)).Column(fields =>
                {
                    fields.Spacing(0);
                    fields.Item().Element(c => LigneAcquit(c, "NOM - POSTNOM", nom, MmPt(1.1f)));
                    fields.Item().Element(c => LigneAcquit(c, "IDENTITE / MATRICULE", matricule, MmPt(1.1f)));
                    fields.Item().Element(c => LigneAcquit(c, "DIRECTION", direction, MmPt(1.1f)));
                    fields.Item().Element(c => LigneAcquit(c, "SIGNATURE", string.Empty, MmPt(3f)));
                });
            });
    }

    private static void LigneAcquit(IContainer container, string label, string value, float underlineMinHeight)
    {
        container
            .PaddingVertical(MmPt(0.12f))
            .Column(col =>
            {
                col.Spacing(0);

                col.Item().Row(row =>
                {
                    row.AutoItem().AlignTop()
                        .Text($"{label} : ").SemiBold().FontSize(7f).FontColor(LabelBlue).LineHeight(1f);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        row.RelativeItem().AlignTop()
                            .Text(ForPdfInlineDisplay(value)).FontSize(7.5f).FontColor(Colors.Black).LineHeight(1f);
                    }
                });

                col.Item().PaddingTop(MmPt(0.08f))
                    .MinHeight(underlineMinHeight)
                    .BorderBottom(0.45f).BorderColor(Colors.Grey.Lighten1);
            });
    }

    /// <summary>Évite les retours intempestifs sur les tirets dans le PDF (présentation uniquement).</summary>
    private static string ForPdfInlineDisplay(string value)
        => value.Replace("-", "\u2011", StringComparison.Ordinal);

    private static string ResolveRecuLabel(string? recuCaisseCentrale)
    {
        if (string.IsNullOrWhiteSpace(recuCaisseCentrale))
            return RecuInstitutionnelDefaut;

        var trimmed = recuCaisseCentrale.Trim();
        if (trimmed.Length < 15)
            return RecuInstitutionnelDefaut;

        if (trimmed.Contains("CAISSE", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains("SOMME", StringComparison.OrdinalIgnoreCase))
            return trimmed.ToUpperInvariant();

        return RecuInstitutionnelDefaut;
    }

    private static string NormalizeAccountingValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string ComposeIdentiteMatricule(string? matricule, string? identite)
    {
        if (!string.IsNullOrWhiteSpace(matricule))
            return matricule.Trim();
        if (!string.IsNullOrWhiteSpace(identite))
            return identite.Trim();
        return string.Empty;
    }

    private static string DisplayOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string FormatMontantFc(decimal montant)
    {
        var formatted = string.Format(Fr, "{0:N2}", montant);
        return formatted.Replace('\u00A0', ' ').Replace('\u202F', ' ');
    }
}
