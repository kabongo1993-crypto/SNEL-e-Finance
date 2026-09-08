using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Application.SnelComptes.Import.DTOs;
using BudgetWeb.Application.SnelComptes.Import.Interfaces;
using BudgetWeb.Application.SnelComptes.Import.Services;
using BudgetWeb.Infrastructure.Import.Excel;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BudgetWeb.UnitTests.SnelComptes;

public class SnelComptesImportParserTests
{
    [Fact]
    public void Parse_IgnoreLesSeptProduits()
    {
        var parsed = SnelComptesImportParser.Parse(LignesMinimalistes());

        Assert.Equal(7, parsed.LignesIgnorees.Count(l => l.Motif == SnelComptesImportParser.MotifProduitsEnAttente));
        Assert.DoesNotContain(parsed.Rubriques, r => r.CodeImport is "7021" or "7022" or "71" or "72" or "75" or "77" or "79");
        Assert.DoesNotContain(parsed.Rubriques, r => r.CodeImport is "7000" or "7100" or "759");
    }

    [Fact]
    public void Parse_Doublon08800_ConserveUneSeuleOccurrence()
    {
        var parsed = SnelComptesImportParser.Parse(LignesMinimalistes());

        Assert.Single(parsed.Rubriques, r => r.CodeImport == "08800");
        Assert.Contains(parsed.LignesIgnorees, l => l.Code == "08800");
        Assert.Contains(parsed.Anomalies, a => a.Code == "DOUBLON_08800" && a.Severite == "Warning");
        Assert.DoesNotContain(parsed.Anomalies, a => a.Code == "CODE_DUPLIQUE" && a.CodeElement == "08800");
    }

    [Fact]
    public void Parse_ConstruitSectionPuisRubrique()
    {
        var parsed = SnelComptesImportParser.Parse(LignesMinimalistes());

        var section = Assert.Single(parsed.Rubriques, r => r.CodeImport == "0010");
        Assert.Null(section.CodeParent);
        Assert.Equal(0, section.Niveau);
        Assert.Equal("Achats énergie", section.Libelle);

        var rubrique = Assert.Single(parsed.Rubriques, r => r.CodeImport == "00100");
        Assert.Equal("0010", rubrique.CodeParent);
        Assert.Equal(1, rubrique.Niveau);
        Assert.DoesNotContain(parsed.Rubriques, r => r.CodeImport == "00");
    }

    [Fact]
    public void Parse_ItemsBI_CategorieUniquementSurLesRacines()
    {
        var parsed = SnelComptesImportParser.Parse(LignesMinimalistes());

        Assert.Equal(5, parsed.ItemsBI.Count);
        var ia = Assert.Single(parsed.ItemsBI, i => i.CodeImport == "I.A");
        Assert.Equal(0, ia.Niveau);
        Assert.Null(ia.CodeParent);
        Assert.Equal("FINANCEMENTS SUR FONDS PROPRES", ia.Categorie);

        var item1 = Assert.Single(parsed.ItemsBI, i => i.CodeImport == "ITEM 1");
        Assert.Equal("Item 1", item1.CodeExcel);
        Assert.Equal("I.A", item1.CodeParent);
        Assert.Equal(1, item1.Niveau);
        Assert.Null(item1.Categorie);

        var ii = Assert.Single(parsed.ItemsBI, i => i.CodeImport == "II");
        Assert.Equal("FINANCEMENTS EXTERIEURS", ii.Categorie);
        Assert.Null(ii.CodeParent);
    }

    [Fact]
    public void Parse_ParentIntrouvable_Erreur()
    {
        var rows = new List<SnelComptesExcelRow>
        {
            Entete(),
            Row(8, "", "", "0010", "00100", "Enfant orphelin"),
        };

        var parsed = SnelComptesImportParser.Parse(rows);
        Assert.Contains(parsed.Anomalies, a => a.Code == "PARENT_INTROUVABLE" && a.Severite == "Error");
    }

    [Fact]
    public void Parse_CodeDupliqueHors08800_Erreur()
    {
        var rows = new List<SnelComptesExcelRow>
        {
            Entete(),
            Row(8, "", "", "0010", "0010", "Section"),
            Row(9, "", "", "0010", "0010", "Section encore"),
        };

        var parsed = SnelComptesImportParser.Parse(rows);
        Assert.Contains(parsed.Anomalies, a => a.Code == "CODE_DUPLIQUE" && a.Severite == "Error");
        Assert.Single(parsed.Rubriques);
    }

