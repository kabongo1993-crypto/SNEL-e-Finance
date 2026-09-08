using System.Globalization;
using System.Reflection;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

/// <summary>
/// Rendu PDF officiel SNEL — en-tête / filigrane / pied de page alignés sur enTETE_SNEL.
/// </summary>
public class QuestPdfDocumentRenderer : IDocumentPdfRenderer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly Lazy<byte[]?> LogoBytes = new(() => LoadAssetBytes("snel-logo.png"));
    private static readonly Lazy<byte[]?> WatermarkBytes = new(() => LoadAssetBytes("snel-logo-watermark.png") ?? LoadAssetBytes("snel-logo.png"));

    static QuestPdfDocumentRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(DocumentPrevisionPayloadDto payload)
        => BuildDocument(payload).GeneratePdf();

    public IReadOnlyList<byte[]> RenderPngPages(DocumentPrevisionPayloadDto payload, int rasterDpi = 110)
    {
        _ = rasterDpi;
        var temp = Path.Combine(Path.GetTempPath(), "snel-doc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            BuildDocument(payload).GenerateImages(index => Path.Combine(temp, $"p{index + 1:00}.png"));
            return Directory.GetFiles(temp, "*.png")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllBytes)
                .ToList();
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { /* ignore */ }
        }
    }

    private static Document BuildDocument(DocumentPrevisionPayloadDto payload)
    {
        var titre = string.IsNullOrWhiteSpace(payload.Titre)
            ? DocumentPrevisionType.TitreCourt(payload.TypeDocument)
            : payload.Titre;
        var action = string.IsNullOrWhiteSpace(payload.ActionLibelle)
            ? DocumentPrevisionType.ActionLibelle(payload.TypeDocument)
            : payload.ActionLibelle;
        var statut = string.IsNullOrWhiteSpace(payload.StatutAffiche)
            ? DocumentPrevisionType.StatutAffiche(payload.TypeDocument)
            : payload.StatutAffiche;
        var signature = string.IsNullOrWhiteSpace(payload.SignatureLibelle)
            ? DocumentPrevisionType.SignatureRole(payload.TypeDocument)
            : payload.SignatureLibelle;
        var isDept = string.Equals(payload.Portee, DocumentPrevisionPortee.Departement, StringComparison.OrdinalIgnoreCase);
        var logo = LogoBytes.Value;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginLeft(36);
                page.MarginRight(36);
                page.MarginTop(28);
                page.MarginBottom(72);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                var watermark = WatermarkBytes.Value;
                if (watermark is { Length: > 0 })
                {
                    page.Background().AlignCenter().AlignMiddle()
                        .Width(300).Height(300).Image(watermark).FitArea();
                }

                page.Header().Element(h => ComposeHeader(h, payload, logo));
                page.Content().PaddingTop(8).Element(c =>
                    ComposeContent(c, payload, titre, action, statut, signature, isDept));
                page.Footer().Element(f => ComposeFooter(f, payload));
            });
        });
    }

    private static void ComposeHeader(IContainer container, DocumentPrevisionPayloadDto payload, byte[]? logo)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(56).Element(e =>
                {
                    if (logo is { Length: > 0 })
                        e.Height(48).Image(logo).FitArea();
                });
                row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(c =>
                {
                    c.Item().Text("Société Nationale d'Electricité").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                    c.Item().Text("SNEL").SemiBold().FontSize(10).FontColor(Colors.Blue.Darken2);
                });
                row.ConstantItem(120).AlignRight().AlignMiddle().Column(c =>
                {
                    c.Item().Text("Kinshasa, le").FontSize(8);
                    c.Item().Text(payload.DateEvenement.ToString("dd/MM/yyyy", Fr)).Bold().FontSize(9);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1.2f).LineColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(1).LineHorizontal(0.5f).LineColor(Colors.Yellow.Darken2);
        });
    }

    private static void ComposeContent(
        IContainer container,
        DocumentPrevisionPayloadDto payload,
        string titre,
        string action,
        string statut,
        string signature,
        bool isDept)
    {
        container.Column(col =>
        {
            col.Spacing(6);

            col.Item().AlignCenter().Text(titre).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
            col.Item().AlignCenter().Text($"Référence : {payload.Reference}").SemiBold().FontSize(9);
            col.Item().AlignCenter().Text($"STATUT : {statut}").Bold().FontSize(10);
            col.Item().AlignCenter().Text(action).FontSize(8).FontColor(Colors.Grey.Darken2);

            col.Item().PaddingTop(4).Border(0.75f).BorderColor(Colors.Grey.Medium).Padding(8).Column(box =>
            {
                box.Spacing(2);
                box.Item().Text("IDENTIFICATION DU DOSSIER BUDGÉTAIRE").Bold().FontSize(9);
                box.Item().Text($"Exercice : {payload.AnneeExercice}");
                box.Item().Text($"Version : V{payload.NumeroVersion:00}" +
                                (string.IsNullOrWhiteSpace(payload.LibelleVersion) ? "" : $" — {payload.LibelleVersion}"));
                var types = new List<string>();
                if (payload.MontantDC != 0) types.Add("DC");
                if (payload.MontantAE != 0) types.Add("AE");
                if (payload.MontantBI != 0) types.Add("BI");
                if (types.Count == 0) types.Add("—");
                box.Item().Text($"Type de budget : {string.Join(" / ", types)}");
                box.Item().Text($"Département : {payload.CodeDepartement} — {payload.LibelleDepartement}");
                if (!isDept)
                {
                    box.Item().Text($"Unité budgétaire : {payload.LibelleUB}");
                    box.Item().Text($"Code UB : {payload.CodeUB}");
                }

                box.Item().Text(isDept ? "PORTÉE : DÉPARTEMENT" : "PORTÉE : UNITÉ BUDGÉTAIRE").Bold();
                if (isDept)
                    box.Item().Text($"NOMBRE D'UB : {payload.NbUbConcernees}").Bold();
                box.Item().Text("Devise : USD").Bold();
                box.Item().Text($"Date de l'opération : {payload.DateEvenement:dd/MM/yyyy HH:mm}");
                box.Item().Text($"Utilisateur / acteur : {payload.NomUtilisateurAuteur}" +
                                (string.IsNullOrWhiteSpace(payload.RoleActeur) ? "" : $" ({payload.RoleActeur})"));
                box.Item().Text($"Référence documentaire : {payload.Reference}");
                if (!string.IsNullOrWhiteSpace(payload.CodeMode))
                    box.Item().Text($"Mode de prévision : {payload.CodeMode}");
            });

            if (!string.IsNullOrWhiteSpace(payload.Motif))
            {
                col.Item().PaddingTop(4).Border(1.5f).BorderColor(Colors.Red.Medium).Background(Colors.Red.Lighten5).Padding(10).Column(m =>
                {
                    m.Item().Text("MOTIF DU REJET").Bold().FontSize(11).FontColor(Colors.Red.Darken2);
                    m.Item().PaddingTop(4).Text(payload.Motif!).FontSize(10);
                });
            }

            col.Item().PaddingTop(6).Text("SYNTHÈSE BUDGÉTAIRE (USD)").Bold().FontSize(10);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });
                void Row(string k, string v, bool bold = false)
                {
                    t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(k).SemiBold();
                    var cell = t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).AlignRight();
                    if (bold) cell.Text(v).Bold();
                    else cell.Text(v);
                }

                Row("Montant DC", FormatUsd(payload.MontantDC));
                Row("Montant AE", FormatUsd(payload.MontantAE));
                Row("Montant BI", FormatUsd(payload.MontantBI));
                Row("TOTAL", FormatUsd(payload.MontantTotal), bold: true);
            });

            if (isDept && payload.UbConcernees.Count > 0)
            {
                col.Item().PaddingTop(8).Text("DÉTAIL DES UNITÉS BUDGÉTAIRES").Bold().FontSize(10);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(55);
                        c.RelativeColumn(3);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.4f);
                    });
                    HeaderCell(t, "Code UB");
                    HeaderCell(t, "Libellé");
                    HeaderCell(t, "DC (USD)");
                    HeaderCell(t, "AE (USD)");
                    HeaderCell(t, "BI (USD)");
                    HeaderCell(t, "TOTAL (USD)");
                    foreach (var ub in payload.UbConcernees)
                    {
                        BodyCell(t, ub.CodeUB);
                        BodyCell(t, ub.LibelleUB);
                        BodyCell(t, FormatUsdNumber(ub.MontantDC), right: true);
                        BodyCell(t, FormatUsdNumber(ub.MontantAE), right: true);
                        BodyCell(t, FormatUsdNumber(ub.MontantBI), right: true);
                        BodyCell(t, FormatUsdNumber(ub.MontantTotal), right: true);
                    }
                });
            }

            if (!isDept && payload.LignesDetail.Count > 0)
            {
                col.Item().PaddingTop(8).Text($"DÉTAIL DES PRÉVISIONS ({payload.Devise})").Bold().FontSize(10);
                ComposeDetailTable(col, payload);
            }

            col.Item().PaddingTop(18).AlignCenter().Column(sig =>
            {
                sig.Item().Text(signature).Bold().FontSize(9);
                sig.Item().PaddingTop(28).Text("____________________").AlignCenter();
                sig.Item().Text(payload.NomUtilisateurAuteur).AlignCenter().FontSize(9);
                if (!string.IsNullOrWhiteSpace(payload.RoleActeur))
                    sig.Item().Text(payload.RoleActeur!).AlignCenter().FontSize(8).FontColor(Colors.Grey.Darken1);
                sig.Item().Text($"Date : {payload.DateEvenement:dd/MM/yyyy}").AlignCenter().FontSize(8);
            });
        });
    }

    private static void ComposeDetailTable(ColumnDescriptor col, DocumentPrevisionPayloadDto payload)
    {
        var mensuel = payload.EstMensuel || payload.LignesDetail.Any(l => l.EstMensuel && !l.EstGroupe);
        col.Item().Table(t =>
        {
            if (mensuel)
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(42);
                    c.RelativeColumn(2.2f);
                    c.ConstantColumn(48);
                    for (var i = 0; i < 12; i++) c.ConstantColumn(28);
                });
                HeaderCell(t, "RB");
                HeaderCell(t, "Désignation");
                HeaderCell(t, "Cumul");
                foreach (var m in new[] { "Jan", "Fév", "Mar", "Avr", "Mai", "Jun", "Jul", "Aoû", "Sep", "Oct", "Nov", "Déc" })
                    HeaderCell(t, m);
            }
            else
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(55);
                    c.RelativeColumn(4);
                    c.RelativeColumn(1.5f);
                });
                HeaderCell(t, "RB");
                HeaderCell(t, "Désignation");
                HeaderCell(t, "Montant annuel (USD)");
            }

            foreach (var l in payload.LignesDetail)
            {
                if (l.EstGroupe)
                {
                    var bg = Colors.Blue.Lighten4;
                    if (mensuel)
                    {
                        t.Cell().ColumnSpan(15).Background(bg).Padding(3).Text($"{l.Code} — {l.Designation}").Bold().FontSize(8);
                    }
                    else
                    {
                        t.Cell().ColumnSpan(3).Background(bg).Padding(3).Text($"{l.Code} — {l.Designation}").Bold().FontSize(8);
                    }

                    continue;
                }

                BodyCell(t, l.Code, bold: false);
                BodyCell(t, l.Designation);
                BodyCell(t, FormatUsdNumber(l.MontantAnnuel), right: true);
                if (mensuel)
                {
                    BodyCell(t, FormatUsdNumber(l.M01), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M02), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M03), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M04), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M05), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M06), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M07), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M08), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M09), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M10), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M11), right: true, tiny: true);
                    BodyCell(t, FormatUsdNumber(l.M12), right: true, tiny: true);
                }
            }
        });
    }

    private static void ComposeFooter(IContainer container, DocumentPrevisionPayloadDto payload)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.8f).LineColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(4).DefaultTextStyle(x => x.FontSize(6.5f)).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("SIEGE SOCIAL : KINSHASA").Bold();
                    c.Item().Text("2381, Avenue de la Justice");
                    c.Item().Text("B.P. 500 KINSHASA / GOMBE");
                    c.Item().Text("N.R.C. N° 6976 Kinshasa");
                    c.Item().Text("ID. NAT. : A 03970 O");
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("TELEX : 21347 SNEL RDC");
                    c.Item().Text("Tél. : + (243 12) 33 684 / 33 665");
                    c.Item().Text("Fax : + 243 12 33735");
                    c.Item().Text("+ 243 871 682 622 677");
                    c.Item().Text("E-mail : sneldg@ic.cd");
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("UBC : 201 – 134 502 - 10");
                    c.Item().Text("B.C.D : 901 – 001 42 02 – 34");
                    c.Item().Text("BCDC : 101 – 095 18 51 – 03");
                    c.Item().Text("Citibank : 831 – 300 026 – 001");
                    c.Item().Text("BCCE : 301– 0019530 – 17");
                    c.Item().Text("Sbic Bank : 101 091 093 70 01");
                });
            });
            col.Item().PaddingTop(3).Row(r =>
            {
                r.RelativeItem().Text(t =>
                {
                    t.Span("Document généré par SNEL e-Finance · ").FontSize(7);
                    t.Span(payload.Reference).FontSize(7).SemiBold();
                });
                r.ConstantItem(70).AlignRight().Text(t =>
                {
                    t.Span("Page ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" / ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        });
    }

    private static void HeaderCell(TableDescriptor t, string text)
        => t.Cell().Background(Colors.Blue.Darken3).Padding(3)
            .Text(text).FontColor(Colors.White).Bold().FontSize(7);

    private static void BodyCell(TableDescriptor t, string text, bool right = false, bool bold = false, bool tiny = false)
    {
        var cell = t.Cell().Border(0.4f).BorderColor(Colors.Grey.Lighten1).Padding(2);
        if (right) cell = cell.AlignRight();
        var style = cell.Text(text).FontSize(tiny ? 6.5f : 7.5f);
        if (bold) style.Bold();
    }

    public static string FormatUsd(decimal n) => FormatUsdNumber(n) + " USD";

    public static string FormatUsdNumber(decimal n)
        => string.Format(Fr, "{0:N2}", n);

    private static byte[]? LoadAssetBytes(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name is not null)
        {
            using var s = asm.GetManifestResourceStream(name);
            if (s is null) return null;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Documents", "Assets", fileName);
        if (File.Exists(path)) return File.ReadAllBytes(path);
        var alt = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "Assets", fileName));
        return File.Exists(alt) ? File.ReadAllBytes(alt) : null;
    }
}
