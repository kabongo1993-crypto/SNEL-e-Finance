using System.Data.Common;
using System.Diagnostics;
using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Documents;
using BudgetWeb.Infrastructure.Persistence;
using BudgetWeb.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var outDir = @"D:\InstantFLOW\Instant_Flow\tmp_pdf_review\rapports_dc_consolide";
Directory.CreateDirectory(outDir);

var cs = "Server=localhost\\HEROS_SQL19;Database=BD_SNEL;Trusted_Connection=True;TrustServerCertificate=True;";
var queryCount = 0;
var opts = new DbContextOptionsBuilder<BudgetDbContext>()
    .UseSqlServer(cs)
    .AddInterceptors(new CountingInterceptor(() => Interlocked.Increment(ref queryCount)))
    .Options;
await using var ctx = new BudgetDbContext(opts);

var repo = new RapportPrevisionDcRepository(ctx);
var resolver = new RapportOrganisationResolver();
var pdf = new RapportPrevisionDcConsolidePdfRenderer();
var svc = new RapportPrevisionDcConsolideService(repo, resolver, pdf);

const long idVersion = 4;
var structures = await repo.GetStructuresAsync([]);
var ac = structures.Values.First(s => s.Code == "AC" && s.TypeStructure.Equals("ENTITE", StringComparison.OrdinalIgnoreCase));
var dam = structures.Values.First(s => s.Code.Contains("DAM", StringComparison.OrdinalIgnoreCase)
    && s.TypeStructure.Equals("DEPARTEMENT", StringComparison.OrdinalIgnoreCase)
    && s.ParentId == ac.IdStructure);

async Task Gen(string name, RapportDcConsolideQuery q)
{
    queryCount = 0;
    var sw = Stopwatch.StartNew();
    var dto = await svc.GetAsync(q);
    var bytes = pdf.Render(dto);
    sw.Stop();
    await File.WriteAllBytesAsync(Path.Combine(outDir, name), bytes);
    Console.WriteLine($"{name}: {bytes.Length}b ub={dto.EnTete.NbUB} groupes={dto.Groupes.Count} total={dto.EnTete.MontantTotalDc} q={queryCount} ms={sw.ElapsedMilliseconds}");
    Console.WriteLine($"  titre={dto.EnTete.Titre}");
    pdf.RenderPreviewImages(dto, Path.Combine(outDir, "png", Path.GetFileNameWithoutExtension(name)));
}

await Gen("01_TOUTES_UB.pdf", new RapportDcConsolideQuery(idVersion, null, null, null, "VALIDEE"));
await Gen("02_ENTITE_AC.pdf", new RapportDcConsolideQuery(idVersion, ac.IdStructure, null, null, "VALIDEE"));
await Gen("03_DEPT_DAM.pdf", new RapportDcConsolideQuery(idVersion, ac.IdStructure, dam.IdStructure, null, "VALIDEE"));

foreach (var f in Directory.GetFiles(outDir, "*.pdf"))
{
    using var doc = UglyToad.PdfPig.PdfDocument.Open(f);
    var text = string.Join(" ", doc.GetPages().Select(p => p.Text));
    Console.WriteLine($"PDFPIG {Path.GetFileName(f)} pages={doc.NumberOfPages} SNEL={(text.Contains("SNEL")?"OK":"NO")} MENSUALISÉES={(text.Contains("MENSUALIS")?"OK":"NO")} USD={(text.Contains("USD")?"OK":"NO")}");
}

sealed class CountingInterceptor(Action onCommand) : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { onCommand(); return base.ReaderExecuting(command, eventData, result); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { onCommand(); return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
}
