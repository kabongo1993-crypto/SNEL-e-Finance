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
/// CRUD réel pct.TYPE_COMPTE (BD_SNEL) via l'API in-process.
/// Identifiant = Code. Idempotent. Ne seed pas FCT/CAISSE.
/// </summary>
public class TypeCompteReferentielSqlTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CodeTest = "TESTTYPE";
    private const string LibelleTest = "Type test CRUD";
    private const string LibelleModifie = "Type test CRUD modifié";
    private const string NumeroCompteTest = "__IT_TYPECOMPTE_FCT__";

    private readonly WebApplicationFactory<Program> _factory;

    public TypeCompteReferentielSqlTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TypesComptes_CrudReel_CodeEstIdentifiant()
    {
        var client = await CreateAuthenticatedClientAsync();

        var groupes = await client.GetFromJsonAsync<List<GroupeTypeCompteDto>>(
            "/api/v1/groupes-types-comptes?actifsSeulement=true");
        Assert.NotNull(groupes);
        var groupe = groupes.FirstOrDefault(g => g.Actif)
            ?? throw new InvalidOperationException("Aucun groupe actif dans pct.GROUPE_TYPE_COMPTE.");

        var list0 = await client.GetFromJsonAsync<List<TypeCompteDto>>(
            "/api/v1/types-comptes?actifsSeulement=false");
        Assert.NotNull(list0);
        Assert.All(list0, t => Assert.False(string.IsNullOrWhiteSpace(t.Code)));

        var fct = list0.FirstOrDefault(t => string.Equals(t.Code, "FCT", StringComparison.OrdinalIgnoreCase));
        if (fct is not null)
        {
            Assert.Equal("FCT", fct.Code);
            var byFct = await client.GetFromJsonAsync<TypeCompteDto>("/api/v1/types-comptes/FCT");
            Assert.NotNull(byFct);
            Assert.Equal("FCT", byFct.Code);
        }

        var emptyCode = await client.PostAsJsonAsync(
            "/api/v1/types-comptes",
            new CreateTypeCompteRequest("  ", "X", groupe.IdGroupeTypeCompte));
        Assert.Equal(HttpStatusCode.BadRequest, emptyCode.StatusCode);

        var emptyLibelle = await client.PostAsJsonAsync(
            "/api/v1/types-comptes",
            new CreateTypeCompteRequest("X", "  ", groupe.IdGroupeTypeCompte));
        Assert.Equal(HttpStatusCode.BadRequest, emptyLibelle.StatusCode);

        var missingGroupe = await client.PostAsJsonAsync(
            "/api/v1/types-comptes",
            new CreateTypeCompteRequest("XMISSING", "X", 9_999_999));
        Assert.Equal(HttpStatusCode.BadRequest, missingGroupe.StatusCode);

        if (fct is not null)
        {
            var dup = await client.PostAsJsonAsync(
                "/api/v1/types-comptes",
                new CreateTypeCompteRequest("FCT", "FCT", groupe.IdGroupeTypeCompte));
            Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        }

        var existing = list0.FirstOrDefault(t =>
            string.Equals(t.Code, CodeTest, StringComparison.OrdinalIgnoreCase));

        TypeCompteDto created;
        if (existing is null)
        {
            var createResp = await client.PostAsJsonAsync(
                "/api/v1/types-comptes",
                new CreateTypeCompteRequest(CodeTest, LibelleTest, groupe.IdGroupeTypeCompte, true));
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
            created = (await createResp.Content.ReadFromJsonAsync<TypeCompteDto>())!;
        }
        else
        {
            var reset = await client.PutAsJsonAsync(
                $"/api/v1/types-comptes/{Uri.EscapeDataString(existing.Code)}",
                new UpdateTypeCompteRequest(CodeTest, LibelleTest, groupe.IdGroupeTypeCompte, true));
            reset.EnsureSuccessStatusCode();
            created = (await reset.Content.ReadFromJsonAsync<TypeCompteDto>())!;
        }

        Assert.Equal(CodeTest, created.Code);
        Assert.Equal(groupe.IdGroupeTypeCompte, created.IdGroupeTypeCompte);
        Assert.False(string.IsNullOrWhiteSpace(created.GroupeLibelle));

        var byCode = await client.GetFromJsonAsync<TypeCompteDto>($"/api/v1/types-comptes/{CodeTest}");
        Assert.NotNull(byCode);
        Assert.Equal(CodeTest, byCode.Code);

        var dupTest = await client.PostAsJsonAsync(
            "/api/v1/types-comptes",
            new CreateTypeCompteRequest(CodeTest, LibelleTest, groupe.IdGroupeTypeCompte));
        Assert.Equal(HttpStatusCode.BadRequest, dupTest.StatusCode);

        var altGroupe = groupes.FirstOrDefault(g => g.Actif && g.IdGroupeTypeCompte != groupe.IdGroupeTypeCompte);
        var targetGroupeId = altGroupe?.IdGroupeTypeCompte ?? groupe.IdGroupeTypeCompte;

        var updateResp = await client.PutAsJsonAsync(
            $"/api/v1/types-comptes/{CodeTest}",
            new UpdateTypeCompteRequest(CodeTest, LibelleModifie, targetGroupeId, true));
        updateResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(CodeTest, actif: true, libelle: LibelleModifie, idGroupe: targetGroupeId));

        var renamed = await client.PutAsJsonAsync(
            $"/api/v1/types-comptes/{CodeTest}",
            new UpdateTypeCompteRequest("TEST", LibelleModifie, targetGroupeId, true));
        renamed.EnsureSuccessStatusCode();
        var renamedDto = (await renamed.Content.ReadFromJsonAsync<TypeCompteDto>())!;
        Assert.Equal("TEST", renamedDto.Code);
        Assert.True(await ExistsInSql("TEST", actif: true, libelle: LibelleModifie));
        Assert.False(await ExistsInSql(CodeTest, actif: true));

        var restore = await client.PutAsJsonAsync(
            "/api/v1/types-comptes/TEST",
            new UpdateTypeCompteRequest(CodeTest, LibelleModifie, targetGroupeId, true));
        restore.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(CodeTest, actif: true, libelle: LibelleModifie));

        var offResp = await client.PutAsJsonAsync(
            $"/api/v1/types-comptes/{CodeTest}",
            new UpdateTypeCompteRequest(CodeTest, LibelleModifie, targetGroupeId, false));
        offResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(CodeTest, actif: false));

        var onResp = await client.PutAsJsonAsync(
            $"/api/v1/types-comptes/{CodeTest}",
            new UpdateTypeCompteRequest(CodeTest, LibelleModifie, targetGroupeId, true));
        onResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(CodeTest, actif: true));

        var list = await client.GetFromJsonAsync<List<TypeCompteDto>>("/api/v1/types-comptes?actifsSeulement=false");
        Assert.NotNull(list);
        Assert.Contains(list, t => string.Equals(t.Code, CodeTest, StringComparison.OrdinalIgnoreCase));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var sqlCount = await db.TypesCompte.CountAsync();
        Assert.Equal(list.Count, sqlCount);
        Assert.True(await db.TypesCompte.AllAsync(t => t.Code.Length > 0 && t.FK_GroupeTypeCompte > 0));

        await AssertCompteFkTypeCompteAsync(db, client, fct is not null);
    }

    private async Task AssertCompteFkTypeCompteAsync(
        BudgetDbContext db,
        HttpClient client,
        bool fctExiste)
    {
        if (!fctExiste)
            return;

        var banque = await db.Banques.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.BANQUE est vide — impossible de tester COMPTE.FK_TypeCompte.");
        var direction = await db.DirectionsTresorerie.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.DIRECTION est vide — impossible de tester COMPTE.FK_TypeCompte.");
        var devise = await db.Devises.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("dpm.DEVISE est vide — impossible de tester COMPTE.FK_TypeCompte.");

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] = {NumeroCompteTest};");

        try
        {
            const string codeFct = "FCT";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [pct].[COMPTE]
                    ([NumeroCompte], [LibelleCompte], [FK_Banque], [FK_Direction], [FK_TypeCompte], [FK_Devise], [Actif])
                VALUES
                    ({NumeroCompteTest}, N'Test FK type FCT', {banque.IdBanque}, {direction.IdDirection}, {codeFct}, {devise.IdDevise}, 1);
                """);

            var inserted = await db.ComptesFinanciers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.NumeroCompte == NumeroCompteTest);
            Assert.NotNull(inserted);
            Assert.Equal("FCT", inserted.FK_TypeCompte);

            var fctRow = await db.TypesCompte.AsNoTracking().FirstAsync(t => t.Code == "FCT");
            var refuse = await client.PutAsJsonAsync(
                "/api/v1/types-comptes/FCT",
                new UpdateTypeCompteRequest("FCT2", "FCT", fctRow.FK_GroupeTypeCompte, true));
            Assert.Equal(HttpStatusCode.BadRequest, refuse.StatusCode);
            var refuseBody = await refuse.Content.ReadAsStringAsync();
            Assert.Contains("utilisé par un ou plusieurs comptes", refuseBody, StringComparison.OrdinalIgnoreCase);

            const string codeInexistant = "INEXISTANT";
            var numeroX = NumeroCompteTest + "_X";
            var inexistant = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO [pct].[COMPTE]
                        ([NumeroCompte], [LibelleCompte], [FK_Banque], [FK_Direction], [FK_TypeCompte], [FK_Devise], [Actif])
                    VALUES
                        ({numeroX}, N'Test FK inexistant', {banque.IdBanque}, {direction.IdDirection}, {codeInexistant}, {devise.IdDevise}, 1);
                    """);
            });
            Assert.Contains("FK_PCT_COMPTE_TypeCompte", FlattenSql(inexistant), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            var numeroX = NumeroCompteTest + "_X";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] IN ({NumeroCompteTest}, {numeroX});");
        }
    }

    private static string FlattenSql(Exception ex)
    {
        var parts = new List<string> { ex.Message };
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
            parts.Add(inner.Message);
        if (ex is DbUpdateException dbEx && dbEx.InnerException is SqlException sql)
            parts.Add(sql.Message);
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

    private async Task<bool> ExistsInSql(string code, bool actif, string? libelle = null, long? idGroupe = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var row = await db.TypesCompte.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code);
        if (row is null) return false;
        if (row.Actif != actif) return false;
        if (libelle is not null && row.Libelle != libelle) return false;
        if (idGroupe is not null && row.FK_GroupeTypeCompte != idGroupe) return false;
        return true;
    }
}
