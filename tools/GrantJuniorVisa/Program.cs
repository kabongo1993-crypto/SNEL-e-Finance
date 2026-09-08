using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// One-shot: accorde imputer_dc + viser_budget au Junior DC (GJ) sans afficher de secrets.
var apiDir = Path.GetFullPath(Path.Combine(
    Directory.GetCurrentDirectory(),
    "..",
    "..",
    "src",
    "BudgetWeb.API"));
if (!Directory.Exists(apiDir))
{
    apiDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "BudgetWeb.API"));
}
if (!Directory.Exists(apiDir))
    throw new DirectoryNotFoundException($"BudgetWeb.API introuvable (cherché depuis {Directory.GetCurrentDirectory()}).");

var config = new ConfigurationBuilder()
    .SetBasePath(apiDir)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var cs = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection manquant.");

var options = new DbContextOptionsBuilder<BudgetDbContext>()
    .UseSqlServer(cs)
    .Options;

await using var db = new BudgetDbContext(options);

var juniors = await (
    from u in db.Utilisateurs.AsNoTracking()
    join p in db.ProfilsUtilisateur.AsNoTracking() on u.IdUtilisateur equals p.FK_Utilisateur
    where u.Actif
          && (p.CodeProfil == "GESTIONNAIRE_JUNIOR_DC"
              || p.CodeProfil == "GESTIONNAIRE_JUNIOR"
              || u.NomUtilisateur.ToLower().Contains("gj")
              || u.Nom.Contains("Junior"))
    select new { u.IdUtilisateur, u.NomUtilisateur, u.Nom, u.Prenom, p.CodeProfil }
).ToListAsync();

if (juniors.Count == 0)
{
    Console.WriteLine("Aucun utilisateur Junior trouvé.");
    return 1;
}

Console.WriteLine("Cibles trouvées :");
foreach (var g in juniors.GroupBy(x => x.IdUtilisateur))
{
    var first = g.First();
    var profils = string.Join(",", g.Select(x => x.CodeProfil).Distinct());
    Console.WriteLine($"  id={first.IdUtilisateur} user={first.NomUtilisateur} profils=[{profils}]");
}

var targetIds = juniors.Select(j => j.IdUtilisateur).Distinct().ToList();
Console.WriteLine($"Mise à jour de {targetIds.Count} utilisateur(s).");

foreach (var targetId in targetIds)
{
    var target = juniors.First(j => j.IdUtilisateur == targetId);
    Console.WriteLine($"--- {target.NomUtilisateur} (id={targetId}) ---");

    var needed = new[] { "paiements.imputer_dc", "paiements.viser_budget" };
    var existing = await db.PermissionsUtilisateur
        .Where(p => p.FK_Utilisateur == targetId)
        .Select(p => p.CodePermission)
        .ToListAsync();

    foreach (var code in needed)
    {
        if (existing.Any(e => e.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"  déjà présent : {code}");
            continue;
        }

        db.PermissionsUtilisateur.Add(new PermissionUtilisateur
        {
            FK_Utilisateur = targetId,
            CodePermission = code,
            DateAttribution = DateTime.UtcNow,
        });
        Console.WriteLine($"  ajouté : {code}");
    }

    var hasDcProfil = await db.ProfilsUtilisateur.AnyAsync(
        p => p.FK_Utilisateur == targetId && p.CodeProfil == "GESTIONNAIRE_JUNIOR_DC");
    if (!hasDcProfil)
    {
        db.ProfilsUtilisateur.Add(new ProfilUtilisateur
        {
            FK_Utilisateur = targetId,
            CodeProfil = "GESTIONNAIRE_JUNIOR_DC",
        });
        Console.WriteLine("  profil ajouté : GESTIONNAIRE_JUNIOR_DC");
    }
}

await db.SaveChangesAsync();

foreach (var targetId in targetIds)
{
    var name = juniors.First(j => j.IdUtilisateur == targetId).NomUtilisateur;
    var after = await db.PermissionsUtilisateur.AsNoTracking()
        .Where(p => p.FK_Utilisateur == targetId)
        .Select(p => p.CodePermission)
        .OrderBy(c => c)
        .ToListAsync();
    Console.WriteLine($"Permissions individuelles {name}:");
    foreach (var p in after)
        Console.WriteLine($"  - {p}");
}

Console.WriteLine("OK — droits en base. Déconnectez puis reconnectez le compte GJ utilisé.");
return 0;