    [Fact]
    public void Parse_NeCreePasDeRbDepuisLesColonnesCD()
    {
        var parsed = SnelComptesImportParser.Parse(LignesMinimalistes());
        Assert.DoesNotContain(parsed.Rubriques, r => r.CodeImport is "00" or "A" or "B");
        Assert.DoesNotContain(parsed.ItemsBI, i => i.CodeImport is "A" or "B");
    }

    internal static SnelComptesExcelRow Entete()
        => new(7, "", "", "Section", "Code", "Libellé");

    internal static SnelComptesExcelRow Row(int ligne, string c, string d, string e, string f, string g)
        => new(ligne, c, d, e, f, g);

    internal static List<SnelComptesExcelRow> LignesMinimalistes() =>
    [
        Entete(),
        Row(8, "00", "ACHATS", "0010", "0010", "Achats énergie"),
        Row(9, "", "ACHATS", "0010", "00100", "Achats énergie électrique"),
        Row(10, "08", "DOTATIONS", "08800", "08800", "Dotations aux amortissements"),
        Row(11, "", "", "08800", "08800", "Dotations aux amortissements"),
        Row(12, "", "", "7000", "7021", "Ventes à l’intérieur"),
        Row(13, "", "", "7000", "7022", "Ventes à l’exportation"),
        Row(14, "", "", "7100", "71", "Subventions d’exploitation"),
        Row(15, "", "", "7200", "72", "Production immobilisée"),
        Row(16, "", "", "7500", "75", "Autres produits"),
        Row(17, "", "", "7700", "77", "Revenus financiers"),
        Row(18, "", "", "7900", "79", "Reprises de provisions"),
        Row(19, "A", "FINANCEMENTS SUR FONDS PROPRES", "", "I.A", "Investissements Strategiques"),
        Row(20, "", "", "I.A", "Item 1", "Matériel et outillage"),
        Row(21, "", "", "I.A", "Item 2", "Installations techniques"),
        Row(22, "B", "FINANCEMENTS SUR FONDS PROPRES", "", "I.B", "Investissement d'appui"),
        Row(23, "", "FINANCEMENTS EXTERIEURS", "II", "II", "Projets (Financements Extérieurs)"),
    ];
}

public class SnelComptesImportServiceTests
{
    [Fact]
    public async Task Preview_CompteLesLignesACreer()
    {
        var harness = Harness.Create(SnelComptesImportParserTests.LignesMinimalistes());
        var preview = await harness.Service.PrevisualiserAsync(harness.Fichier);

        Assert.True(preview.PeutImporter);
        Assert.Equal(3, preview.Rubriques.TotalDetecte);
        Assert.Equal(3, preview.Rubriques.ACreer);
        Assert.Equal(0, preview.Rubriques.DejaExistant);
        Assert.Equal(0, preview.Rubriques.EnConflit);
        Assert.Equal(5, preview.ItemsBI.TotalDetecte);
        Assert.Equal(5, preview.ItemsBI.ACreer);
        Assert.Equal(7, preview.LignesIgnorees.Count(l => l.Motif == SnelComptesImportParser.MotifProduitsEnAttente));
        Assert.Contains(preview.ArbreRubriques, n => n.Code == "0010" && n.Enfants.Any(e => e.Code == "00100"));
    }

    [Fact]
    public async Task Execute_RefuseSansConfirmation()
    {
        var harness = Harness.Create(SnelComptesImportParserTests.LignesMinimalistes());
        var result = await harness.Service.ExecuterAsync(new SnelComptesExecuteRequestDto(false, harness.Fichier));

        Assert.False(result.Succes);
        Assert.Empty(harness.RbRepo.Items);
        Assert.Empty(harness.ItemRepo.Items);
    }

