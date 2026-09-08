using System.Diagnostics;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Documents;

var outDir = args.Length > 0 ? args[0] : @"D:\InstantFLOW\Instant_Flow\tmp_pdf_review";
Directory.CreateDirectory(outDir);

var renderer = new QuestPdfDocumentRenderer();
var sw = Stopwatch.StartNew();

void Write(string name, DocumentPrevisionPayloadDto payload)
{
    var t0 = Stopwatch.GetTimestamp();
    var bytes = renderer.Render(payload);
    var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    var path = Path.Combine(outDir, name);
    File.WriteAllBytes(path, bytes);
    Console.WriteLine($"{name}: {bytes.Length} bytes in {ms:F0} ms · {payload.Reference}");

    var pngs = renderer.RenderPngPages(payload, 100);
    for (var i = 0; i < pngs.Count; i++)
    {
        var pngPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(name) + $"_p{i + 1}.png");
        File.WriteAllBytes(pngPath, pngs[i]);
        Console.WriteLine($"  preview: {Path.GetFileName(pngPath)} ({pngs[i].Length} bytes)");
    }
}

var detail = new List<DocumentPrevisionLigneDetailDto>
{
    new(true, "00", "ACHAT ET VARIATIONS DE STOCKS", "DC", 0, false),
    new(false, "00100", "Achats énergie électrique", "DC", 12_500_000.50m, false),
    new(false, "00110", "Achats fournitures", "DC", 1_000m, false),
    new(true, "01", "TRANSPORTS", "DC", 0, false),
    new(false, "01201", "Transport personnel", "DC", 0m, false),
};

Write("SUB_UB.pdf", Base("SUB", "SNEL/DG/BUD/2026/V01/SUB/00099", DocumentPrevisionPortee.Ub, detail));
Write("REJ_UB.pdf", Base("REJ", "SNEL/DG/BUD/2026/V01/REJ/00099", DocumentPrevisionPortee.Ub, detail) with
{
    Motif = "Montants incohérents par rapport à l'enveloppe départementale.",
    StatutAffiche = DocumentPrevisionType.StatutAffiche("REJ"),
});
Write("CTL_UB.pdf", Base("CTL", "SNEL/DG/BUD/2026/V01/CTL/00099", DocumentPrevisionPortee.Ub, detail));
Write("VAL_UB.pdf", Base("VAL", "SNEL/DG/BUD/2026/V01/VAL/00099", DocumentPrevisionPortee.Ub, detail));

var ubs = Enumerable.Range(1, 8).Select(i => new DocumentPrevisionUbLigneDto(
    i, $"UB{i:000}", $"Unité budgétaire {i}", 1000m * i, 500m * i, 200m * i, 1700m * i)).ToList();
Write("SUB_DEPT.pdf", Base("SUB", "SNEL/DG/BUD/2026/V01/SUB/00100", DocumentPrevisionPortee.Departement, [], ubs, 8));

var mensuel = new List<DocumentPrevisionLigneDetailDto>
{
    new(true, "00", "ACHAT ET VARIATIONS DE STOCKS", "DC", 0, true),
    new(false, "00100", "Achats énergie", "DC", 12000m, true,
        1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000),
};
Write("SUB_MENSUEL.pdf", Base("SUB", "SNEL/DG/BUD/2026/V01/SUB/00101", DocumentPrevisionPortee.Ub, mensuel) with
{
    EstMensuel = true,
    CodeMode = "MENSUEL",
});

Console.WriteLine($"Formats: 0={QuestPdfDocumentRenderer.FormatUsd(0)} | 1000={QuestPdfDocumentRenderer.FormatUsd(1000)} | 12500000={QuestPdfDocumentRenderer.FormatUsd(12_500_000)} | 12500000.50={QuestPdfDocumentRenderer.FormatUsd(12_500_000.50m)}");
Console.WriteLine($"Total elapsed {sw.ElapsedMilliseconds} ms");

static DocumentPrevisionPayloadDto Base(
    string type,
    string reference,
    string portee,
    IReadOnlyList<DocumentPrevisionLigneDetailDto> lignes,
    IReadOnlyList<DocumentPrevisionUbLigneDto>? ubs = null,
    int nb = 1)
    => new(
        DocumentPrevisionType.Titre(type),
        reference,
        type,
        portee,
        nb,
        2026,
        1,
        1,
        "Version initiale",
        1,
        "DG",
        "Direction Générale",
        portee == DocumentPrevisionPortee.Ub ? 10 : null,
        portee == DocumentPrevisionPortee.Ub ? "UB001" : null,
        portee == DocumentPrevisionPortee.Ub ? "Unité budgétaire test" : null,
        DateTime.Now,
        1,
        "Admin SNEL",
        null,
        DocumentPrevisionType.StatutAffiche(type),
        type == "REJ" ? "Motif test" : null,
        null,
        12_500_000.50m,
        1_000m,
        0m,
        12_501_000.50m,
        ubs ?? [])
    {
        Devise = "USD",
        ActionLibelle = DocumentPrevisionType.ActionLibelle(type),
        StatutAffiche = DocumentPrevisionType.StatutAffiche(type),
        SignatureLibelle = DocumentPrevisionType.SignatureRole(type),
        RoleActeur = type switch
        {
            "CTL" => "Contrôleur",
            "VAL" => "Validateur",
            "REJ" => "Responsable",
            _ => "Utilisateur",
        },
        LignesDetail = lignes,
        EstMensuel = false,
        CodeMode = "ANNUEL",
    };
