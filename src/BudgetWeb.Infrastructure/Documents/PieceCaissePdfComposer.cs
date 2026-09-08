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
/// État A5 paysage (210 × 148 mm) — Pièce de caisse dépense SNEL/DFI.
/// Reproduction visuelle de la maquette historique administratif.
/// </summary>
internal static class PieceCaissePdfComposer
{
    /// <summary>210 mm (L) × 148 mm (H) — format A5 paysage imposé.</summary>
    public static readonly PageSize PageSizeA5Landscape = PageSizes.A5.Landscape();

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const float Mm = 72f / 25.4f;

    private static readonly string HeaderBlue = "#0B3D7A";
    private static readonly string DateBlue = "#2E6DB4";
    private static readonly string LabelBlue = "#1E5AA8";
    private static readonly string BonAPayerBlue = "#5B9BD5";
    private static readonly string FieldBorder = "#4A90D9";
    private static readonly string OuterBorder = "#3D7FBF";

    private const string RecuSnelDefaut = "RECU DE S.N.E.L";
    private const string TitreHistorique = "PIECE DE CAISSE DEPENSE";

    private static readonly string TableHeaderBlue = "#C8DDF5";

    private const int BoxesSr = 3;
    private const int BoxesComptabiliteGenerale = 8;
    private const int BoxesCpCpa = 6;
    private const int BoxesNumeroAppariement = 8;
    private const int GridDataRows = 4;
    private const int BoxesRecuChiffres = 9;

    private const float PageMarginMm = 2f;
    private const float ContentBorderMm = 1.2f;
    private const float PageWidthMm = 210f;
    private const float CellHMm = 4.55f;
    private const float BlocBorderExtraMm = 0.5f;
    private const float RecuCellWMm = 3.12f;
    private const float RecuCellHMm = 5.1f;
    private const float FcBadgeMm = 5.5f;
    private const float FcBadgeGapMm = 0.8f;
    private const float GapSrCgMm = 1f;
    private const float GridSeparatorMm = 0.6f;
    private const float RecuToGrilleGapMm = 0.25f;
    private const float RecuBorderAllowanceMm = 1.1f;
    private const float GapGrilleFcMm = 0.35f;
    private const float GapIdComptabiliteMm = 2.15f;

    private static float MmPt(float mm) => mm * Mm;

    private sealed record BlocComptableMetrics(
        float CellWMm,
        float CellWPt,
        float FcMontantWMm,
        float FcMontantWPt,
        float BlocSrWPt,
        float BlocCgWPt,
        float BlocCpWPt,
        float BlocAppWPt,
        float BlocPrincipalWPt,
        float ColFcTotalWPt,
        float GridWidthMm,
        float RecuToGrilleGapDynamicMm,
        float RecuColWMm,
        float RowTotalWMm);

    private static BlocComptableMetrics? _blocMetrics;
    private static BlocComptableMetrics Bloc => _blocMetrics ??= ComputeBlocComptableMetrics();