    [Fact]
    public async Task Execute_InserePuisEstIdempotent()
    {
        var harness = Harness.Create(SnelComptesImportParserTests.LignesMinimalistes());

        var premier = await harness.Service.ExecuterAsync(new SnelComptesExecuteRequestDto(true, harness.Fichier));
        Assert.True(premier.Succes);
        Assert.Equal(3, premier.RubriquesInserees);
        Assert.Equal(5, premier.ItemsBIInserees);
        Assert.Equal(3, harness.RbRepo.Items.Count);
        Assert.Equal(5, harness.ItemRepo.Items.Count);

        var enfant = harness.RbRepo.Items.Single(r => r.CodeRB == "00100");
        Assert.Equal(1, enfant.Niveau);
        Assert.NotNull(enfant.ParentId);

        var item1 = harness.ItemRepo.Items.Single(i => i.CodeItem == "ITEM 1");
        Assert.Equal("I.A", item1.ParentCode);
        Assert.Null(item1.Categorie);

        var second = await harness.Service.ExecuterAsync(new SnelComptesExecuteRequestDto(true, harness.Fichier));
        Assert.True(second.Succes);
        Assert.Equal(0, second.RubriquesInserees);
        Assert.Equal(0, second.ItemsBIInserees);
        Assert.Equal(3, harness.RbRepo.Items.Count);
        Assert.Equal(5, harness.ItemRepo.Items.Count);
        Assert.Contains("déjà présents", second.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_ConflitLibelle_BloqueSansModifier()
    {
        var harness = Harness.Create(SnelComptesImportParserTests.LignesMinimalistes());
        harness.RbRepo.Items.Add(new RubriqueBudgetaireDto(
            1, "0010", "Libellé différent", null, null, null, 0, true, DateTime.Now, 0, 0));

        var preview = await harness.Service.PrevisualiserAsync(harness.Fichier);
        Assert.False(preview.PeutImporter);
        Assert.Equal(1, preview.Rubriques.EnConflit);

        var result = await harness.Service.ExecuterAsync(new SnelComptesExecuteRequestDto(true, harness.Fichier));
        Assert.False(result.Succes);
        Assert.Single(harness.RbRepo.Items);
        Assert.Equal("Libellé différent", harness.RbRepo.Items[0].Libelle);
        Assert.Empty(harness.ItemRepo.Items);
    }

    [Fact]
    public async Task Execute_NeReimportePasLesProduits()
    {
        var harness = Harness.Create(SnelComptesImportParserTests.LignesMinimalistes());
        await harness.Service.ExecuterAsync(new SnelComptesExecuteRequestDto(true, harness.Fichier));

        Assert.DoesNotContain(harness.RbRepo.Items, r => r.CodeRB is "7021" or "71" or "759" or "7000");
    }

    private sealed class Harness
    {
        public required SnelComptesImportService Service { get; init; }
        public required FakeRubriqueRepository RbRepo { get; init; }
        public required FakeItemRepository ItemRepo { get; init; }
        public required string Fichier { get; init; }

        public static Harness Create(IReadOnlyList<SnelComptesExcelRow> rows)
        {
            var fichier = Path.GetTempFileName();
            File.WriteAllText(fichier, "placeholder");
            var rbRepo = new FakeRubriqueRepository();
            var itemRepo = new FakeItemRepository();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var service = new SnelComptesImportService(
                new FakeWorkbookReader(rows),
                new RubriqueBudgetaireService(rbRepo),
                new ItemBIService(itemRepo),
                new ImmediateTransaction(),
                configuration);

            return new Harness { Service = service, RbRepo = rbRepo, ItemRepo = itemRepo, Fichier = fichier };
        }
    }

    private sealed class FakeWorkbookReader(IReadOnlyList<SnelComptesExcelRow> rows) : ISnelComptesWorkbookReader
    {
        public Task<IReadOnlyList<SnelComptesExcelRow>> LireAsync(string fichierSource, CancellationToken cancellationToken = default)
            => Task.FromResult(rows);
    }

    private sealed class ImmediateTransaction : ISnelComptesImportTransaction
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            => action(cancellationToken);
    }

    internal sealed class FakeRubriqueRepository : IRubriqueBudgetaireRepository
    {
        private long _nextId = 1;
        public List<RubriqueBudgetaireDto> Items { get; } = [];

        public Task<IReadOnlyList<RubriqueBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RubriqueBudgetaireDto>>(Items);

        public Task<RubriqueBudgetaireDto?> GetByIdAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(r => r.IdRB == idRB));

        public Task<bool> ExistsByCodeAsync(string codeRB, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(r =>
                r.CodeRB.Equals(codeRB, StringComparison.OrdinalIgnoreCase)
                && (excludeId is null || r.IdRB != excludeId)));

        public Task<bool> ExistsByIdAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(r => r.IdRB == idRB));

        public Task<bool> WouldCreateCycleAsync(long idRB, long parentId, CancellationToken cancellationToken = default)
            => Task.FromResult(idRB == parentId);

