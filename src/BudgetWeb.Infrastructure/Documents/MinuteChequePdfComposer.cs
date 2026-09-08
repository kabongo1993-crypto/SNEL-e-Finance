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
/// A4 portrait — Minute chèque / demande d'établissement O.P. SNEL/DFI.
/// Reproduction visuelle du modèle historique administratif.
/// </summary>
internal static class MinuteChequePdfComposer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const float Mm = 72f / 25.4f;

    private static readonly string HeaderBlue = "#0B3D7A";
    private static readonly string DateBlue = "#2E6DB4";
    private static readonly string LabelBlue = "#1E5AA8";
    private static readonly string FieldBorder = "#4A90D9";
    private static readonly string OuterBorder = "#3D7FBF";
    private static readonly string TableHeaderBlue = "#C8DDF5";

    private const string TitreHistorique = "DEMANDE D'ETABLISSEMENT D'ORDRE DE PAIEMENT (O.P.)";

    private const int BoxesCompteGeneral = 10;
    private const int BoxesCpCa = 4;
    private const int BoxesMontantChiffres = 12;
    private const int BoxesLs = 2;
    private const int BoxesSuiviExtra = 14;
    private const int BoxesAppariement = 10;
    private const int SuiviGridRows = 6;

    private const float PageMarginMm = 8f;
    private const float BlockSpacingMm = 2.4f;
    private const float CellHMm = 4.6f;
    private const float CellWMm = 4.05f;
    private const float HeaderBandHMm = 5.8f;
    private const float QrSizeMm = 24f;
    private const float QrColWMm = 30f;

    private static float MmPt(float mm) => mm * Mm;
    private static float CellW => MmPt(CellWMm);
    private static float CellH => MmPt(CellHMm);

    public static void ComposeDocument(PageDescriptor page, MinuteChequeDocumentDto p)
    {
        DocumentInstrumentPerf.TimeComposeDocument(() =>
        {
        page.Size(PageSizes.A4);
        page.Margin(MmPt(PageMarginMm));
        page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Colors.Black));

        SnelInstitutionalPdfChrome.ApplyDiscreteBackgroundWatermark(page);

        page.Content()
            .Background(Colors.White)
            .Border(0.8f).BorderColor(OuterBorder)
            .Padding(MmPt(2.5f))
            .Column(main =>
            {
                main.Spacing(MmPt(BlockSpacingMm));

                main.Item().Element(c => ComposeHeader(c, p));
                main.Item().Element(c => ComposeTitre(c, p.NumeroOp));
                main.Item().Element(c => ComposeGrilleComptable(c, p));
                main.Item().Element(c => ComposeGrilleSuiviExtra(c, p));
                main.Item().Element(c => ComposeBlocOpBeneficiaire(c, p));
                main.Item().Element(c => ComposeBlocMontantAPayer(c, p));
                main.Item().Element(c => ComposeBlocMotif(c, p));
                main.Item().Element(c => ComposeZonesSignature(c, p));
                main.Item().Element(c => ComposeZoneApprobationQr(c, p));
            });
        });
    }

    // ── En-tête institutionnel ───────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, MinuteChequeDocumentDto p)
    {
        var logo = SnelInstitutionalPdfChrome.Logo;
        var dateDoc = p.DateDocument.ToString("dd/MM/yyyy", Fr);

        container.Row(row =>
        {
            row.RelativeItem().Row(left =>
            {
                left.ConstantItem(MmPt(18)).AlignTop().Element(e =>
                {
                    if (logo is { Length: > 0 })
                        e.Height(MmPt(14)).Image(logo).FitArea();
                });

                left.RelativeItem().PaddingLeft(MmPt(2)).Column(c =>
                {
                    c.Item().Text("SOCIETE NATIONALE D'ELECTRICITE SA")
                        .Bold().FontSize(10f).FontColor(HeaderBlue);
                    c.Item().Text("DIRECTION DES FINANCES")
                        .SemiBold().FontSize(8.5f).FontColor(LabelBlue);
                    c.Item().Text("DIRECTION DES COMPTABILITES")
                        .SemiBold().FontSize(8.5f).FontColor(LabelBlue);
                    c.Item().Text("DFI/DCI")
                        .SemiBold().FontSize(8.5f).FontColor(LabelBlue);
                });
            });

            row.ConstantItem(MmPt(28)).AlignTop().AlignRight()
                .Border(0.7f).BorderColor(FieldBorder).CornerRadius(MmPt(3))
                .Background(TableHeaderBlue)
                .PaddingVertical(MmPt(1.2f)).PaddingHorizontal(MmPt(2))
                .Column(c =>
                {
                    c.Item().AlignCenter().Text(dateDoc).Bold().FontSize(9f).FontColor(DateBlue);
                });
        });
    }

    private static void ComposeTitre(IContainer container, string numeroOp)
    {
        var numero = string.IsNullOrWhiteSpace(numeroOp) ? string.Empty : numeroOp.Trim();

        container.PaddingVertical(MmPt(0.5f)).AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(x => x.FontColor(HeaderBlue));
            text.Span($"{TitreHistorique} N° ").Bold().FontSize(10.5f);
            if (string.IsNullOrEmpty(numero))
            {
                text.Span(new string('.', 16)).FontSize(8f);
                return;
            }

            text.Span(new string('.', 4)).FontSize(8f);
            text.Span($" {numero} ").Bold().FontSize(10.5f);
            text.Span(new string('.', 4)).FontSize(8f);
        });
    }

    // ── Grille comptable : CG | CP/CA | MONTANT | L/S ───────────────────────

    private static void ComposeGrilleComptable(IContainer container, MinuteChequeDocumentDto p)
    {
        container
            .Border(0.65f).BorderColor(FieldBorder)
            .Row(row =>
            {
                row.RelativeItem(3.2f).Element(c => ColonneGrilleComptable(
                    c, "COMPTE GENERAL", p.CompteGeneral, BoxesCompteGeneral));
                row.ConstantItem(0.6f).LineVertical(0.55f).LineColor(FieldBorder);
                row.RelativeItem(1.2f).Element(c => ColonneGrilleComptable(
                    c, "CP / CA", p.CpCa, BoxesCpCa));
                row.ConstantItem(0.6f).LineVertical(0.55f).LineColor(FieldBorder);
                row.RelativeItem(2.4f).Element(c => ColonneMontant(c, p));
                row.ConstantItem(0.6f).LineVertical(0.55f).LineColor(FieldBorder);
                row.RelativeItem(0.7f).Element(c => ColonneGrilleComptable(
                    c, "L / S", p.Ls, BoxesLs));
            });
    }

    private static void ColonneGrilleComptable(
        IContainer container,
        string label,
        string? value,
        int boxCount)
    {
        container.Column(col =>
        {
            col.Item().Height(MmPt(HeaderBandHMm)).Background(TableHeaderBlue)
                .BorderBottom(0.55f).BorderColor(FieldBorder)
                .AlignCenter().AlignMiddle()
                .Text(label).Bold().FontSize(7.5f).FontColor(LabelBlue);

            col.Item().PaddingVertical(MmPt(0.8f)).PaddingHorizontal(MmPt(0.5f))
                .AlignCenter()
                .Element(c => GrilleCaracteres(c, value, boxCount));
        });
    }

    private static void ColonneMontant(IContainer container, MinuteChequeDocumentDto p)
    {
        container.Column(col =>
        {
            col.Item().Height(MmPt(HeaderBandHMm)).Background(TableHeaderBlue)
                .BorderBottom(0.55f).BorderColor(FieldBorder)
                .AlignCenter().AlignMiddle()
                .Text("MONTANT").Bold().FontSize(7.5f).FontColor(LabelBlue);

            col.Item().PaddingVertical(MmPt(0.8f)).PaddingHorizontal(MmPt(0.4f))
                .AlignCenter()
                .Element(c => LigneMontantComptable(c, p.DevisePaiement, p.MontantPaiement, BoxesMontantChiffres));
        });
    }

    /// <summary>Ligne MONTANT : case devise + cases chiffres (alignée à droite).</summary>
    private static void LigneMontantComptable(
        IContainer container,
        string? devise,
        decimal? montant,
        int boxCount)
    {
        var deviseLabel = string.IsNullOrWhiteSpace(devise) ? string.Empty : ResolveDeviseLabel(devise);

        container.Row(row =>
        {
            row.AutoItem().Height(CellH).MinWidth(MmPt(8))
                .Border(0.55f).BorderColor(FieldBorder)
                .Background(Colors.White)
                .AlignCenter().AlignMiddle()
                .Text(deviseLabel).Bold().FontSize(6.5f).FontColor(LabelBlue);

            row.AutoItem().PaddingLeft(MmPt(0.5f))
                .Element(c => GrilleMontantChiffres(c, montant, boxCount));
        });
    }

    private static void GrilleMontantChiffres(IContainer container, decimal? montant, int boxCount)
    {
        var digits = montant is decimal m ? FormatMontantDigits(m) : string.Empty;

        container.Row(row =>
        {
            for (var i = 0; i < boxCount; i++)
            {
                var digitIndex = i - (boxCount - digits.Length);
                var ch = digitIndex >= 0 ? digits[digitIndex].ToString() : string.Empty;
                row.ConstantItem(CellW).Height(CellH)
                    .Border(0.45f).BorderColor(FieldBorder)
                    .Background(Colors.White)
                    .AlignCenter().AlignMiddle()
                    .Text(ch).Bold().FontSize(6.5f).FontColor(Colors.Black);
            }
        });
    }

    // ── Grande grille suivi extra-comptable ──────────────────────────────────

    private static void ComposeGrilleSuiviExtra(IContainer container, MinuteChequeDocumentDto p)
    {
        container
            .Border(0.65f).BorderColor(FieldBorder)
            .Row(row =>
            {
                row.RelativeItem(2.6f).Element(c => ColonneSuiviExtra(
                    c, "SUIVI EXTRA COMPTABLE", p.SuiviExtraComptable, BoxesSuiviExtra));
                row.ConstantItem(0.6f).LineVertical(0.55f).LineColor(FieldBorder);
                row.RelativeItem(2f).Element(c => ColonneSuiviExtra(
                    c, "N° APPARIEMENT", p.NumeroAppariement, BoxesAppariement));
                row.ConstantItem(0.6f).LineVertical(0.55f).LineColor(FieldBorder);
                row.RelativeItem(2.4f).Element(c => ColonneSuiviMontant(c, p));
            });
    }

    private static void ColonneSuiviExtra(
        IContainer container,
        string label,
        string? value,
        int boxCount)
    {
        container.Column(col =>
        {
            col.Item().Height(MmPt(HeaderBandHMm)).Background(TableHeaderBlue)
                .BorderBottom(0.55f).BorderColor(FieldBorder)
                .AlignCenter().AlignMiddle()
                .Text(label).Bold().FontSize(7.2f).FontColor(LabelBlue);

            col.Item().PaddingVertical(MmPt(0.6f)).PaddingHorizontal(MmPt(0.4f)).Column(rows =>
            {
                for (var r = 0; r < SuiviGridRows; r++)
                {
                    var lineValue = r == 0 ? value : null;
                    rows.Item().PaddingBottom(MmPt(0.35f))
                        .Element(c => GrilleCaracteres(c, lineValue, boxCount));
                }
            });
        });
    }

    private static void ColonneSuiviMontant(IContainer container, MinuteChequeDocumentDto p)
    {
        container.Column(col =>
        {
            col.Item().Height(MmPt(HeaderBandHMm)).Background(TableHeaderBlue)
                .BorderBottom(0.55f).BorderColor(FieldBorder)
                .AlignCenter().AlignMiddle()
                .Text("MONTANT").Bold().FontSize(7.2f).FontColor(LabelBlue);

            col.Item().PaddingVertical(MmPt(0.6f)).PaddingHorizontal(MmPt(0.4f)).Column(rows =>
            {
                for (var r = 0; r < SuiviGridRows; r++)
                {
                    rows.Item().PaddingBottom(MmPt(0.35f)).AlignCenter()
                        .Element(c => LigneMontantComptable(
                            c,
                            r == 0 ? p.DevisePaiement : null,
                            r == 0 ? p.MontantPaiement : null,
                            BoxesMontantChiffres));
                }
            });
        });
    }

    private static void GrilleCaracteres(IContainer container, string? value, int boxCount)
    {
        var chars = NormalizeAccountingValue(value);

        container.AlignCenter().Row(row =>
        {
            for (var i = 0; i < boxCount; i++)
            {
                var ch = i < chars.Length ? chars[i].ToString() : string.Empty;
                row.ConstantItem(CellW).Height(CellH)
                    .Border(0.45f).BorderColor(FieldBorder)
                    .Background(Colors.White)
                    .AlignCenter().AlignMiddle()
                    .Text(ch).Bold().FontSize(6.5f).FontColor(Colors.Black);
            }
        });
    }

    // ── Bloc O.P. / bénéficiaire / banque ────────────────────────────────────

    private static void ComposeBlocOpBeneficiaire(IContainer container, MinuteChequeDocumentDto p)
    {
        var numeroOp = DisplayOptional(p.NumeroOp);
        var beneficiaire = DisplayOptional(p.BeneficiaireAffichage);
        var adresse = DisplayOptional(p.BeneficiaireAdresse);
        var banque = DisplayOptional(p.BeneficiaireBanque);
        var compte = DisplayOptional(p.BeneficiaireNumeroCompte);

        container.Border(0.65f).BorderColor(FieldBorder).Column(col =>
        {
            col.Item().BorderBottom(0.55f).BorderColor(FieldBorder).Row(row =>
            {
                row.RelativeItem().Padding(MmPt(1)).Row(r =>
                {
                    r.AutoItem().Text("O.P. N° ").Bold().FontSize(8.5f).FontColor(LabelBlue);
                    r.RelativeItem().MinHeight(MmPt(5))
                        .BorderBottom(0.45f).BorderColor(Colors.Grey.Lighten1)
                        .PaddingBottom(MmPt(0.3f))
                        .Text(numeroOp).Bold().FontSize(9f).FontColor(Colors.Black);
                });

                row.ConstantItem(MmPt(52)).BorderLeft(0.55f).BorderColor(FieldBorder)
                    .Background(TableHeaderBlue).AlignCenter().AlignMiddle()
                    .Text("A LA BANQUE").Bold().FontSize(8f).FontColor(LabelBlue);
            });

            col.Item().BorderBottom(0.55f).BorderColor(FieldBorder).Padding(MmPt(1.2f)).Column(fields =>
            {
                fields.Item().Text("Nom ou Raison sociale et").SemiBold().FontSize(7.5f).FontColor(LabelBlue);
                fields.Item().Text("Adresse du bénéficiaire").SemiBold().FontSize(7.5f).FontColor(LabelBlue);
                fields.Item().PaddingTop(MmPt(0.5f)).MinHeight(MmPt(12)).Column(lines =>
                {
                    if (!string.IsNullOrWhiteSpace(beneficiaire))
                    {
                        lines.Item().Text(ForPdfInlineDisplay(beneficiaire))
                            .FontSize(8.5f).FontColor(Colors.Black).LineHeight(1.2f);
                    }

                    if (!string.IsNullOrWhiteSpace(adresse))
                    {
                        lines.Item().PaddingTop(MmPt(0.3f)).Text(ForPdfInlineDisplay(adresse))
                            .FontSize(8.5f).FontColor(Colors.Black).LineHeight(1.2f);
                    }
                });
            });

            col.Item().Padding(MmPt(1.2f)).Column(fields =>
            {
                fields.Item().Text("Banque et N° de Compte").SemiBold().FontSize(7.5f).FontColor(LabelBlue);
                fields.Item().PaddingTop(MmPt(0.5f)).MinHeight(MmPt(6))
                    .Text(ComposeBanqueCompte(banque, compte))
                    .FontSize(8.5f).FontColor(Colors.Black).LineHeight(1.2f);
            });
        });
    }

    // ── Montant à payer ────────────────────────────────────────────────────────

    private static void ComposeBlocMontantAPayer(IContainer container, MinuteChequeDocumentDto p)
    {
        var devise = ResolveDeviseLabel(p.DevisePaiement);
        var montantChiffres = FormatMontantAffichage(p.MontantPaiement, p.DevisePaiement);
        var montantLettres = p.MontantEnLettres.Trim().ToUpperInvariant();

        container.Border(0.65f).BorderColor(FieldBorder).Column(col =>
        {
            col.Item().Background(TableHeaderBlue).BorderBottom(0.55f).BorderColor(FieldBorder)
                .PaddingVertical(MmPt(0.6f)).AlignCenter()
                .Text("MONTANT A PAYER").Bold().FontSize(8.5f).FontColor(LabelBlue);

            col.Item().PaddingHorizontal(MmPt(1.5f)).PaddingVertical(MmPt(1)).Column(lines =>
            {
                lines.Spacing(MmPt(1.2f));

                lines.Item().Row(row =>
                {
                    row.AutoItem().Text("En chiffres : ").SemiBold().FontSize(8f).FontColor(LabelBlue);
                    row.AutoItem().PaddingLeft(MmPt(0.5f))
                        .Border(0.5f).BorderColor(FieldBorder).Background(Colors.White)
                        .PaddingHorizontal(MmPt(1.5f)).PaddingVertical(MmPt(0.4f))
                        .Text($"{devise}  {montantChiffres}").Bold().FontSize(9f).FontColor(Colors.Black);
                });

                lines.Item().Row(row =>
                {
                    row.AutoItem().AlignTop().Text("En lettres : ").SemiBold().FontSize(8f).FontColor(LabelBlue);
                    row.RelativeItem().PaddingLeft(MmPt(0.5f)).MinHeight(MmPt(8))
                        .BorderBottom(0.45f).BorderColor(Colors.Grey.Lighten1)
                        .PaddingBottom(MmPt(0.4f))
                        .Text(montantLettres).FontSize(8.5f).FontColor(Colors.Black).LineHeight(1.15f);
                });
            });
        });
    }

    // ── Motif ──────────────────────────────────────────────────────────────────

    private static void ComposeBlocMotif(IContainer container, MinuteChequeDocumentDto p)
    {
        var motif = p.Motif.Trim().ToUpperInvariant();

        container.Border(0.65f).BorderColor(FieldBorder).Column(col =>
        {
            col.Item().Background(TableHeaderBlue).BorderBottom(0.55f).BorderColor(FieldBorder)
                .PaddingVertical(MmPt(0.6f)).AlignCenter()
                .Text("MOTIF DU PAIEMENT").Bold().FontSize(8.5f).FontColor(LabelBlue);

            col.Item().MinHeight(MmPt(16)).Padding(MmPt(1.5f))
                .Text(motif).FontSize(8.5f).FontColor(Colors.Black).LineHeight(1.2f);
        });
    }

    // ── Signatures ───────────────────────────────────────────────────────────

    private static void ComposeZonesSignature(IContainer container, MinuteChequeDocumentDto p)
    {
        var etabliPar = DisplayOptional(p.EtabliPar);

        container.Row(row =>
        {
            row.RelativeItem().Element(c => ZoneSignature(
                c, "ETABLI PAR", etabliPar, signatureHeightMm: 10f));
            row.ConstantItem(MmPt(4));
            row.RelativeItem().Element(c => ZoneSignature(
                c, "VISA TRESORERIE", string.Empty, signatureHeightMm: 10f));
        });
    }

    private static void ZoneSignature(
        IContainer container,
        string titre,
        string value,
        float signatureHeightMm)
    {
        container.Border(0.65f).BorderColor(FieldBorder).Column(col =>
        {
            col.Item().Background(TableHeaderBlue).BorderBottom(0.55f).BorderColor(FieldBorder)
                .PaddingVertical(MmPt(0.5f)).AlignCenter()
                .Text(titre).Bold().FontSize(8f).FontColor(LabelBlue);

            col.Item().Padding(MmPt(1)).Column(body =>
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    body.Item().Text(value).FontSize(8f).FontColor(Colors.Black).LineHeight(1.1f);
                }

                body.Item().PaddingTop(MmPt(0.5f)).MinHeight(MmPt(signatureHeightMm))
                    .BorderBottom(0.45f).BorderColor(Colors.Grey.Lighten1);
            });
        });
    }

    // ── QR + approbation / ordonnancement ─────────────────────────────────────

    private static void ComposeZoneApprobationQr(IContainer container, MinuteChequeDocumentDto p)
    {
        container.MinHeight(MmPt(24)).Row(row =>
        {
            row.ConstantItem(MmPt(QrColWMm)).AlignBottom()
                .Element(c => ComposeQr(c, p.IdentifiantVerification));

            row.ConstantItem(MmPt(3));

            row.RelativeItem().AlignBottom()
                .Border(0.65f).BorderColor(FieldBorder)
                .Column(col =>
                {
                    col.Item().Background(TableHeaderBlue).BorderBottom(0.55f).BorderColor(FieldBorder)
                        .PaddingVertical(MmPt(0.55f)).AlignCenter()
                        .Text("APPROBATION / ORDONNANCEMENT").Bold().FontSize(8f).FontColor(LabelBlue);

                    col.Item().MinHeight(MmPt(14)).Padding(MmPt(1));
                });
        });
    }

    private static void ComposeQr(IContainer container, string identifiantVerification)
    {
        DocumentInstrumentPerf.TimeQr(() =>
        {
        var payload = string.IsNullOrWhiteSpace(identifiantVerification)
            ? "SNEL-MINUTE-CHEQUE"
            : identifiantVerification.Trim();

        container.Column(col =>
        {
            col.Item().Width(MmPt(QrSizeMm)).Height(MmPt(QrSizeMm)).Background(Colors.White)
                .Svg(size =>
                {
                    var writer = new QRCodeWriter();
                    var matrix = writer.encode(payload, BarcodeFormat.QR_CODE,
                        Math.Max(64, (int)size.Width), Math.Max(64, (int)size.Height));
                    return new SvgRenderer().Render(matrix, BarcodeFormat.QR_CODE, null).Content;
                });

            if (!string.IsNullOrWhiteSpace(identifiantVerification))
            {
                col.Item().PaddingTop(MmPt(0.4f)).Width(MmPt(QrColWMm))
                    .Text(identifiantVerification.Trim())
                    .FontSize(4.8f).FontColor(Colors.Grey.Darken1).LineHeight(1f);
            }
        });
        });
    }

    // ── Utilitaires ───────────────────────────────────────────────────────────

    private static string ComposeBanqueCompte(string banque, string compte)
    {
        if (string.IsNullOrWhiteSpace(banque) && string.IsNullOrWhiteSpace(compte))
            return string.Empty;
        if (string.IsNullOrWhiteSpace(compte))
            return ForPdfInlineDisplay(banque);
        if (string.IsNullOrWhiteSpace(banque))
            return ForPdfInlineDisplay(compte);
        return $"{ForPdfInlineDisplay(banque)} — {ForPdfInlineDisplay(compte)}";
    }

    private static string ResolveDeviseLabel(string? devise)
    {
        if (string.IsNullOrWhiteSpace(devise))
            return "FC";
        return devise.Trim().ToUpperInvariant();
    }

    private static string FormatMontantDigits(decimal montant)
    {
        var cents = (long)Math.Truncate(montant * 100m);
        var whole = cents / 100;
        var frac = cents % 100;
        return frac == 0
            ? whole.ToString(CultureInfo.InvariantCulture)
            : $"{whole.ToString(CultureInfo.InvariantCulture)}{frac.ToString("00", CultureInfo.InvariantCulture)}";
    }

    private static string FormatMontantAffichage(decimal montant, string? devise)
    {
        var formatted = string.Format(Fr, "{0:N2}", montant);
        formatted = formatted.Replace('\u00A0', ' ').Replace('\u202F', ' ');
        return string.IsNullOrWhiteSpace(devise) ? formatted : formatted;
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

    private static string ForPdfInlineDisplay(string value)
        => value.Replace("-", "\u2011", StringComparison.Ordinal);

    private static string DisplayOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