    /// <summary>
    /// Cellules de grille à largeur fixe ; FC réduit ; l'espace récupéré décale SR→App vers la droite.
    /// </summary>
    private static BlocComptableMetrics ComputeBlocComptableMetrics()
    {
        const float fixedCellWMm = CellHMm - 0.01f;
        const float fcMontantWMm = 38f;

        var pageInnerMm = PageWidthMm - 2f * PageMarginMm - ContentBorderMm;
        var recuColWMm = BoxesRecuChiffres * RecuCellWMm + RecuBorderAllowanceMm;

        float BlocWidthMm(int boxes) => boxes * fixedCellWMm + BlocBorderExtraMm;

        var gridWidthMm =
            BlocWidthMm(BoxesSr)
            + GapSrCgMm
            + BlocWidthMm(BoxesComptabiliteGenerale)
            + GridSeparatorMm
            + BlocWidthMm(BoxesCpCpa)
            + GridSeparatorMm
            + BlocWidthMm(BoxesNumeroAppariement);

        var fcColWMm = FcBadgeMm + FcBadgeGapMm + fcMontantWMm;

        float BlocWidthPt(int boxes) => MmPt(boxes * fixedCellWMm + BlocBorderExtraMm);

        var blocCg = BlocWidthPt(BoxesComptabiliteGenerale);
        var blocCp = BlocWidthPt(BoxesCpCpa);
        var blocApp = BlocWidthPt(BoxesNumeroAppariement);
        var colFcTotal = MmPt(fcColWMm);
        var rowContentMm = recuColWMm + RecuToGrilleGapMm + gridWidthMm + GapGrilleFcMm + fcColWMm;
        var recuToGrilleGapDynamicMm = pageInnerMm - rowContentMm + RecuToGrilleGapMm;
        if (recuToGrilleGapDynamicMm < RecuToGrilleGapMm)
            recuToGrilleGapDynamicMm = RecuToGrilleGapMm;

        return new BlocComptableMetrics(
            fixedCellWMm,
            MmPt(fixedCellWMm),
            fcMontantWMm,
            MmPt(fcMontantWMm),
            BlocWidthPt(BoxesSr),
            blocCg,
            blocCp,
            blocApp,
            blocCg + MmPt(GridSeparatorMm) + blocCp + MmPt(GridSeparatorMm) + blocApp,
            colFcTotal,
            gridWidthMm,
            recuToGrilleGapDynamicMm,
            recuColWMm,
            pageInnerMm);
    }

    private static float CellW => Bloc.CellWPt;
    private static float CellH => MmPt(CellHMm);
    private static float RecuCellW => MmPt(RecuCellWMm);
    private static float RecuCellH => MmPt(RecuCellHMm);
    private static float CellFontSize => 5f;
    private static float GapSrCg => MmPt(GapSrCgMm);
    private static float GapGrilleFc => MmPt(GapGrilleFcMm);
    private static float RecuToGrilleGap => MmPt(Bloc.RecuToGrilleGapDynamicMm);
    private static float BlocSrW => Bloc.BlocSrWPt;
    private static float BlocCgW => Bloc.BlocCgWPt;
    private static float BlocCpW => Bloc.BlocCpWPt;
    private static float BlocAppW => Bloc.BlocAppWPt;
    private static float BlocPrincipalW => Bloc.BlocPrincipalWPt;
    private static float EnteteTableauHauteur => MmPt(6.2f);
    private static float EtiquetteRowHauteur => MmPt(5f);
    private const float RecuLabelToCellsGapMm = 0.22f;
    private static float GrilleComptableBandHMm => 5f + 6.2f + GridDataRows * CellHMm;
    private static float GrilleComptableBandH => MmPt(GrilleComptableBandHMm);
    private static float ColRecuW => MmPt(Bloc.RecuColWMm);
    private static float EtiquetteTableGapMm => 0.65f;
    private static float ColFcBadgeW => MmPt(FcBadgeMm);
    private static float ColFcMontantW => Bloc.FcMontantWPt;
    private static float ColFcTotalW => Bloc.ColFcTotalWPt;
    private static float PourAcquitW => MmPt(86f);
    private static float ZoneSpacingMm => 2.2f;
    private static float GapComptableCorpsMm => 2.0f;
    private static float GapSommeMotifMm => 2.2f;
    private static float GapMotifPjMm => 1.85f;
    private static float GapApresPieceJustificativeMm => 1.7f;
    private static float GapApresSeparateurMm => 1.85f;
    private static float SommeDeCartoucheHMm => 9.9f;
    private static float SommeDeCartouchePaddingHMm => 0.45f;
    private static float SommeDeCartouchePaddingWMm => 1.0f;
    private static float SommeDeCartoucheRadiusMm => 2.2f;
    private static float SommeDeInnerBoxHMm => 8.8f;
    private static float SommeDeGapBeforeFrancsMm => 0.35f;
    private static float SommeDeGapAfterFrancsMm => 1.0f;
    private static float SommeDeLabelMinWMm => 18f;
    private static float FrancsBoxWMm => 14.2f;
    private static float RecuBlocPaddingMm => 0.28f;
    private static float RecuBlocRadiusMm => 1.2f;
    private static float RecuLabelInsetMm => 1.0f;
    private static float PiedDePageHauteurMm => 30f;
    private static float PiedDePageBottomMm => 2f;
    private static float QrSizeMm => 27f;
    private static float QrColWMm => 32f;
    private static float QrLabelGapMm => 0.45f;

