using System.Collections.Concurrent;
using BudgetWeb.Infrastructure.Persistence;
using BudgetWeb.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var cs = args.Length > 0
    ? args[0]
    : "Server=localhost\\HEROS_SQL19;Database=BD_SNEL;Trusted_Connection=True;TrustServerCertificate=True;";

var annee = (short)2099;
var idDept = 999001L;
var numVersion = 99;
var type = "TST";

await using (var setup = Create(cs))
{
    await setup.Database.ExecuteSqlInterpolatedAsync($@"
DELETE FROM DOCUMENT_PREVISION_SEQUENCE
WHERE Annee = {annee} AND IdDepartement = {idDept} AND NumeroVersion = {numVersion} AND TypeDocument = {type}");
}

var bag = new ConcurrentBag<int>();
var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
{
    await using var ctx = Create(cs);
    var repo = new DocumentPrevisionRepository(ctx);
    var n = await repo.AllouerNumeroAsync(annee, idDept, numVersion, type);
    bag.Add(n);
})).ToArray();

await Task.WhenAll(tasks);
var nums = bag.OrderBy(x => x).ToList();
Console.WriteLine("Allocated: " + string.Join(", ", nums));
Console.WriteLine($"Count={nums.Count} Distinct={nums.Distinct().Count()} Min={nums.Min()} Max={nums.Max()}");
Console.WriteLine(nums.Count == 20 && nums.Distinct().Count() == 20 && nums.Min() == 1 && nums.Max() == 20
    ? "CONCURRENCY_OK"
    : "CONCURRENCY_FAIL");

await using (var cleanup = Create(cs))
{
    await cleanup.Database.ExecuteSqlInterpolatedAsync($@"
DELETE FROM DOCUMENT_PREVISION_SEQUENCE
WHERE Annee = {annee} AND IdDepartement = {idDept} AND NumeroVersion = {numVersion} AND TypeDocument = {type}");
}

static BudgetDbContext Create(string cs)
{
    var opts = new DbContextOptionsBuilder<BudgetDbContext>().UseSqlServer(cs).Options;
    return new BudgetDbContext(opts);
}
