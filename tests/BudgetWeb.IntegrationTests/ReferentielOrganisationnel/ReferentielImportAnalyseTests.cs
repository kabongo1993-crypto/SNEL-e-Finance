using BudgetWeb.Application;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Enums;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using BudgetWeb.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BudgetWeb.IntegrationTests.ReferentielOrganisationnel;

public class ReferentielImportAnalyseTests
{
    [Fact]
    public async Task Analyse_FichierCorrespondance_RetourneCompteursAttendus()
    {
        var filePath = ResoudreCheminFichier();
        if (!File.Exists(filePath))
        {
            return;
        }

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost\\HEROS_SQL19;Database=BD_SNEL;Trusted_Connection=True;TrustServerCertificate=True;",
                ["Import:CorrespondanceFilePath"] = filePath
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();
        var importService = provider.GetRequiredService<IReferentielImportService>();

        var analyse = await importService.AnalyserAsync(filePath);
        var preview = await importService.PrevisualiserAsync(filePath);

        Assert.Equal(11, analyse.Compteurs.Entites);
        Assert.Equal(15, analyse.Compteurs.Departements);
        Assert.Equal(41, analyse.Compteurs.RelationsEntiteDepartement);
        Assert.True(analyse.Compteurs.UnitesBudgetaires >= 500);
        Assert.Equal(12, analyse.Compteurs.LignesSansCodeUB);
        Assert.Equal(252, analyse.Compteurs.Structures);
        Assert.True(preview.PeutImporter);
        Assert.DoesNotContain(preview.Analyse.Anomalies, a => a.Severite >= ImportIssueSeverity.Error);

        var divisionsCommerciales = preview.Structures
            .Where(s => s.CleMetier.StartsWith("ENTITE|DDK>DEPARTEMENT|DEC>DIVISION|", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Code)
            .OrderBy(c => c)
            .ToList();
        Assert.Equal(["GCC", "GCE", "GCN", "GCO", "GCS"], divisionsCommerciales);

        Assert.Equal("DSG", preview.UnitesBudgetaires.Single(u => u.CodeUB == "A00600").DepartementSigle);
        Assert.Equal("DSG", preview.UnitesBudgetaires.Single(u => u.CodeUB == "A00610").DepartementSigle);
        Assert.Equal("DCG", preview.UnitesBudgetaires.Single(u => u.CodeUB == "A00620").DepartementSigle);
        Assert.Equal("DCG", preview.UnitesBudgetaires.Single(u => u.CodeUB == "A00630").DepartementSigle);
    }

    private static string ResoudreCheminFichier()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Correspondance_Organisationnelle_Budget_Web.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Correspondance_Organisationnelle_Budget_Web.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", "import", "Correspondance_Organisationnelle_Budget_Web.xlsx")
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return Path.GetFullPath(candidates[0]);
    }
}