    public static void ComposeDocument(PageDescriptor page, PieceCaisseDocumentDto p)
    {
        DocumentInstrumentPerf.TimeComposeDocument(() =>
        {
        // Format fixe A5 paysage (210 × 148 mm) — ne pas utiliser ContinuousSize.
        page.Size(PageSizeA5Landscape);
        page.Margin(MmPt(2));
        page.DefaultTextStyle(x => x.FontSize(8f).FontColor(Colors.Black));

        ApplyPieceCaisseWatermark(page);

        page.Content()
            .Border(0.8f).BorderColor(OuterBorder)
            .Column(main =>
            {
                main.Spacing(0);
                main.Item().Element(c => ComposeHeader(c, p));
                main.Item().PaddingTop(MmPt(ZoneSpacingMm))
                    .Element(c => ComposeZoneSuperieure(c, p));
                main.Item().PaddingTop(MmPt(GapComptableCorpsMm))
                    .Column(corps =>
                    {
                        corps.Spacing(0);
                        corps.Item().Element(c => ComposeLigneSommeDe(c, p));
                        corps.Item().PaddingTop(MmPt(GapSommeMotifMm))
                            .Element(c => ComposeLigneMotif(c, p));
                        corps.Item().PaddingTop(MmPt(GapMotifPjMm))
                            .Element(c => ComposeLignePieceJustificative(c, p));
                    });
                main.Item().PaddingTop(MmPt(GapApresPieceJustificativeMm))
                    .Element(c => c.LineHorizontal(0.8f).LineColor(OuterBorder));
                main.Item().PaddingTop(MmPt(GapApresSeparateurMm))
                    .PaddingBottom(MmPt(PiedDePageBottomMm))
                    .Element(c => ComposePiedDePage(c, p));
                main.Item().ExtendVertical();
            });
        });
    }

    /// <summary>Filigrane institutionnel discret (chrome SNEL partagé).</summary>
    private static void ApplyPieceCaisseWatermark(PageDescriptor page)
        => SnelInstitutionalPdfChrome.ApplyDiscreteBackgroundWatermark(page);

