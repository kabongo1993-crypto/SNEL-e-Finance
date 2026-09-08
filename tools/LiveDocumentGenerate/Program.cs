using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Documents;
using BudgetWeb.Infrastructure.Persistence;
using BudgetWeb.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var outDir = @"D:\InstantFLOW\Instant_Flow\tmp_pdf_review\live";
Directory.CreateDirectory(outDir);

var cs = "Server=localhost\\HEROS_SQL19;Database=BD_SNEL;Trusted_Connection=True;TrustServerCertificate=True;";
var opts = new DbContextOptionsBuilder<BudgetDbContext>().UseSqlServer(cs).Options;
await using var ctx = new BudgetDbContext(opts);

var repo = new DocumentPrevisionRepository(ctx);
var store = new LocalDocumentFileStore(CreateConfig());
var pdf = new QuestPdfDocumentRenderer();
var svc = new DocumentPrevisionService(repo, pdf, store);

var adminId = await ctx.Utilisateurs.AsNoTracking()
    .Where(u => u.NomUtilisateur == "admin.snel")
    .Select(u => u.IdUtilisateur)
    .FirstAsync();

const long idVersion = 4;
var sw = System.Diagnostics.Stopwatch.StartNew();

async Task Save(string label, BudgetWeb.Application.DTOs.DocumentPrevisionDto doc)
{
    var bytes = await svc.GetPdfBytesAsync(doc.IdDocument);
    var path = Path.Combine(outDir, $"{label}_{doc.Reference.Replace('/', '_')}.pdf");
    if (bytes is not null) await File.WriteAllBytesAsync(path, bytes);
    Console.WriteLine($"{label}: {doc.Reference} id={doc.IdDocument} size={doc.TailleOctets} path={path}");
}

var sub = await svc.GenererSoumissionUbAsync(idVersion, 2, adminId, null);
await Save("SUB", sub);
var rej = await svc.GenererRejetUbAsync(idVersion, 543, adminId, "Motif de rejet institutionnel — test rendu officiel.", "SOUMISE", null);
await Save("REJ", rej);
var ctl = await svc.GenererControleUbAsync(idVersion, 2, adminId, null);
await Save("CTL", ctl);
var val = await svc.GenererValidationUbAsync(idVersion, 3, adminId, null);
await Save("VAL", val);

// Département synthesis (UB DAM)
var deptDam = await ctx.UnitesBudgetaires.AsNoTracking()
    .Where(u => u.CodeUB == "H00000")
    .Select(u => u.FK_Departement)
    .FirstAsync();
var ubDam = await ctx.UnitesBudgetaires.AsNoTracking()
    .Where(u => u.FK_Departement == deptDam)
    .Select(u => u.IdUB)
    .Take(5)
    .ToListAsync();
var subDept = await svc.GenererSoumissionDepartementAsync(idVersion, deptDam, adminId, null, ubDam);
if (subDept is not null) await Save("SUB_DEPT", subDept);

Console.WriteLine($"Done in {sw.ElapsedMilliseconds} ms");

static IConfiguration CreateConfig()
{
    Environment.SetEnvironmentVariable(
        "Documents__PrevisionsPath",
        Path.Combine(@"D:\InstantFLOW\Instant_Flow\tmp_pdf_review\live_store"));
    return new ConfigurationBuilder().AddEnvironmentVariables().Build();
}
