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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BudgetWeb.IntegrationTests;

/// <summary>
/// Raccordement réel pct.COMPTE (BD_SNEL) via l'API in-process.
/// Pas de mock. Soft-delete uniquement. Import historique optionnel si COMPTE.xlsx est présent.
/// </summary>
public class CompteReferentielSqlTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string NumeroCrud = "__IT_CPT_CRUD__";
    private const string NumeroImportA = "__IT_CPT_IMP_A__";
    private const string NumeroImportB = "__IT_CPT_IMP_B__";
    private const long IdImportA = 910001;
    private const long IdImportB = 910002;
    private static readonly string ExcelPath = Path.Combine(@"d:\InstantFLOW\Trésorerie", "COMPTE.xlsx");

    private readonly WebApplicationFactory<Program> _factory;

    public CompteReferentielSqlTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Comptes_SchemaFkEtCrudSansSuppressionPhysique()
    {
        var client = await CreateAuthenticatedClientAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            await AssertSchemaAsync(db);
        }

        var refs = await LoadRefsAsync();

        await CleanupNumerosAsync(NumeroCrud);

        try
        {
            var create = await client.PostAsJsonAsync(
                "/api/v1/comptes",
                new CreateCompteFinancierRequest(
                    NumeroCrud,
                    "Compte test CRUD",
                    refs.IdBanque,
                    refs.IdDirection,
                    refs.CodeType,
                    refs.IdDevise,
                    refs.IdProvince,
                    null,
                    true));
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<CompteFinancierDto>();
            Assert.NotNull(created);
            Assert.True(created.IdCompte > 0);
            Assert.Equal(NumeroCrud, created.NumeroCompte);
            Assert.Equal(refs.IdBanque, created.IdBanque);
            Assert.Equal(refs.CodeType, created.CodeTypeCompte);
            Assert.Equal(refs.IdProvince, created.IdProvince);
            Assert.Null(created.IdUtilisateur);
            Assert.True(created.Actif);
            Assert.Null(created.DateCloture);

            var list = await client.GetFromJsonAsync<List<CompteFinancierDto>>("/api/v1/comptes?actifsSeulement=false");
            Assert.NotNull(list);
            Assert.Contains(list, c => c.NumeroCompte == NumeroCrud && c.IdBanque == refs.IdBanque);

            var byId = await client.GetFromJsonAsync<CompteFinancierDto>($"/api/v1/comptes/{created.IdCompte}");
            Assert.NotNull(byId);
            Assert.Equal("Compte test CRUD", byId.LibelleCompte);

            var update = await client.PutAsJsonAsync(
                $"/api/v1/comptes/{created.IdCompte}",
                new UpdateCompteFinancierRequest(
                    NumeroCrud,
                    "Compte test CRUD modifié",
                    refs.IdBanque,
                    refs.IdDirection,
                    refs.CodeType,
                    refs.IdDevise,
                    refs.IdProvince,
                    null,
                    true));
            update.EnsureSuccessStatusCode();
            var updated = await update.Content.ReadFromJsonAsync<CompteFinancierDto>();
            Assert.Equal("Compte test CRUD modifié", updated!.LibelleCompte);

            var off = await client.PutAsJsonAsync(
                $"/api/v1/comptes/{created.IdCompte}",
                new UpdateCompteFinancierRequest(
                    NumeroCrud,
                    "Compte test CRUD modifié",
                    refs.IdBanque,
                    refs.IdDirection,
                    refs.CodeType,
                    refs.IdDevise,
                    refs.IdProvince,
                    null,
                    false));
            off.EnsureSuccessStatusCode();
            var offDto = await off.Content.ReadFromJsonAsync<CompteFinancierDto>();
            Assert.False(offDto!.Actif);
            Assert.NotNull(offDto.DateCloture);

            var on = await client.PutAsJsonAsync(
                $"/api/v1/comptes/{created.IdCompte}",
                new UpdateCompteFinancierRequest(
                    NumeroCrud,
                    "Compte test CRUD modifié",
                    refs.IdBanque,
                    refs.IdDirection,
                    refs.CodeType,
                    refs.IdDevise,
                    refs.IdProvince,
                    null,
                    true));
            on.EnsureSuccessStatusCode();
            var onDto = await on.Content.ReadFromJsonAsync<CompteFinancierDto>();
            Assert.True(onDto!.Actif);
            Assert.Null(onDto.DateCloture);

            var delete = await client.DeleteAsync($"/api/v1/comptes/{created.IdCompte}");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);

            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            var stillThere = await db.ComptesFinanciers.AsNoTracking()
                .AnyAsync(c => c.IdCompte == created.IdCompte);
            Assert.True(stillThere);
        }
        finally
        {
            await CleanupNumerosAsync(NumeroCrud);
        }
    }

    [Fact]
    public async Task Comptes_PreviewErreurs_PuisImportIdentifiantsHistoriques()
    {
        var client = await CreateAuthenticatedClientAsync();
        var refs = await LoadRefsAsync();
        await CleanupIdsAsync(IdImportA, IdImportB);
        await CleanupNumerosAsync(NumeroImportA, NumeroImportB);

        try
        {
            var previewErr = await client.PostAsJsonAsync(
                "/api/v1/comptes/import/preview",
                new ImportComptesPreviewRequest("erreurs.xlsx",
                [
                    new ImportCompteRawRequest(2, "910010", "X1", "L", "1", "BANQUE_INCONNUE", refs.CodeType, "CDF", "36526", "1", "0", refs.IdProvince),
                    new ImportCompteRawRequest(3, "910011", "X2", "L", "1", refs.IdBanque, "TYPE_INCONNU", "CDF", "36526", "1", "0", refs.IdProvince),
                    new ImportCompteRawRequest(4, "910012", "X3", "L", "1", refs.IdBanque, refs.CodeType, "DEVISE_X", "36526", "1", "0", refs.IdProvince),
                    new ImportCompteRawRequest(5, "910013", "X4", "L", "1", refs.IdBanque, refs.CodeType, "CDF", "36526", "1", "0", "PROV_X"),
                    new ImportCompteRawRequest(6, "910014", NumeroImportA, "A", "1", refs.IdBanque, refs.CodeType, "CDF", "36526", "1", "0", refs.IdProvince),
                    new ImportCompteRawRequest(7, "910015", NumeroImportA, "A2", "1", refs.IdBanque, refs.CodeType, "CDF", "36526", "1", "0", refs.IdProvince),
                ]));
            previewErr.EnsureSuccessStatusCode();
            var preview = await previewErr.Content.ReadFromJsonAsync<ImportComptesPreviewDto>();
            Assert.NotNull(preview);
            Assert.Equal(6, preview.Resume.Analysees);
            Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Banque BANQUE_INCONNUE introuvable", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Type de compte TYPE_INCONNU introuvable", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Devise DEVISE_X introuvable", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(preview.Lignes, l => l.Resultat.Contains("Province PROV_X introuvable", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(preview.Lignes, l => l.Statut == "doublon_fichier");

            var importPayload = new ImportComptesRequest("mini.xlsx",
            [
                new ImportCompteRawRequest(2, IdImportA.ToString(CultureInfo.InvariantCulture), NumeroImportA, "Import A", refs.IdDirection.ToString(CultureInfo.InvariantCulture), refs.IdBanque, refs.CodeType, "CDF", "36526", "1", "0", refs.IdProvince),
                new ImportCompteRawRequest(3, IdImportB.ToString(CultureInfo.InvariantCulture), NumeroImportB, "Import B", refs.IdDirection.ToString(CultureInfo.InvariantCulture), refs.IdBanque, refs.CodeType, "EURO", "36526", "1", "0", null),
            ]);
            var previewOk = await client.PostAsJsonAsync("/api/v1/comptes/import/preview", new ImportComptesPreviewRequest("mini.xlsx", importPayload.Lignes));
            previewOk.EnsureSuccessStatusCode();
            var previewMini = await previewOk.Content.ReadFromJsonAsync<ImportComptesPreviewDto>();
            Assert.Equal(2, previewMini!.Resume.AImporter);

            var imported = await client.PostAsJsonAsync("/api/v1/comptes/import", importPayload);
            imported.EnsureSuccessStatusCode();
            var result = await imported.Content.ReadFromJsonAsync<ImportComptesResultDto>();
            Assert.Equal(2, result!.Importes);

            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            var a = await db.ComptesFinanciers.AsNoTracking().FirstAsync(c => c.IdCompte == IdImportA);
            Assert.Equal(NumeroImportA, a.NumeroCompte);
            Assert.Equal(refs.IdBanque, a.FK_Banque);
            Assert.Null(a.FK_Utilisateur);
            Assert.Equal(refs.IdProvince, a.FK_Province);
            var b = await db.ComptesFinanciers.AsNoTracking().FirstAsync(c => c.IdCompte == IdImportB);
            Assert.Null(b.FK_Province);
            Assert.Null(b.FK_Utilisateur);
        }
        finally
        {
            await CleanupIdsAsync(IdImportA, IdImportB);
            await CleanupNumerosAsync(NumeroImportA, NumeroImportB);
        }
    }

    [Fact]
    public async Task Comptes_ImportFichierHistoriqueCompteXlsx()
    {
        if (!File.Exists(ExcelPath))
            return;

        var client = await CreateAuthenticatedClientAsync();
        var lignes = LireCompteXlsx(ExcelPath);
        Assert.True(lignes.Count >= 390, $"COMPTE.xlsx devrait contenir ~391 lignes (actuel={lignes.Count}).");
        Assert.Contains(lignes, l => string.Equals(l.Banque, "RAWBANK", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lignes, l => string.Equals(l.TypeCompte, "FCT", StringComparison.OrdinalIgnoreCase));

        var previewResp = await client.PostAsJsonAsync(
            "/api/v1/comptes/import/preview",
            new ImportComptesPreviewRequest("COMPTE.xlsx", lignes));
        previewResp.EnsureSuccessStatusCode();
        var preview = await previewResp.Content.ReadFromJsonAsync<ImportComptesPreviewDto>();
        Assert.NotNull(preview);
        Assert.Equal(lignes.Count, preview.Resume.Analysees);
        Assert.True(
            preview.Resume.Doublons >= 1 || preview.Resume.Erreurs >= 1,
            "Le fichier historique doit signaler au moins un doublon ou une ligne en erreur. "
            + string.Join(" | ", preview.Lignes.Where(l => l.Statut is "erreur" or "doublon_fichier").Select(l => $"{l.LigneExcel}:{l.Resultat}").Take(20)));

        var erreursGroupées = string.Join(
            " | ",
            preview.Lignes
                .Where(l => l.Statut == "erreur")
                .GroupBy(l => l.Resultat)
                .Select(g => $"{g.Key} x{g.Count()}")
                .Take(20));
        Assert.True(
            preview.Resume.AImporter > 0 || preview.Resume.DejaExistants > 0,
            $"Aucune ligne importable. aImporter={preview.Resume.AImporter} erreurs={preview.Resume.Erreurs} doublons={preview.Resume.Doublons}. {erreursGroupées}");

        if (preview.Resume.AImporter > 0)
        {
            var importResp = await client.PostAsJsonAsync(
                "/api/v1/comptes/import",
                new ImportComptesRequest("COMPTE.xlsx", lignes));
            importResp.EnsureSuccessStatusCode();
            var result = await importResp.Content.ReadFromJsonAsync<ImportComptesResultDto>();
            Assert.NotNull(result);
            Assert.Equal(preview.Resume.AImporter, result.Importes);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var count = await db.ComptesFinanciers.CountAsync();
        var erreurs = string.Join(
            " | ",
            preview.Lignes
                .Where(l => l.Statut == "erreur")
                .GroupBy(l => l.Resultat)
                .Select(g => $"{g.Key} x{g.Count()}")
                .Take(20));
        Assert.True(
            count >= 380,
            $"pct.COMPTE devrait contenir les comptes historiques (actuel={count}). "
            + $"Preview aImporter={preview.Resume.AImporter} erreurs={preview.Resume.Erreurs} doublons={preview.Resume.Doublons} deja={preview.Resume.DejaExistants}. {erreurs}");
        var sansUser = await db.ComptesFinanciers.CountAsync(c => c.FK_Utilisateur == null);
        Assert.Equal(count, sansUser);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM pct.COMPTE c WHERE NOT EXISTS (SELECT 1 FROM pct.BANQUE b WHERE b.IdBanque = c.FK_Banque)) AS OrphelinsBanque,
              (SELECT COUNT(*) FROM pct.COMPTE c WHERE NOT EXISTS (SELECT 1 FROM pct.TYPE_COMPTE t WHERE t.Code = c.FK_TypeCompte)) AS OrphelinsType,
              (SELECT COUNT(*) FROM pct.COMPTE c WHERE c.FK_Province IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pct.PROVINCE p WHERE p.IdProvince = c.FK_Province)) AS OrphelinsProvince,
              (SELECT COUNT(*) FROM pct.COMPTE c WHERE NOT EXISTS (SELECT 1 FROM dpm.DEVISE d WHERE d.IdDevise = c.FK_Devise)) AS OrphelinsDevise,
              (SELECT COUNT(*) FROM pct.COMPTE c WHERE NOT EXISTS (SELECT 1 FROM pct.DIRECTION dir WHERE dir.IdDirection = c.FK_Direction)) AS OrphelinsDirection;
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.Equal(0, reader.GetInt32(1));
        Assert.Equal(0, reader.GetInt32(2));
        Assert.Equal(0, reader.GetInt32(3));
        Assert.Equal(0, reader.GetInt32(4));
    }

    private static List<ImportCompteRawRequest> LireCompteXlsx(string path)
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

        var colId = Col("idcompte");
        var colNumero = Col("numerocompte");
        var colLibelle = Col("libellecompte");
        var colDir = Col("direction");
        var colBanque = Col("banque");
        var colType = Col("typecompte");
        var colDevise = Col("devise");
        var colDateCrea = Col("datecreation");
        var colDateClot = Col("datecloture");
        var colEtat = Col("etat");
        var colProv = Col("idtprovince", "idprovince", "province");
        if (colId < 0 || colNumero < 0 || colLibelle < 0 || colBanque < 0 || colType < 0 || colDevise < 0)
            throw new InvalidOperationException("Colonnes COMPTE.xlsx introuvables.");

        var lignes = new List<ImportCompteRawRequest>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            lignes.Add(new ImportCompteRawRequest(
                row.RowNumber(),
                Cell(row, colId),
                Cell(row, colNumero),
                Cell(row, colLibelle),
                Cell(row, colDir),
                Cell(row, colBanque),
                Cell(row, colType),
                Cell(row, colDevise),
                Cell(row, colDateCrea),
                Cell(row, colDateClot),
                Cell(row, colEtat),
                EmptyToNull(Cell(row, colProv))));
        }

        return lignes;
    }

    private static string FoldHeader(string value)
        => new string(value.Normalize().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string? EmptyToNull(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Cell(IXLRow row, int col)
    {
        if (col < 0) return "";
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return "";
        if (cell.TryGetValue(out double numeric))
        {
            if (Math.Abs(numeric - Math.Round(numeric)) < 0.0000001)
                return ((long)Math.Round(numeric)).ToString(CultureInfo.InvariantCulture);
            return numeric.ToString("G15", CultureInfo.InvariantCulture);
        }
        return cell.GetString().Trim();
    }

    private async Task<(string IdBanque, long IdDirection, string CodeType, long IdDevise, string IdProvince)> LoadRefsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var banque = await db.Banques.AsNoTracking().FirstOrDefaultAsync(b => b.IdBanque == "RAWBANK")
            ?? await db.Banques.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.BANQUE est vide.");
        var direction = await db.DirectionsTresorerie.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.DIRECTION est vide.");
        var type = await db.TypesCompte.AsNoTracking().FirstOrDefaultAsync(t => t.Code == "FCT")
            ?? await db.TypesCompte.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.TYPE_COMPTE est vide.");
        var devise = await db.Devises.AsNoTracking().FirstOrDefaultAsync(d => d.Code == "CDF")
            ?? await db.Devises.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("dpm.DEVISE est vide.");
        var province = await db.Provinces.AsNoTracking().FirstOrDefaultAsync(p => p.IdProvince == "KIN")
            ?? await db.Provinces.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.PROVINCE est vide.");
        return (banque.IdBanque, direction.IdDirection, type.Code, devise.IdDevise, province.IdProvince);
    }

    private static async Task AssertSchemaAsync(BudgetDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.name, ty.name, c.max_length, c.is_nullable, c.is_identity
            FROM sys.columns c
            INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'pct.COMPTE')
              AND c.name IN (N'IdCompte', N'FK_Banque', N'FK_TypeCompte', N'FK_Province', N'FK_Utilisateur', N'FK_Direction', N'FK_Devise');
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        var map = new Dictionary<string, (string Type, int MaxLen, bool Nullable, bool Identity)>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            map[reader.GetString(0)] = (
                reader.GetString(1),
                Convert.ToInt32(reader[2]),
                Convert.ToBoolean(reader[3]),
                Convert.ToBoolean(reader[4]));
        }

        Assert.True(map.ContainsKey("IdCompte"));
        Assert.Equal("bigint", map["IdCompte"].Type);
        Assert.True(map["IdCompte"].Identity);
        Assert.Equal("varchar", map["FK_Banque"].Type);
        Assert.Equal(50, map["FK_Banque"].MaxLen);
        Assert.False(map["FK_Banque"].Nullable);
        Assert.Equal("varchar", map["FK_TypeCompte"].Type);
        Assert.Equal(20, map["FK_TypeCompte"].MaxLen);
        Assert.False(map["FK_TypeCompte"].Nullable);
        Assert.Equal("varchar", map["FK_Province"].Type);
        Assert.Equal(20, map["FK_Province"].MaxLen);
        Assert.True(map["FK_Province"].Nullable);
        Assert.Equal("bigint", map["FK_Utilisateur"].Type);
        Assert.True(map["FK_Utilisateur"].Nullable);
        Assert.Equal("bigint", map["FK_Direction"].Type);
        Assert.False(map["FK_Direction"].Nullable);
        Assert.Equal("bigint", map["FK_Devise"].Type);
        Assert.False(map["FK_Devise"].Nullable);
    }

    private async Task CleanupNumerosAsync(params string[] numeros)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        foreach (var n in numeros)
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] = {n};");
    }

    private async Task CleanupIdsAsync(params long[] ids)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        foreach (var id in ids)
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [pct].[COMPTE] WHERE [IdCompte] = {id};");
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
}