    // ── En-tête ─────────────────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, PieceCaisseDocumentDto p)
    {
        var logo = SnelInstitutionalPdfChrome.Logo;
        var dateDoc = p.DatePiece.ToString("dd/MM/yyyy", Fr);

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
                    c.Item().Text("DIRECTION DE LA TRESORERIE GENERALE")
                        .Bold().FontSize(8.5f).FontColor(Colors.White);
                    c.Item().Text("DIVISION DE GESTION DE FONDS PROPRES")
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
                .Element(c => ComposeTitrePieceCaisse(c, p.NumeroPiece));
        });
    }

    private static void ComposeTitrePieceCaisse(IContainer container, string numeroPiece)
    {
        var numero = string.IsNullOrWhiteSpace(numeroPiece) ? string.Empty : numeroPiece.Trim();

        container.Text(text =>
        {
            text.DefaultTextStyle(x => x.FontColor(Colors.White));
            text.Span($"{TitreHistorique} N° ").Bold().FontSize(9f);
            if (string.IsNullOrEmpty(numero))
            {
                text.Span(new string('.', 28)).FontSize(7.5f);
                return;
            }

            text.Span(new string('.', 6)).FontSize(7.5f);
            text.Span($" {numero} ").Bold().FontSize(9f);
            text.Span(new string('.', 6)).FontSize(7.5f);
        });
    }

    // ── Zone supérieure : ID, grille comptable, RECU, montants FC ───────────

    private static void ComposeZoneSuperieure(IContainer container, PieceCaisseDocumentDto p)
    {
        container.Column(col =>
        {
            col.Spacing(0);
            col.Item().Element(c => ComposeZoneId(c, p));

            col.Item().PaddingTop(MmPt(GapIdComptabiliteMm)).AlignLeft().Row(bloc =>
            {
                bloc.ConstantItem(ColRecuW).AlignBottom()
                    .Element(c => ComposeZoneRecu(c, p));
                bloc.ConstantItem(RecuToGrilleGap);
                bloc.AutoItem().AlignBottom().Element(c => ComposeGrilleComptable(c, p));
                bloc.ConstantItem(GapGrilleFc);
                bloc.ConstantItem(ColFcTotalW).AlignBottom()
                    .Element(c => ComposeColonneMontantsFc(c, p));
            });
        });
    }

    private static void ComposeZoneId(IContainer container, PieceCaisseDocumentDto p)
    {
        var idDisplay = ResolveIdDisplay(p.ReferenceDemande);

        container.Row(row =>
        {
            row.AutoItem().AlignMiddle()
                .Text("- ID :").SemiBold().FontSize(8f).FontColor(LabelBlue);
            row.AutoItem().PaddingLeft(MmPt(1)).Border(0.6f).BorderColor(FieldBorder)
                .Background(Colors.White).CornerRadius(MmPt(1.5f))
                .PaddingHorizontal(MmPt(2)).PaddingVertical(MmPt(0.35f))
                .Text(idDisplay).Bold().FontSize(8f).FontColor(Colors.Black);
        });
    }

    private static void ComposeEtiquettePieceDeCaisse(IContainer container)
    {
        container.AlignCenter().PaddingBottom(MmPt(EtiquetteTableGapMm))
            .Background(HeaderBlue).CornerRadius(MmPt(1))
            .PaddingHorizontal(MmPt(1.8f)).PaddingVertical(MmPt(0.2f))
            .Text("Pièce de caisse").Bold().FontSize(5.5f).FontColor(Colors.White);
    }

    private static void ComposeGrilleComptable(IContainer container, PieceCaisseDocumentDto p)
    {
        container.Row(row =>
        {
            row.ConstantItem(BlocSrW)
                .Border(0.6f).BorderColor(FieldBorder)
                .Element(c => ComposeBlocSr(c, p));

            row.ConstantItem(GapSrCg);

            row.ConstantItem(BlocPrincipalW).Column(main =>
            {
                main.Spacing(0);
                main.Item().Height(EtiquetteRowHauteur).Row(et =>
                {
                    et.ConstantItem(BlocCgW).AlignBottom()
                        .Element(c => ComposeEtiquettePieceDeCaisse(c));
                });

                main.Item()
                    .Border(0.6f).BorderColor(FieldBorder)
                    .Row(grid =>
                    {
                        grid.ConstantItem(BlocCgW).Element(c => ComposeBlocCg(c, p));
                        grid.ConstantItem(0.6f).LineVertical(0.55f).LineColor(FieldBorder);
                        grid.ConstantItem(BlocCpW).Element(c => ComposeBlocCpCpa(c, p));
                        grid.ConstantItem(0.6f).LineVertical(0.55f).LineColor(FieldBorder);
                        grid.ConstantItem(BlocAppW).Element(c => ComposeBlocAppariement(c, p));
                    });
            });
        });
    }

    private static void ComposeBlocSr(IContainer container, PieceCaisseDocumentDto p)
    {
        container.Column(col =>
        {
            col.Spacing(0);
            col.Item().Height(EtiquetteRowHauteur);
            col.Item().Element(c => ComposeEnteteBlocTableau(c, "SR"));
            col.Item().PaddingHorizontal(MmPt(0.2f)).Column(rows =>
            {
                rows.Item().Element(c => GrilleLigne(c, BuildSrLigneReference(p), BoxesSr));
                for (var i = 1; i < GridDataRows; i++)
                    rows.Item().Element(c => GrilleLigne(c, LigneVide(BoxesSr), BoxesSr));
            });
        });
    }

    private static void ComposeBlocCg(IContainer container, PieceCaisseDocumentDto p)
    {
        container.Column(col =>
        {
            col.Spacing(0);
            col.Item().Element(c => ComposeEnteteComptabiliteGenerale(c));
            col.Item().PaddingHorizontal(MmPt(0.2f)).Column(rows =>
            {
                rows.Item().Element(c =>
                    GrilleLigne(c, LigneCoinsReference("19", "26", BoxesComptabiliteGenerale), BoxesComptabiliteGenerale));
                for (var i = 1; i < GridDataRows; i++)
                    rows.Item().Element(c => GrilleLigne(c, LigneVide(BoxesComptabiliteGenerale), BoxesComptabiliteGenerale));
            });
        });
    }

    private static void ComposeBlocCpCpa(IContainer container, PieceCaisseDocumentDto p)
    {
        container.Column(col =>
        {
            col.Spacing(0);
            col.Item().Element(c => ComposeEnteteBlocTableau(c, "CP CPA"));
            col.Item().PaddingHorizontal(MmPt(0.2f)).Column(rows =>
            {
                rows.Item().Element(c =>
                    GrilleLigne(c, LigneCoinsReference("27", "32", BoxesCpCpa), BoxesCpCpa));
                for (var i = 1; i < GridDataRows; i++)
                    rows.Item().Element(c => GrilleLigne(c, LigneVide(BoxesCpCpa), BoxesCpCpa));
            });
        });
    }

    private static void ComposeBlocAppariement(IContainer container, PieceCaisseDocumentDto p)
    {
        container.Column(col =>
        {
            col.Spacing(0);
            col.Item().Element(c => ComposeEnteteBlocTableau(c, "Numero Appariement"));
            col.Item().PaddingHorizontal(MmPt(0.2f)).Column(rows =>
            {
                rows.Item().Element(c =>
                    GrilleLigne(c, LigneCoinsReference("33", "40", BoxesNumeroAppariement), BoxesNumeroAppariement));
                for (var i = 1; i < GridDataRows; i++)
                    rows.Item().Element(c => GrilleLigne(c, LigneVide(BoxesNumeroAppariement), BoxesNumeroAppariement));
            });
        });
    }

    private static void ComposeEnteteComptabiliteGenerale(IContainer container)
    {
        container.Height(EnteteTableauHauteur).Background(TableHeaderBlue).AlignCenter()
            .PaddingVertical(MmPt(0.15f)).PaddingHorizontal(MmPt(0.3f))
            .Column(col =>
            {
                col.Spacing(0);
                col.Item().AlignCenter().Text("COMPTABILITE").Bold().FontSize(5.5f).FontColor(Colors.Black);
                col.Item().AlignCenter().Text("GENERALE").Bold().FontSize(5.5f).FontColor(Colors.Black);
            });
    }

    private static void ComposeEnteteBlocTableau(IContainer container, string titre)
    {
        container.Height(EnteteTableauHauteur).Background(TableHeaderBlue).AlignCenter()
            .PaddingVertical(MmPt(0.25f)).PaddingHorizontal(MmPt(0.3f))
            .AlignMiddle()
            .Text(titre).Bold().FontSize(5.5f).FontColor(Colors.Black);
    }

    private static string[] BuildSrLigneReference(PieceCaisseDocumentDto p)
    {
        var sr = NormalizeValue(p.Sr);
        var row = LigneVide(BoxesSr);
        if (string.IsNullOrEmpty(sr))
            return row;
        row[0] = sr;
        row[2] = sr;
        return row;
    }

    private static string[] LigneCoinsReference(string gauche, string droite, int colonnes)
    {
        var row = LigneVide(colonnes);
        row[0] = gauche;
        row[colonnes - 1] = droite;
        return row;
    }

    private static string[] LigneVide(int colonnes)
        => Enumerable.Repeat(string.Empty, colonnes).ToArray();

    private static void GrilleLigne(IContainer container, IReadOnlyList<string> cellules, int boxCount)
    {
        container.AlignCenter().Row(row =>
        {
            for (var i = 0; i < boxCount; i++)
            {
                var val = i < cellules.Count ? cellules[i] : string.Empty;
                row.ConstantItem(CellW).Height(CellH)
                    .Border(0.4f).BorderColor(FieldBorder)
                    .Background(Colors.White)
                    .AlignCenter().AlignMiddle()
                    .Text(val).FontSize(CellFontSize).FontColor(Colors.Black);
            }
        });
    }

    private static void ComposeZoneRecu(IContainer container, PieceCaisseDocumentDto p)
    {
        var recu = ResolveRecuLabel(p.RecuSnel);

        // Label à l'intérieur du cartouche : 1 page A5, texte entier visible (sans hauteur explicite supplémentaire).
        container.AlignBottom().Width(ColRecuW)
            .Border(0.6f).BorderColor(FieldBorder).CornerRadius(MmPt(RecuBlocRadiusMm))
            .Background(Colors.White)
            .Column(col =>
            {
                col.Spacing(0);
                col.Item().PaddingLeft(MmPt(RecuLabelInsetMm)).PaddingTop(MmPt(0.15f))
                    .Row(labelRow =>
                    {
                        labelRow.AutoItem().Background(Colors.White).PaddingHorizontal(MmPt(0.45f))
                            .Text(recu).SemiBold().FontSize(7.0f).FontColor(LabelBlue).LineHeight(1.08f);
                    });
                col.Item().PaddingTop(MmPt(RecuLabelToCellsGapMm)).AlignCenter()
                    .Element(c => GrilleMontantChiffres(c, p.MontantFc));
                col.Item().PaddingTop(MmPt(0.12f)).AlignCenter()
                    .Text("(en chiffres)").FontSize(5.8f).FontColor(LabelBlue).Italic();
                col.Item().PaddingBottom(MmPt(RecuBlocPaddingMm));
            });
    }

    private static void GrilleMontantChiffres(IContainer container, decimal montant)
    {
        var digits = ((long)Math.Truncate(montant)).ToString(CultureInfo.InvariantCulture);

        container.AlignCenter().Row(row =>
        {
            for (var i = 0; i < BoxesRecuChiffres; i++)
            {
                var digitIndex = i - (BoxesRecuChiffres - digits.Length);
                var ch = digitIndex >= 0 ? digits[digitIndex].ToString() : string.Empty;
                row.ConstantItem(RecuCellW).Height(RecuCellH)
                    .Border(0.5f).BorderColor(FieldBorder)
                    .Background(Colors.White)
                    .AlignCenter().AlignMiddle()
                    .Text(ch).Bold().FontSize(7f).FontColor(Colors.Black);
            }
        });
    }

    private static void ComposeColonneMontantsFc(IContainer container, PieceCaisseDocumentDto p)
    {
        // Miroir vertical exact de la grille SR : étiquette → en-têtes → 4 lignes de données
        container.Column(col =>
        {
            col.Spacing(0);
            col.Item().Height(EtiquetteRowHauteur);
            col.Item().Height(EnteteTableauHauteur);
            col.Item().Height(CellH).AlignMiddle()
                .Element(c => LigneMontantFc(c, p.MontantFc));
            col.Item().Height(CellH).AlignMiddle()
                .Element(c => LigneMontantFc(c, 0m));
            col.Item().Height(CellH).AlignMiddle()
                .Element(c => LigneMontantFc(c, 0m));
            col.Item().Height(CellH);
        });
    }

    private static void LigneMontantFc(IContainer container, decimal montant)
    {
        container.Height(CellH).Width(ColFcTotalW).Row(row =>
        {
            row.ConstantItem(ColFcBadgeW).Height(CellH)
                .Background(HeaderBlue).AlignCenter().AlignMiddle()
                .Text("FC").Bold().FontSize(6.5f).FontColor(Colors.White);

            row.ConstantItem(MmPt(FcBadgeGapMm));

            row.ConstantItem(ColFcMontantW).Height(CellH)
                .Border(0.6f).BorderColor(FieldBorder).CornerRadius(MmPt(1.2f))
                .Background(Colors.White)
                .PaddingHorizontal(MmPt(0.8f))
                .AlignMiddle().AlignRight()
                .Text(FormatMontantFc(montant)).Bold().FontSize(7.5f).FontColor(Colors.Black);
        });
    }

    // ── Corps descriptif ──────────────────────────────────────────────────────

    private static void ComposeLigneSommeDe(IContainer container, PieceCaisseDocumentDto p)
    {
        var cartoucheH = MmPt(SommeDeCartoucheHMm);
        var innerPadH = MmPt(SommeDeCartouchePaddingHMm);
        var innerPadW = MmPt(SommeDeCartouchePaddingWMm);
        var innerBoxH = MmPt(SommeDeInnerBoxHMm);
        var gapBeforeFrancs = MmPt(SommeDeGapBeforeFrancsMm);
        var gapAfterFrancs = MmPt(SommeDeGapAfterFrancsMm);
        var francsBoxW = MmPt(FrancsBoxWMm);
        var montantLettres = p.MontantEnLettres.Trim().ToUpperInvariant();

        // Grand cartouche horizontal unique (modèle historique)
        container
            .Height(cartoucheH)
            .Border(0.6f).BorderColor(FieldBorder).CornerRadius(MmPt(SommeDeCartoucheRadiusMm))
            .Background(Colors.White)
            .PaddingHorizontal(innerPadW)
            .PaddingVertical(innerPadH)
            .AlignMiddle()
            .Row(row =>
            {
                row.Spacing(0);

                // Sous-row : hauteur unique imposée aux deux blocs (alignement haut/bas strict)
                row.AutoItem().Height(innerBoxH).Row(blocks =>
                {
                    blocks.Spacing(gapBeforeFrancs);

                    blocks.AutoItem().Height(innerBoxH).MinWidth(MmPt(SommeDeLabelMinWMm))
                        .Element(cell => cell
                            .Height(innerBoxH)
                            .Background(HeaderBlue).CornerRadius(MmPt(2f))
                            .AlignMiddle().AlignCenter()
                            .Text("La somme de").Bold().FontSize(7.4f).FontColor(Colors.White));

                    blocks.ConstantItem(francsBoxW).Height(innerBoxH)
                        .Element(cell => cell
                            .Height(innerBoxH)
                            .Border(0.6f).BorderColor(FieldBorder).CornerRadius(MmPt(1.5f))
                            .Background(Colors.White)
                            .AlignMiddle().AlignCenter()
                            .Column(francs =>
                            {
                                francs.Spacing(MmPt(0.14f));
                                francs.Item().AlignCenter()
                                    .Text("FRANCS").SemiBold().FontSize(6.2f).FontColor(LabelBlue).LineHeight(1.06f);
                                francs.Item().AlignCenter()
                                    .Text("CONGOLAIS").SemiBold().FontSize(6.2f).FontColor(LabelBlue).LineHeight(1.06f);
                            }));
                });

                row.ConstantItem(gapAfterFrancs);

                row.RelativeItem().AlignMiddle()
                    .Text(montantLettres).Bold().FontSize(8.2f).FontColor(Colors.Black).LineHeight(1.06f);
            });
    }

    private static void ComposeLigneMotif(IContainer container, PieceCaisseDocumentDto p)
    {
        container.Text(text =>
        {
            text.DefaultTextStyle(x => x.LineHeight(1.25f));
            text.Span("- MOTIF : ").SemiBold().FontSize(8.5f).FontColor(LabelBlue);
            text.Span(p.Motif.Trim().ToUpperInvariant()).FontSize(8.5f).FontColor(Colors.Black);
        });
    }

    private static void ComposeLignePieceJustificative(IContainer container, PieceCaisseDocumentDto p)
    {
        var pj = DisplayOptional(p.PieceJustificative);
        container.Text(text =>
        {
            text.DefaultTextStyle(x => x.LineHeight(1.25f));
            text.Span("- PIECE JUSTIFICATIVE : ").SemiBold().FontSize(8.5f).FontColor(LabelBlue);
            if (!string.IsNullOrWhiteSpace(pj))
                text.Span(pj.ToUpperInvariant()).FontSize(8.5f).FontColor(Colors.Black);
        });
    }

    // ── Pied de page ──────────────────────────────────────────────────────────

    private static void ComposePiedDePage(IContainer container, PieceCaisseDocumentDto p)
    {
        var bandH = MmPt(PiedDePageHauteurMm);
        var qrColW = MmPt(QrColWMm);

        container
            .Height(bandH)
            .Row(row =>
            {
                row.Spacing(MmPt(1.5f));

                row.ConstantItem(qrColW).Height(bandH).AlignBottom()
                    .Element(c => ComposeQr(c, p.IdentifiantVerification));

                row.RelativeItem().Height(bandH).AlignBottom().AlignCenter()
                    .PaddingBottom(MmPt(0.8f))
                    .Text("BON A PAYER").Bold().FontSize(12f).FontColor(BonAPayerBlue);

                row.ConstantItem(PourAcquitW).Height(bandH).AlignBottom()
                    .Element(c => ComposePourAcquit(c, p));
            });
    }

    private static void ComposeQr(IContainer container, string identifiantVerification)
    {
        DocumentInstrumentPerf.TimeQr(() =>
        {
        var payload = string.IsNullOrWhiteSpace(identifiantVerification)
            ? "SNEL-PIECE-CAISSE"
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
                col.Item().PaddingTop(MmPt(QrLabelGapMm))
                    .Width(MmPt(QrColWMm))
                    .Text(identifiantVerification.Trim())
                    .FontSize(4.8f).FontColor(Colors.Grey.Darken1).LineHeight(1f);
            }
        });
        });
    }

    private static void ComposePourAcquit(IContainer container, PieceCaisseDocumentDto p)
    {
        var nom = DisplayOptional(p.BeneficiaireAffichage);
        var matricule = ComposeIdentiteMatricule(p.BeneficiaireMatricule, p.BeneficiaireIdentite);

        container.Border(0.6f).BorderColor(FieldBorder).Column(col =>
        {
            col.Item().Background(HeaderBlue).PaddingVertical(MmPt(0.28f)).AlignCenter()
                .Text("POUR ACQUIT").Bold().FontSize(7f).FontColor(Colors.White).LineHeight(1f);

            col.Item().PaddingHorizontal(MmPt(0.7f)).PaddingVertical(MmPt(0.18f)).Column(fields =>
            {
                fields.Spacing(0);
                fields.Item().Element(c => LigneAcquit(c, "NOM - POSTNOM", nom, MmPt(0.95f)));
                fields.Item().Element(c => LigneAcquit(c, "IDENTITE / MATRICULE", matricule, MmPt(0.95f)));
                fields.Item().Element(c => LigneAcquit(c, "SIGNATURE", string.Empty, MmPt(2.05f)));
            });
        });
    }

    private static void LigneAcquit(IContainer container, string label, string value, float underlineMinHeight)
    {
        container.PaddingVertical(MmPt(0.06f)).Column(col =>
        {
            col.Spacing(0);
            col.Item().Row(row =>
            {
                row.AutoItem().AlignTop()
                    .Text($"{label} : ").SemiBold().FontSize(7f).FontColor(LabelBlue).LineHeight(1f);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    row.RelativeItem().AlignTop()
                        .Text(ForPdfInlineDisplay(value)).FontSize(7.5f).FontColor(Colors.Black).LineHeight(1.05f);
                }
            });
            col.Item().PaddingTop(MmPt(0.06f)).MinHeight(underlineMinHeight)
                .BorderBottom(0.45f).BorderColor(Colors.Grey.Lighten1);
        });
    }

    // ── Utilitaires ───────────────────────────────────────────────────────────

    private static string ResolveRecuLabel(string? recuSnel)
    {
        if (string.IsNullOrWhiteSpace(recuSnel) || EstLibelleRecuPlaceholder(recuSnel))
            return RecuSnelDefaut;
        return recuSnel.Trim().ToUpperInvariant();
    }

    /// <summary>Valeurs placeholder en base (ex. « - ») → libellé institutionnel par défaut.</summary>
    private static bool EstLibelleRecuPlaceholder(string recuSnel)
    {
        var t = recuSnel.Trim();
        return t is "-" or "—" or "–" or "N/A" or "." or ".." or "...";
    }

    private static string ResolveIdDisplay(string referenceDemande)
    {
        if (string.IsNullOrWhiteSpace(referenceDemande))
            return string.Empty;
        var trimmed = referenceDemande.Trim();
        if (trimmed.StartsWith('R') && trimmed.Length > 1)
            return trimmed[1..];
        return trimmed;
    }

    private static string NormalizeValue(string? value)
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
