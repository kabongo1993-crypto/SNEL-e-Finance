using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BudgetWeb.IntegrationTests;

/// <summary>
/// Affectations pct.COMPTE_CATEGORIE via l'API in-process. Pas de mock.
/// Import historique optionnel si COMPTE_CATEGORIE.xlsx est présent.
/// </summary>
public class CompteCategorieReferentielSqlTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string NumeroA = "__IT_CCAT_A__";
    private const string NumeroB = "__IT_CCAT_B__";
    private const string NumeroInactif = "__IT_CCAT_OFF__";
    private const long IdImportA = 920101;
    private const long IdImportB = 920102;
    private static readonly string ExcelPath = Path.Combine(@"d:\InstantFLOW\Trésorerie", "COMPTE_CATEGORIE.xlsx");

    private readonly WebApplicationFactory<Program> _factory;

    public CompteCategorieReferentielSqlTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CompteCategories_SchemaFkCrudClotureEtRegles()
    {
        var client = await CreateAuthenticatedClientAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            await AssertSchemaAsync(db);
        }

        var refs = await LoadRefsAsync();
        await CleanupNumerosAsync(NumeroA, NumeroB, NumeroInactif);

        try
        {
            var compteA = await CreateCompteAsync(client, refs, NumeroA, true);
            var compteB = await CreateCompteAsync(client, refs, NumeroB, true);
            var compteOff = await CreateCompteAsync(client, refs, NumeroInactif, false);

            var empty = await client.GetFromJsonAsync<List<CompteCategorieDto>>(
                $"/api/v1/comptes/{compteA.IdCompte}/categories");
            Assert.NotNull(empty);
            Assert.Empty(empty);

            var missing = await client.GetAsync("/api/v1/comptes/999999001/categories");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

            var catInconnue = await client.PostAsJsonAsync(
                $"/api/v1/comptes/{compteA.IdCompte}/categories",
                new UpsertCompteCategorieRequest(999999, new DateOnly(2020, 1, 1), null));
            Assert.Equal(HttpStatusCode.BadRequest, catInconnue.StatusCode);

            var dates = await client.PostAsJsonAsync(
                $"/api/v1/comptes/{compteA.IdCompte}/categories",
                new UpsertCompteCategorieRequest(refs.IdCategorie, new DateOnly(2025, 1, 1), new DateOnly(2024, 1, 1)));
            Assert.Equal(HttpStatusCode.BadRequest, dates.StatusCode);

            var inactiveCompte = await client.PostAsJsonAsync(
                $"/api/v1/comptes/{compteOff.IdCompte}/categories",
                new UpsertCompteCategorieRequest(refs.IdCategorie, new DateOnly(2020, 1, 1), null));
            Assert.Equal(HttpStatusCode.BadRequest, inactiveCompte.StatusCode);

            var created = await client.PostAsJsonAsync(
                $"/api/v1/comptes/{compteA.IdCompte}/categories",
                new UpsertCompteCategorieRequest(refs.IdCategorie, new DateOnly(2020, 1, 1), null));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var active = await created.Content.ReadFromJsonAsync<CompteCategorieDto>();
            Assert.NotNull(active);
            Assert.True(active.Active);
            Assert.Null(active.DateFin);
            Assert.Equal(refs.IdCategorie, active.IdCategorieCompte);

            var secondeActive = await client.PostAsJsonAsync(
                $"/api/v1/comptes/{compteA.IdCompte}/categories",
                new UpsertCompteCategorieRequest(refs.IdCategorie2, new DateOnly(2025, 1, 1), null));
            Assert.Equal(HttpStatusCode.BadRequest, secondeActive.StatusCode);
            var secondeBody = await secondeActive.Content.ReadFromJsonAsync<ApiMessage>();
            Assert.Contains("affectation active", secondeBody?.Message ?? "", StringComparison.OrdinalIgnoreCase);

            var overlap = await client.PostAsJsonAsync(
                $"/api/v1/comptes/{compteA.IdCompte}/categories",
                new UpsertCompteCategorieRequest(refs.IdCategorie2, new DateOnly(2019, 1, 1), new DateOnly(2021, 1, 1)));
            Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);

            var cloture = await client.PostAsJsonAsync(
                $"/api/v1/comptes/{compteA.IdCompte}/categories/{active.IdCompteCategorie}/cloturer",
                new CloturerCompteCategorieRequest(new DateOnly(2024, 12, 31)));
            cloture.EnsureSuccessStatusCode();
            var closed = await cloture.Content.ReadFromJsonAsync<CompteCategorieDto>();
            Assert.False(closed!.Active);
            Assert.Equal(new DateOnly(2024, 12, 31), closed.DateFin);

            var suivante = await client.PostAsJsonAsync(
                $"/api/v1/comptes/{compteA.IdCompte}/categories",
                new UpsertCompteCategorieRequest(refs.IdCategorie2, new DateOnly(2025, 1, 1), null));
            suivante.EnsureSuccessStatusCode();

            var update = await client.PutAsJsonAsync(
                $"/api/v1/comptes/{compteA.IdCompte}/categories/{closed.IdCompteCategorie}",
                new UpsertCompteCategorieRequest(refs.IdCategorie, new DateOnly(2020, 1, 1), new DateOnly(2024, 12, 30)));
            update.EnsureSuccessStatusCode();

            var list = await client.GetFromJsonAsync<List<CompteCategorieDto>>(
                $"/api/v1/comptes/{compteA.IdCompte}/categories");
            Assert.NotNull(list);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, r => r.Active);
            Assert.Contains(list, r => !r.Active);

            var delete = await client.DeleteAsync(
                $"/api/v1/comptes/{compteA.IdCompte}/categories/{closed.IdCompteCategorie}");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);

            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            Assert.Equal(2, await db.ComptesCategories.CountAsync(c => c.FK_Compte == compteA.IdCompte));

            var catInactive = await CreateCategorieInactiveAsync(client);
            var refuseInactive = await client.PostAsJsonAsync(
                $"/api/v1/comptes/{compteB.IdCompte}/categories",
                new UpsertCompteCategorieRequest(catInactive, new DateOnly(2020, 1, 1), null));
            Assert.Equal(HttpStatusCode.BadRequest, refuseInactive.StatusCode);

            var fkCompte = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO [pct].[COMPTE_CATEGORIE] ([FK_Compte], [FK_CategorieCompte], [DateDebut])
                    VALUES (999999001, {refs.IdCategorie}, {new DateOnly(2020, 1, 1)});
                    """);
            });
            Assert.Contains("FK_PCT_COMPTE_CATEGORIE_Compte", fkCompte.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupNumerosAsync(NumeroA, NumeroB, NumeroInactif);
        }
    }

    [Fact]
    public async Task CompteCategories_PreviewImportEtRollback()
    {
        var client = await CreateAuthenticatedClientAsync();
        var refs = await LoadRefsAsync();
        await CleanupNumerosAsync(NumeroA, NumeroB);
        await CleanupRelationIdsAsync(IdImportA, IdImportB);

        try
        {
            var compteA = await CreateCompteAsync(client, refs, NumeroA, true);
            var compteB = await CreateCompteAsync(client, refs, NumeroB, true);

            var previewErr = await client.PostAsJsonAsync(
                "/api/v1/comptes/categories/import/preview",
                new ImportCompteCategoriesPreviewRequest("erreurs.xlsx",
                [
                    new(2, "920110", "2020-01-01", "1900-01-01", refs.IdCategorie.ToString(), "999999001"),
                    new(3, "920111", "2020-01-01", "1900-01-01", "999999", compteA.IdCompte.ToString()),
                    new(4, IdImportA.ToString(), "2020-01-01", "1900-01-01", refs.IdCategorie.ToString(), compteA.IdCompte.ToString()),
                    new(5, "920112", "2020-01-01", "1", refs.IdCategorie.ToString(), compteA.IdCompte.ToString()),
                    new(6, IdImportB.ToString(), "2023-01-01", "2025-12-31", refs.IdCategorie2.ToString(), compteA.IdCompte.ToString()),
                ]));
            previewErr.EnsureSuccessStatusCode();
            var preview = await previewErr.Content.ReadFromJsonAsync<ImportCompteCategoriesPreviewDto>();
            Assert.NotNull(preview);
            Assert.Equal(5, preview.Resume.Analysees);
            Assert.Equal(1, preview.Resume.AImporter);
            Assert.Equal(1, preview.Resume.Doublons);
            Assert.Equal(1, preview.Resume.ConflitsPeriode);
            Assert.Equal(4, preview.Resume.DatesSentinelleConverties);
            Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Compte 999999001 introuvable", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Catégorie 999999 introuvable", StringComparison.OrdinalIgnoreCase));

            var importOk = await client.PostAsJsonAsync(
                "/api/v1/comptes/categories/import",
                new ImportCompteCategoriesRequest("mini.xlsx",
                [
                    new(2, IdImportA.ToString(), "2018-01-01", "2019-12-31", refs.IdCategorie.ToString(), compteA.IdCompte.ToString()),
                    new(3, IdImportB.ToString(), "2020-01-01", "1900-01-01", refs.IdCategorie2.ToString(), compteB.IdCompte.ToString()),
                ]));
            importOk.EnsureSuccessStatusCode();
            var imported = await importOk.Content.ReadFromJsonAsync<ImportCompteCategoriesResultDto>();
            Assert.Equal(2, imported!.Importes);

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
                var a = await db.ComptesCategories.AsNoTracking().FirstAsync(c => c.IdCompteCategorie == IdImportA);
                Assert.Equal(new DateOnly(2019, 12, 31), a.DateFin);
                var b = await db.ComptesCategories.AsNoTracking().FirstAsync(c => c.IdCompteCategorie == IdImportB);
                Assert.Null(b.DateFin);
            }

            await CleanupRelationIdsAsync(IdImportA, IdImportB);

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<ICompteCategorieRepository>();
                var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
                var boom = await Assert.ThrowsAnyAsync<Exception>(() =>
                    repo.AddRangeWithExplicitIdsAsync(
                    [
                        new BudgetWeb.Domain.Entities.CompteCategorie
                        {
                            IdCompteCategorie = IdImportA,
                            FK_Compte = compteA.IdCompte,
                            FK_CategorieCompte = refs.IdCategorie,
                            DateDebut = new DateOnly(2018, 1, 1),
                            DateFin = new DateOnly(2019, 12, 31),
                        },
                        new BudgetWeb.Domain.Entities.CompteCategorie
                        {
                            IdCompteCategorie = IdImportB,
                            FK_Compte = 999999001,
                            FK_CategorieCompte = refs.IdCategorie,
                            DateDebut = new DateOnly(2020, 1, 1),
                            DateFin = null,
                        },
                    ]));
                Assert.True(boom is SqlException || boom.InnerException is SqlException);
                Assert.False(await db.ComptesCategories.AnyAsync(c => c.IdCompteCategorie == IdImportA));
            }
        }
        finally
        {
            await CleanupNumerosAsync(NumeroA, NumeroB);
            await CleanupRelationIdsAsync(IdImportA, IdImportB);
        }
    }

    [Fact]
    public async Task CompteCategories_ImportFichierHistorique()
    {
        if (!File.Exists(ExcelPath))
            return;

        var client = await CreateAuthenticatedClientAsync();
        var lignes = LireCompteCategorieXlsx(ExcelPath);
        Assert.Equal(272, lignes.Count);

        var previewResp = await client.PostAsJsonAsync(
            "/api/v1/comptes/categories/import/preview",
            new ImportCompteCategoriesPreviewRequest("COMPTE_CATEGORIE.xlsx", lignes));
        previewResp.EnsureSuccessStatusCode();
        var preview = await previewResp.Content.ReadFromJsonAsync<ImportCompteCategoriesPreviewDto>();
        Assert.NotNull(preview);
        Assert.Equal(272, preview.Resume.Analysees);
        Assert.True(
            preview.Resume.DatesSentinelleConverties >= 1,
            "Le fichier historique doit convertir des Date_Fin 1900-01-01.");
        Assert.True(
            preview.Resume.AImporter > 0 || preview.Resume.DejaExistants > 0,
            $"Aucune ligne importable. aImporter={preview.Resume.AImporter} erreurs={preview.Resume.Erreurs} "
            + $"doublons={preview.Resume.Doublons} conflits={preview.Resume.ConflitsPeriode}.");

        if (preview.Resume.AImporter > 0)
        {
            var importResp = await client.PostAsJsonAsync(
                "/api/v1/comptes/categories/import",
                new ImportCompteCategoriesRequest("COMPTE_CATEGORIE.xlsx", lignes));
            importResp.EnsureSuccessStatusCode();
            var result = await importResp.Content.ReadFromJsonAsync<ImportCompteCategoriesResultDto>();
            Assert.NotNull(result);
            Assert.Equal(preview.Resume.AImporter, result.Importes);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var count = await db.ComptesCategories.CountAsync();
        Assert.True(count >= 1, $"pct.COMPTE_CATEGORIE devrait contenir l'historique (actuel={count}).");

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM pct.COMPTE_CATEGORIE cc
               WHERE NOT EXISTS (SELECT 1 FROM pct.COMPTE c WHERE c.IdCompte = cc.FK_Compte)) AS OrphelinsCompte,
              (SELECT COUNT(*) FROM pct.COMPTE_CATEGORIE cc
               WHERE NOT EXISTS (SELECT 1 FROM pct.CATEGORIE_COMPTE cat WHERE cat.IdCategorieCompte = cc.FK_CategorieCompte)) AS OrphelinsCategorie,
              (SELECT COUNT(*) FROM (
                    SELECT FK_Compte FROM pct.COMPTE_CATEGORIE WHERE DateFin IS NULL
                    GROUP BY FK_Compte HAVING COUNT(*) > 1
              ) x) AS DoublesActives;
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, Convert.ToInt32(reader[0]));
        Assert.Equal(0, Convert.ToInt32(reader[1]));
        Assert.Equal(0, Convert.ToInt32(reader[2]));
    }

    private static List<ImportCompteCategorieRawRequest> LireCompteCategorieXlsx(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = ws.FirstRowUsed() ?? throw new InvalidOperationException("Feuille Excel vide.");
        foreach (var cell in headerRow.CellsUsed())
        {
            var folded = FoldHeader(cell.GetString());
            if (folded.Length == 0 || headers.ContainsKey(folded)) continue;
            headers[folded] = cell.Address.ColumnNumber;
        }

        int Col(params string[] names)
        {
            foreach (var n in names)
            {
                if (headers.TryGetValue(n, out var i)) return i;
            }
            return -1;
        }

        var colId = Col("idtcompteaveccategorie", "idcomptecategorie");
        var colDebut = Col("datedebut");
        var colFin = Col("datefin");
        var colCat = Col("idtcategoriecompte", "idcategoriecompte");
        var colCompte = Col("idcompte");
        if (colId < 0 || colDebut < 0 || colCat < 0 || colCompte < 0)
            throw new InvalidOperationException("Colonnes COMPTE_CATEGORIE.xlsx introuvables.");

        var lignes = new List<ImportCompteCategorieRawRequest>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            lignes.Add(new ImportCompteCategorieRawRequest(
                row.RowNumber(),
                Cell(row, colId),
                Cell(row, colDebut),
                Cell(row, colFin),
                Cell(row, colCat),
                Cell(row, colCompte)));
        }

        return lignes;
    }

    private static string FoldHeader(string value)
        => new string(value.Normalize().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string Cell(IXLRow row, int col)
    {
        if (col < 0) return "";
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return "";
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue(out DateTime dt))
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (cell.TryGetValue(out double numeric))
        {
            if (Math.Abs(numeric - Math.Round(numeric)) < 0.0000001)
                return ((long)Math.Round(numeric)).ToString(CultureInfo.InvariantCulture);
            return numeric.ToString("G15", CultureInfo.InvariantCulture);
        }
        return cell.GetString().Trim();
    }

    private async Task<CompteFinancierDto> CreateCompteAsync(
        HttpClient client,
        Refs refs,
        string numero,
        bool actif)
    {
        var resp = await client.PostAsJsonAsync(
            "/api/v1/comptes",
            new CreateCompteFinancierRequest(
                numero,
                "Compte test catégories",
                refs.IdBanque,
                refs.IdDirection,
                refs.CodeType,
                refs.IdDevise,
                refs.IdProvince,
                null,
                actif));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CompteFinancierDto>())!;
    }

    private async Task<long> CreateCategorieInactiveAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/categories-comptes",
            new CreateCategorieCompteRequest("__IT_CCAT_INACTIVE__", null, true));
        if (create.StatusCode == HttpStatusCode.BadRequest)
        {
            var list = await client.GetFromJsonAsync<List<CategorieCompteDto>>(
                "/api/v1/categories-comptes?actifsSeulement=false");
            var existing = list!.First(c => c.Libelle == "__IT_CCAT_INACTIVE__");
            var offExisting = await client.PutAsJsonAsync(
                $"/api/v1/categories-comptes/{existing.IdCategorieCompte}",
                new UpdateCategorieCompteRequest(existing.Libelle, existing.Orientation, false));
            offExisting.EnsureSuccessStatusCode();
            return existing.IdCategorieCompte;
        }

        create.EnsureSuccessStatusCode();
        var created = (await create.Content.ReadFromJsonAsync<CategorieCompteDto>())!;
        var off = await client.PutAsJsonAsync(
            $"/api/v1/categories-comptes/{created.IdCategorieCompte}",
            new UpdateCategorieCompteRequest(created.Libelle, created.Orientation, false));
        off.EnsureSuccessStatusCode();
        return created.IdCategorieCompte;
    }

    private async Task<Refs> LoadRefsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var banque = await db.Banques.AsNoTracking().FirstOrDefaultAsync(b => b.IdBanque == "RAWBANK")
            ?? await db.Banques.AsNoTracking().FirstAsync();
        var direction = await db.DirectionsTresorerie.AsNoTracking().FirstAsync();
        var type = await db.TypesCompte.AsNoTracking().FirstOrDefaultAsync(t => t.Code == "FCT")
            ?? await db.TypesCompte.AsNoTracking().FirstAsync();
        var devise = await db.Devises.AsNoTracking().FirstOrDefaultAsync(d => d.Code == "CDF")
            ?? await db.Devises.AsNoTracking().FirstAsync();
        var province = await db.Provinces.AsNoTracking().FirstOrDefaultAsync(p => p.IdProvince == "KIN")
            ?? await db.Provinces.AsNoTracking().FirstAsync();
        var cats = await db.CategoriesCompte.AsNoTracking().Where(c => c.Actif).Take(2).ToListAsync();
        if (cats.Count < 2)
            throw new InvalidOperationException("Au moins deux catégories actives sont requises.");
        return new Refs(banque.IdBanque, direction.IdDirection, type.Code, devise.IdDevise, province.IdProvince,
            cats[0].IdCategorieCompte, cats[1].IdCategorieCompte);
    }

    private static async Task AssertSchemaAsync(BudgetDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.name, ty.name, c.is_nullable, c.is_identity
            FROM sys.columns c
            INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'pct.COMPTE_CATEGORIE');
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        var map = new Dictionary<string, (string Type, bool Nullable, bool Identity)>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            map[reader.GetString(0)] = (reader.GetString(1), Convert.ToBoolean(reader[2]), Convert.ToBoolean(reader[3]));
        }

        Assert.True(map["IdCompteCategorie"].Identity);
        Assert.Equal("bigint", map["IdCompteCategorie"].Type);
        Assert.Equal("bigint", map["FK_Compte"].Type);
        Assert.False(map["FK_Compte"].Nullable);
        Assert.Equal("bigint", map["FK_CategorieCompte"].Type);
        Assert.False(map["FK_CategorieCompte"].Nullable);
        Assert.Equal("date", map["DateDebut"].Type);
        Assert.False(map["DateDebut"].Nullable);
        Assert.Equal("date", map["DateFin"].Type);
        Assert.True(map["DateFin"].Nullable);
    }

    private async Task CleanupNumerosAsync(params string[] numeros)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        foreach (var n in numeros)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [pct].[COMPTE_CATEGORIE] WHERE [FK_Compte] IN (SELECT [IdCompte] FROM [pct].[COMPTE] WHERE [NumeroCompte] = {n});");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] = {n};");
        }
    }

    private async Task CleanupRelationIdsAsync(params long[] ids)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        foreach (var id in ids)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [pct].[COMPTE_CATEGORIE] WHERE [IdCompteCategorie] = {id};");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var authRepo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var utilisateur = await authRepo.FindByIdAsync(4)
            ?? throw new InvalidOperationException("UTILISATEUR Id=4 (admin.snel) introuvable.");
        var profils = await authRepo.ListProfilsAsync(utilisateur.IdUtilisateur);
        var individuelles = await authRepo.ListPermissionsIndividuellesAsync(utilisateur.IdUtilisateur);
        var authUser = AuthService.MapUser(utilisateur, profils, individuelles);
        var (token, _) = tokenService.CreateToken(authUser);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed record Refs(
        string IdBanque,
        long IdDirection,
        string CodeType,
        long IdDevise,
        string IdProvince,
        long IdCategorie,
        long IdCategorie2);

    private sealed class ApiMessage
    {
        public string? Message { get; set; }
    }
}
