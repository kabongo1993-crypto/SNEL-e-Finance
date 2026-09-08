using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Persistence;
using BudgetWeb.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.Development.json", optional: false)
    .Build();
var cs = config.GetConnectionString("DefaultConnection")!;
Console.WriteLine("TargetDatabase=" + new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs).InitialCatalog);

var services = new ServiceCollection();
services.AddDbContext<BudgetDbContext>(o => o.UseSqlServer(cs));
services.AddScoped<IClassementAeRepository, ClassementAeRepository>();
services.AddScoped<IClassementAeService, ClassementAeService>();
await using var sp = services.BuildServiceProvider();
var svc = sp.GetRequiredService<IClassementAeService>();

var before = await svc.GetAsync(4, 1);
Console.WriteLine("BEFORE=" + JsonSerializer.Serialize(before));

var ids = before.Select(x => x.IdClassementAE).Reverse().ToList();
await svc.ReorderAsync(new ReorderClassementAeRequest(4, 1, ids));
var swapped = await svc.GetAsync(4, 1);
Console.WriteLine("SWAPPED=" + JsonSerializer.Serialize(swapped.Select(x => new { x.OrdreAffichage, x.TypeLigne, x.LibelleItemAE, x.LibelleGroupe })));

var restore = swapped.OrderBy(x => x.IdClassementAE).Select(x => x.IdClassementAE).ToList();
await svc.ReorderAsync(new ReorderClassementAeRequest(4, 1, restore));
var after = await svc.GetAsync(4, 1);
Console.WriteLine("RESTORED=" + JsonSerializer.Serialize(after.Select(x => new { x.OrdreAffichage, x.TypeLigne, x.LibelleItemAE, x.LibelleGroupe })));

try {
  await svc.ReorderAsync(new ReorderClassementAeRequest(4, 1, new List<long> { 1 }));
  Console.WriteLine("INVALID=UNEXPECTED_OK");
} catch (Exception ex) {
  Console.WriteLine("INVALID_REJECTED=" + ex.GetType().Name + ":" + ex.Message);
}
