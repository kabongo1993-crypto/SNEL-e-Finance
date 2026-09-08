using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BudgetWeb.IntegrationTests;

/// <summary>
/// CRUD réel pct.PROVINCE (BD_SNEL) via l'API in-process.
/// Identifiant métier VARCHAR. Idempotent. Ne seed pas KIN/KIND/KISA
/// en dehors des créations de test demandées.
/// </summary>
public class ProvinceReferentielSqlTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string IdRename = "ITPRV";
    private const string IdRenameNew = "ITPRVX";
    private const string LibelleRename = "Province test CRUD PRV";
    private const string LibelleRenameModifie = "Province test CRUD PRV modifiée";
    private const string NumeroCompteTest = "__IT_PROVINCE_FK__";

    private readonly WebApplicationFactory<Program> _factory;

    public ProvinceReferentielSqlTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Provinces_CrudReel_IdProvinceEstVarcharMetier()
    {
        var client = await CreateAuthenticatedClientAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
            await AssertSchemaVarcharAsync(db);
        }

        var list0 = await client.GetFromJsonAsync<List<ProvinceDto>>(
            "/api/v1/provinces?actifsSeulement=false");
        Assert.NotNull(list0);
        Assert.All(list0, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.IdProvince));
            Assert.False(int.TryParse(p.IdProvince, out _), "IdProvince ne doit pas être un entier généré.");
        });

        var emptyId = await client.PostAsJsonAsync(
            "/api/v1/provinces",
            new CreateProvinceRequest("   ", "X"));
        Assert.Equal(HttpStatusCode.BadRequest, emptyId.StatusCode);

        var emptyLibelle = await client.PostAsJsonAsync(
            "/api/v1/provinces",
            new CreateProvinceRequest("X", "   "));
        Assert.Equal(HttpStatusCode.BadRequest, emptyLibelle.StatusCode);

        var kin = await EnsureProvinceAsync(client, list0, "KIN", "Kinshasa");
        await EnsureProvinceAsync(client, list0, "KIND", "Province KIND");
        await EnsureProvinceAsync(client, list0, "KISA", "Province KISA");
        Assert.Equal("KIN", kin.IdProvince);
        Assert.Equal("Kinshasa", kin.Libelle);

        var byKin = await client.GetFromJsonAsync<ProvinceDto>("/api/v1/provinces/KIN");
        Assert.NotNull(byKin);
        Assert.Equal("KIN", byKin.IdProvince);

        var dupId = await client.PostAsJsonAsync(
            "/api/v1/provinces",
            new CreateProvinceRequest(" KIN ", "Autre Kinshasa"));
        Assert.Equal(HttpStatusCode.BadRequest, dupId.StatusCode);

        var dupLibelle = await client.PostAsJsonAsync(
            "/api/v1/provinces",
            new CreateProvinceRequest("ITDUP", " Kinshasa "));
        Assert.Equal(HttpStatusCode.BadRequest, dupLibelle.StatusCode);

        var renameExisting = list0.FirstOrDefault(p =>
            string.Equals(p.IdProvince, IdRename, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.IdProvince, IdRenameNew, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Libelle, LibelleRename, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Libelle, LibelleRenameModifie, StringComparison.OrdinalIgnoreCase));

        ProvinceDto renamed;
        if (renameExisting is null)
        {
            var createResp = await client.PostAsJsonAsync(
                "/api/v1/provinces",
                new CreateProvinceRequest(IdRename, LibelleRename, true));
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
            renamed = (await createResp.Content.ReadFromJsonAsync<ProvinceDto>())!;
        }
        else
        {
            var reset = await client.PutAsJsonAsync(
                $"/api/v1/provinces/{Uri.EscapeDataString(renameExisting.IdProvince)}",
                new UpdateProvinceRequest(IdRename, LibelleRename, true));
            reset.EnsureSuccessStatusCode();
            renamed = (await reset.Content.ReadFromJsonAsync<ProvinceDto>())!;
        }

        Assert.Equal(IdRename, renamed.IdProvince);

        var updateLibelle = await client.PutAsJsonAsync(
            $"/api/v1/provinces/{IdRename}",
            new UpdateProvinceRequest(IdRename, LibelleRenameModifie, true));
        updateLibelle.EnsureSuccessStatusCode();

        var renameOk = await client.PutAsJsonAsync(
            $"/api/v1/provinces/{IdRename}",
            new UpdateProvinceRequest(IdRenameNew, LibelleRenameModifie, true));
        renameOk.EnsureSuccessStatusCode();
        var afterRename = await client.GetFromJsonAsync<ProvinceDto>($"/api/v1/provinces/{IdRenameNew}");
        Assert.Equal(IdRenameNew, afterRename!.IdProvince);

        var off = await client.PutAsJsonAsync(
            $"/api/v1/provinces/{IdRenameNew}",
            new UpdateProvinceRequest(IdRenameNew, LibelleRenameModifie, false));
        off.EnsureSuccessStatusCode();
        var on = await client.PutAsJsonAsync(
            $"/api/v1/provinces/{IdRenameNew}",
            new UpdateProvinceRequest(IdRenameNew, LibelleRenameModifie, true));
        on.EnsureSuccessStatusCode();

        var deleteResp = await client.DeleteAsync($"/api/v1/provinces/{IdRenameNew}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleteResp.StatusCode);

        await using var scope2 = _factory.Services.CreateAsyncScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<BudgetDbContext>();
        await AssertCompteFkProvinceAsync(db2, client, "KIN");
    }

    private static async Task<ProvinceDto> EnsureProvinceAsync(
        HttpClient client,
        List<ProvinceDto> list0,
        string id,
        string libelle)
    {
        var existing = list0.FirstOrDefault(p =>
            string.Equals(p.IdProvince, id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            var reset = await client.PutAsJsonAsync(
                $"/api/v1/provinces/{Uri.EscapeDataString(existing.IdProvince)}",
                new UpdateProvinceRequest(id, libelle, true));
            reset.EnsureSuccessStatusCode();
            return (await reset.Content.ReadFromJsonAsync<ProvinceDto>())!;
        }

        var create = await client.PostAsJsonAsync(
            "/api/v1/provinces",
            new CreateProvinceRequest(id, libelle, true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<ProvinceDto>())!;
    }

    private static async Task AssertSchemaVarcharAsync(BudgetDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT OBJECT_NAME(c.object_id), c.name, ty.name, c.max_length, c.is_nullable, c.is_identity
            FROM sys.columns c
            INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE (c.object_id = OBJECT_ID(N'pct.PROVINCE') AND c.name = N'IdProvince')
               OR (c.object_id = OBJECT_ID(N'pct.COMPTE') AND c.name = N'FK_Province')
            ORDER BY OBJECT_NAME(c.object_id) DESC;
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<(string Table, string Col, string Type, int MaxLen, bool Nullable, bool Identity)>();
        while (await reader.ReadAsync())
        {
            rows.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt16(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5)));
        }

        var province = Assert.Single(rows, r => r.Table == "PROVINCE" && r.Col == "IdProvince");
        Assert.Equal("varchar", province.Type);
        Assert.Equal(20, province.MaxLen);
        Assert.False(province.Nullable);
        Assert.False(province.Identity);

        var compte = Assert.Single(rows, r => r.Table == "COMPTE" && r.Col == "FK_Province");
        Assert.Equal("varchar", compte.Type);
        Assert.Equal(20, compte.MaxLen);
        Assert.True(compte.Nullable);
        Assert.False(compte.Identity);
    }

    private async Task AssertCompteFkProvinceAsync(BudgetDbContext db, HttpClient client, string idProvince)
    {
        var banque = await db.Banques.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.BANQUE est vide.");
        var direction = await db.DirectionsTresorerie.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.DIRECTION est vide.");
        var devise = await db.Devises.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("dpm.DEVISE est vide.");
        var typeFct = await db.TypesCompte.AsNoTracking().FirstOrDefaultAsync(t => t.Code == "FCT")
            ?? throw new InvalidOperationException("TYPE_COMPTE FCT introuvable.");

        var numeroX = NumeroCompteTest + "_X";
        var numeroN = NumeroCompteTest + "_N";

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] IN ({NumeroCompteTest}, {numeroX}, {numeroN});");

        try
        {
            var codeFct = typeFct.Code;
            var idDir = direction.IdDirection;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [pct].[COMPTE]
                    ([NumeroCompte], [LibelleCompte], [FK_Banque], [FK_Direction], [FK_TypeCompte], [FK_Devise], [FK_Province], [Actif])
                VALUES
                    ({NumeroCompteTest}, N'Test FK province', {banque.IdBanque}, {idDir}, {codeFct}, {devise.IdDevise}, {idProvince}, 1);
                """);

            var compte = await db.ComptesFinanciers.AsNoTracking()
                .FirstAsync(c => c.NumeroCompte == NumeroCompteTest);
            Assert.Equal(idProvince, compte.FK_Province);

            var refuseRename = await client.PutAsJsonAsync(
                $"/api/v1/provinces/{idProvince}",
                new UpdateProvinceRequest("KIN2", "Kinshasa", true));
            Assert.Equal(HttpStatusCode.BadRequest, refuseRename.StatusCode);
            var refuseBody = await refuseRename.Content.ReadAsStringAsync();
            Assert.Contains("identifiant ne peut pas être modifié", refuseBody, StringComparison.OrdinalIgnoreCase);

            var inexistant = "INEXISTANT";
            var inexistantEx = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO [pct].[COMPTE]
                        ([NumeroCompte], [LibelleCompte], [FK_Banque], [FK_Direction], [FK_TypeCompte], [FK_Devise], [FK_Province], [Actif])
                    VALUES
                        ({numeroX}, N'Test FK inexistant', {banque.IdBanque}, {idDir}, {codeFct}, {devise.IdDevise}, {inexistant}, 1);
                    """);
            });
            Assert.Contains("FK_PCT_COMPTE_Province", FlattenSql(inexistantEx), StringComparison.OrdinalIgnoreCase);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [pct].[COMPTE]
                    ([NumeroCompte], [LibelleCompte], [FK_Banque], [FK_Direction], [FK_TypeCompte], [FK_Devise], [FK_Province], [Actif])
                VALUES
                    ({numeroN}, N'Test FK null', {banque.IdBanque}, {idDir}, {codeFct}, {devise.IdDevise}, {null}, 1);
                """);
            var compteNull = await db.ComptesFinanciers.AsNoTracking()
                .FirstAsync(c => c.NumeroCompte == numeroN);
            Assert.Null(compteNull.FK_Province);

            var sqlDelete = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM [pct].[PROVINCE] WHERE [IdProvince] = {idProvince};");
            });
            Assert.Contains("FK_PCT_COMPTE_Province", FlattenSql(sqlDelete), StringComparison.OrdinalIgnoreCase);

            var off = await client.PutAsJsonAsync(
                $"/api/v1/provinces/{idProvince}",
                new UpdateProvinceRequest(idProvince, "Kinshasa", false));
            off.EnsureSuccessStatusCode();

            var compteApres = await db.ComptesFinanciers.AsNoTracking()
                .FirstAsync(c => c.IdCompte == compte.IdCompte);
            Assert.Equal(idProvince, compteApres.FK_Province);

            var on = await client.PutAsJsonAsync(
                $"/api/v1/provinces/{idProvince}",
                new UpdateProvinceRequest(idProvince, "Kinshasa", true));
            on.EnsureSuccessStatusCode();
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] IN ({NumeroCompteTest}, {numeroX}, {numeroN});");
        }
    }

    private static string FlattenSql(Exception ex)
    {
        var parts = new List<string> { ex.Message };
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
            parts.Add(inner.Message);
        return string.Join(" | ", parts);
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
