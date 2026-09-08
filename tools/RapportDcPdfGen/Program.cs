using System.Data.Common;
using System.Diagnostics;
using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Documents;
using BudgetWeb.Infrastructure.Persistence;
using BudgetWeb.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var outDir = @"D:\InstantFLOW\Instant_Flow\tmp_pdf_review\rapports_dc_v2";
Directory.CreateDirectory(outDir);
var imgDir = Path.Combine(outDir, "png");
Directory.CreateDirectory(imgDir);

var cs = "Server=localhost\\HEROS_SQL19;Database=BD_SNEL;Trusted_Connection=True;TrustServerCertificate=True;";
var queryCount = 0;
var opts = new DbContextOptionsBuilder<BudgetDbContext>()
    .UseSqlServer(cs)
    .AddInterceptors(new CountingInterceptor(() => Interlocked.Increment(ref queryCount)))
    .Options;
await using var ctx = new BudgetDbContext(opts);
var repo = new RapportPrevisionDcRepository(ctx);
var resolver = new RapportOrganisationResolver();
var pdf = new RapportPrevisionDcPdfRenderer();
var svc = new RapportPrevisionDcService(repo, resolver, pdf);

var idVersion = 4L;
var structures = await repo.GetStructuresAsync([]);
var allUbs = await repo.GetUbsActivesAsync();
var resolutions = resolver.ResolveAll(allUbs, structures);

var dcUbIds = await ctx.PrevisionsBudgetaires.AsNoTracking()
    .Where(p => p.FK_VersionBudgetaire == idVersion && p.TypeBudget.CodeType == "DC" && p.MontantAnnuel != 0)
    .Select(p => p.FK_UniteBudgetaire).Distinct().ToListAsync();
var wf = await ctx.WorkflowsPrevisionUb.AsNoTracking()
    .Where(w => w.FK_VersionBudgetaire == idVersion)
    .Select(w => new { w.FK_UniteBudgetaire, w.Statut }).ToListAsync();

var candidates = dcUbIds
    .Select(id => {
        var st = wf.FirstOrDefault(w => w.FK_UniteBudgetaire == id)?.Statut;
        resolutions.TryGetValue(id, out var r);
        return new { id, st, r };
    })
    .Where(x => x.st != null && x.r?.IdEntite != null)
    .GroupBy(x => new { x.r!.IdEntite, x.st })
    .Select(g => new { g.Key.IdEntite, Statut = g.Key.st!, Count = g.Count(), UbIds = g.Select(x => x.id).ToList() })
    .OrderByDescending(x => x.Count)
    .ToList();

foreach (var c in candidates.Take(5))
{
    var e = structures[c.IdEntite!.Value];
    Console.WriteLine($"CANDIDATE entite={e.Code} statut={c.Statut} ubDc={c.Count}");
}

var best = candidates.First();
var entite = structures[best.IdEntite!.Value];
var statut = best.Statut!;
Console.WriteLine($"SELECTED {entite.Code} / {statut}");

async Task Gen(string name, RapportDcQuery q)
{
    queryCount = 0;
    var sw = Stopwatch.StartNew();
    var dto = await svc.GetAsync(q);
    var bytes = pdf.Render(dto);
    sw.Stop();
    await File.WriteAllBytesAsync(Path.Combine(outDir, name), bytes);
    Console.WriteLine($"{name}: bytes={bytes.Length} layout={dto.LayoutColonnes} ub={dto.BlocsUb.Count} total={dto.EnTete.MontantTotalDc} q={queryCount} ms={sw.ElapsedMilliseconds}");
    pdf.RenderPreviewImages(dto, Path.Combine(imgDir, Path.GetFileNameWithoutExtension(name)));
}

await Gen("01_ENTITE_MULTI_UB.pdf", new RapportDcQuery(idVersion, "ENTITE", entite.IdStructure, null, null, null, statut));

var deptId = resolutions.Values
    .Where(r => r.IdEntite == entite.IdStructure && r.IdStructureDepartement != null && best.UbIds.Contains(r.IdUB))
    .GroupBy(r => r.IdStructureDepartement)
    .OrderByDescending(g => g.Count())
    .Select(g => g.Key)
    .FirstOrDefault();
if (deptId is long dId)
{
    var dept = structures[dId];
    Console.WriteLine($"DEPT {dept.Code}");
    await Gen("02_DEPARTEMENT_MULTI_UB.pdf", new RapportDcQuery(idVersion, "DEPARTEMENT", entite.IdStructure, dId, null, null, statut));
}

var mixte = await ctx.PrevisionsBudgetaires.AsNoTracking()
    .Where(p => p.FK_VersionBudgetaire == idVersion && p.TypeBudget.CodeType == "DC" && best.UbIds.Contains(p.FK_UniteBudgetaire))
    .GroupBy(p => p.FK_UniteBudgetaire)
    .Select(g => new { IdUB = g.Key, Modes = g.Select(x => x.ModePrevision.CodeMode.ToUpper()).Distinct().Count() })
    .Where(x => x.Modes >= 2)
    .FirstOrDefaultAsync();
var ubId = mixte?.IdUB ?? best.UbIds.First();
var resUb = resolutions[ubId];
await Gen("03_UB.pdf", new RapportDcQuery(idVersion, "UB", resUb.IdEntite, resUb.IdStructureDepartement, null, ubId, statut));

foreach (var f in Directory.GetFiles(outDir, "*.pdf"))
{
    using var doc = UglyToad.PdfPig.PdfDocument.Open(f);
    Console.WriteLine($"PDFPIG {Path.GetFileName(f)} pages={doc.NumberOfPages}");
}

sealed class CountingInterceptor(Action onCommand) : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { onCommand(); return base.ReaderExecuting(command, eventData, result); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { onCommand(); return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
}