        public Task<int> CountEnfantsAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Count(r => r.ParentId == idRB));

        public Task<int> CountPrevisionsAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<RubriqueBudgetaireDto> CreateAsync(
            string codeRB, string libelle, long? parentId, int niveau, bool actif,
            CancellationToken cancellationToken = default)
        {
            var parentCode = parentId is long pid ? Items.FirstOrDefault(r => r.IdRB == pid)?.CodeRB : null;
            var created = new RubriqueBudgetaireDto(
                _nextId++, codeRB, libelle, parentId, parentCode, null, niveau, actif, DateTime.Now, 0, 0);
            Items.Add(created);
            return Task.FromResult(created);
        }

        public Task<RubriqueBudgetaireDto?> UpdateAsync(
            long idRB, string codeRB, string libelle, long? parentId, int niveau, bool actif,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("L'import ne doit pas modifier une rubrique existante.");

        public Task<RubriqueBudgetaireDto?> SetActifAsync(long idRB, bool actif, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("L'import ne doit pas désactiver une rubrique existante.");

        public Task<bool> DeleteAsync(long idRB, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("L'import ne doit pas supprimer une rubrique existante.");
    }

    internal sealed class FakeItemRepository : IItemBIRepository
    {
        private long _nextId = 1;
        public List<ItemBIDto> Items { get; } = [];

        public Task<IReadOnlyList<ItemBIDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ItemBIDto>>(Items);

        public Task<ItemBIDto?> GetByIdAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.IdItemBI == idItemBI));

        public Task<bool> ExistsByCodeAsync(string codeItem, long? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(i =>
                i.CodeItem.Equals(codeItem, StringComparison.OrdinalIgnoreCase)
                && (excludeId is null || i.IdItemBI != excludeId)));

        public Task<bool> ExistsByIdAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(i => i.IdItemBI == idItemBI));

        public Task<bool> WouldCreateCycleAsync(long idItemBI, long parentId, CancellationToken cancellationToken = default)
            => Task.FromResult(idItemBI == parentId);

        public Task<int> CountEnfantsAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Count(i => i.ParentId == idItemBI));

        public Task<int> CountPrevisionsAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<ItemBIDto> CreateAsync(
            string codeItem, string libelle, long? parentId, int niveau, string? categorie, bool actif,
            CancellationToken cancellationToken = default)
        {
            var parentCode = parentId is long pid ? Items.FirstOrDefault(i => i.IdItemBI == pid)?.CodeItem : null;
            var created = new ItemBIDto(
                _nextId++, codeItem, libelle, parentId, parentCode, null, niveau, categorie, actif, DateTime.Now, 0, 0);
            Items.Add(created);
            return Task.FromResult(created);
        }

        public Task<ItemBIDto?> UpdateAsync(
            long idItemBI, string codeItem, string libelle, long? parentId, int niveau, string? categorie, bool actif,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("L'import ne doit pas modifier un item BI existant.");

        public Task<ItemBIDto?> SetActifAsync(long idItemBI, bool actif, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("L'import ne doit pas désactiver un item BI existant.");

        public Task<bool> DeleteAsync(long idItemBI, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("L'import ne doit pas supprimer un item BI existant.");
    }
}

public class SnelComptesWorkbookReaderTests
{
    [Fact]
    public async Task LireFichierReel_Produit64RbEt11Items()
    {
        var path = TrouverExcel();
        Assert.True(File.Exists(path), $"Fichier Excel introuvable : {path}");

        var reader = new SnelComptesWorkbookReader();
        var rows = await reader.LireAsync(path);
        var parsed = SnelComptesImportParser.Parse(rows);

        Assert.Equal(64, parsed.Rubriques.Count);
        Assert.Equal(13, parsed.Rubriques.Count(r => r.Niveau == 0));
        Assert.Equal(51, parsed.Rubriques.Count(r => r.Niveau == 1));
        Assert.Equal(11, parsed.ItemsBI.Count);
        Assert.Equal(7, parsed.LignesIgnorees.Count(l => l.Motif == SnelComptesImportParser.MotifProduitsEnAttente));
        Assert.Single(parsed.Rubriques, r => r.CodeImport == "08800");
        Assert.DoesNotContain(parsed.Rubriques, r => r.CodeImport is "7021" or "7022" or "71" or "72" or "75" or "77" or "79" or "759");
        Assert.Contains(parsed.ItemsBI, i => i.CodeExcel == "Item 1" && i.CodeImport == "ITEM 1");
        Assert.DoesNotContain(parsed.Anomalies, a => a.Severite == "Error");
    }

    private static string TrouverExcel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Tableau_Comptes_SYSCOHADA_Sections_Colore.xlsx");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "Tableau_Comptes_SYSCOHADA_Sections_Colore.xlsx");
    }
}
