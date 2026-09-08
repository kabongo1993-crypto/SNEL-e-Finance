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
/// CRUD réel pct.CATEGORIE_COMPTE (BD_SNEL) via l'API in-process.
/// Idempotent. Ne seed pas les catégories historiques.
/// </summary>
public class CategorieCompteReferentielSqlTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string LibelleTest = "Catégorie test CRUD CAT";
    private const string LibelleModifie = "Catégorie test CRUD CAT modifiée";
    private const string NumeroCompteTest = "__IT_CATEGORIE_FK__";

    private readonly WebApplicationFactory<Program> _factory;

    public CategorieCompteReferentielSqlTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CategoriesComptes_CrudReel_EcritPctCategorieCompte()
    {
        var client = await CreateAuthenticatedClientAsync();

        var list0 = await client.GetFromJsonAsync<List<CategorieCompteDto>>(
            "/api/v1/categories-comptes?actifsSeulement=false");
        Assert.NotNull(list0);
        Assert.All(list0, c => Assert.True(c.IdCategorieCompte > 0));

        var emptyResp = await client.PostAsJsonAsync(
            "/api/v1/categories-comptes",
            new CreateCategorieCompteRequest("   ", null, true));
        Assert.Equal(HttpStatusCode.BadRequest, emptyResp.StatusCode);

        if (list0.Any(c => string.Equals(c.Libelle, "Kinshasa", StringComparison.OrdinalIgnoreCase)))
        {
            var dupHist = await client.PostAsJsonAsync(
                "/api/v1/categories-comptes",
                new CreateCategorieCompteRequest(" Kinshasa ", "Kinshasa", true));
            Assert.Equal(HttpStatusCode.BadRequest, dupHist.StatusCode);
        }

        var existing = list0.FirstOrDefault(c =>
            string.Equals(c.Libelle, LibelleTest, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Libelle, LibelleModifie, StringComparison.OrdinalIgnoreCase));

        CategorieCompteDto created;
        if (existing is null)
        {
            var createResp = await client.PostAsJsonAsync(
                "/api/v1/categories-comptes",
                new CreateCategorieCompteRequest(LibelleTest, null, true));
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
            created = (await createResp.Content.ReadFromJsonAsync<CategorieCompteDto>())!;
        }
        else
        {
            var reset = await client.PutAsJsonAsync(
                $"/api/v1/categories-comptes/{existing.IdCategorieCompte}",
                new UpdateCategorieCompteRequest(LibelleTest, null, true));
            reset.EnsureSuccessStatusCode();
            created = (await reset.Content.ReadFromJsonAsync<CategorieCompteDto>())!;
        }

        Assert.True(created.IdCategorieCompte > 0);
        Assert.Equal(LibelleTest, created.Libelle);
        Assert.Null(created.Orientation);
        Assert.True(created.Actif);

        var byId = await client.GetFromJsonAsync<CategorieCompteDto>(
            $"/api/v1/categories-comptes/{created.IdCategorieCompte}");
        Assert.NotNull(byId);
        Assert.Equal(created.IdCategorieCompte, byId.IdCategorieCompte);

        var dupResp = await client.PostAsJsonAsync(
            "/api/v1/categories-comptes",
            new CreateCategorieCompteRequest(LibelleTest, "X", true));
        Assert.Equal(HttpStatusCode.BadRequest, dupResp.StatusCode);

        var updateLibelle = await client.PutAsJsonAsync(
            $"/api/v1/categories-comptes/{created.IdCategorieCompte}",
            new UpdateCategorieCompteRequest(LibelleModifie, "Orientation test", true));
        updateLibelle.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(created.IdCategorieCompte, true, LibelleModifie, "Orientation test"));

        var offResp = await client.PutAsJsonAsync(
            $"/api/v1/categories-comptes/{created.IdCategorieCompte}",
            new UpdateCategorieCompteRequest(LibelleModifie, "Orientation test", false));
        offResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(created.IdCategorieCompte, actif: false));

        var onResp = await client.PutAsJsonAsync(
            $"/api/v1/categories-comptes/{created.IdCategorieCompte}",
            new UpdateCategorieCompteRequest(LibelleModifie, "Orientation test", true));
        onResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(created.IdCategorieCompte, actif: true));

        var deleteResp = await client.DeleteAsync($"/api/v1/categories-comptes/{created.IdCategorieCompte}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleteResp.StatusCode);
        Assert.True(await ExistsInSql(created.IdCategorieCompte, actif: true));

        var list = await client.GetFromJsonAsync<List<CategorieCompteDto>>(
            "/api/v1/categories-comptes?actifsSeulement=false");
        Assert.NotNull(list);
        Assert.Contains(list, c => c.IdCategorieCompte == created.IdCategorieCompte);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        Assert.Equal(list.Count, await db.CategoriesCompte.CountAsync());
        Assert.True(await db.CategoriesCompte.AllAsync(c => c.IdCategorieCompte > 0));

        await AssertCompteCategorieFkAsync(db, client, created.IdCategorieCompte);
    }

    private async Task AssertCompteCategorieFkAsync(
        BudgetDbContext db,
        HttpClient client,
        long idCategorie)
    {
        var banque = await db.Banques.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.BANQUE est vide.");
        var direction = await db.DirectionsTresorerie.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.DIRECTION est vide.");
        var devise = await db.Devises.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("dpm.DEVISE est vide.");
        var typeFct = await db.TypesCompte.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == "FCT")
            ?? throw new InvalidOperationException("TYPE_COMPTE FCT introuvable.");
        var codeFct = typeFct.Code;

        var numeroX = NumeroCompteTest + "_X";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [pct].[COMPTE_CATEGORIE] WHERE [FK_Compte] IN (SELECT [IdCompte] FROM [pct].[COMPTE] WHERE [NumeroCompte] IN ({NumeroCompteTest}, {numeroX}));");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] IN ({NumeroCompteTest}, {numeroX});");

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [pct].[COMPTE]
                    ([NumeroCompte], [LibelleCompte], [FK_Banque], [FK_Direction], [FK_TypeCompte], [FK_Devise], [Actif])
                VALUES
                    ({NumeroCompteTest}, N'Test FK catégorie', {banque.IdBanque}, {direction.IdDirection}, {codeFct}, {devise.IdDevise}, 1);
                """);

            var compte = await db.ComptesFinanciers.AsNoTracking()
                .FirstAsync(c => c.NumeroCompte == NumeroCompteTest);

            var dateDebut = new DateOnly(2026, 1, 1);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [pct].[COMPTE_CATEGORIE]
                    ([FK_Compte], [FK_CategorieCompte], [DateDebut])
                VALUES
                    ({compte.IdCompte}, {idCategorie}, {dateDebut});
                """);

            var liaison = await db.ComptesCategories.AsNoTracking()
                .FirstAsync(cc => cc.FK_Compte == compte.IdCompte);
            Assert.Equal(idCategorie, liaison.FK_CategorieCompte);
            Assert.Null(liaison.DateFin);

            var sqlDelete = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM [pct].[CATEGORIE_COMPTE] WHERE [IdCategorieCompte] = {idCategorie};");
            });
            Assert.Contains("FK_PCT_COMPTE_CATEGORIE_Categorie", sqlDelete.Message, StringComparison.OrdinalIgnoreCase);

            var categorie = await db.CategoriesCompte.AsNoTracking()
                .FirstAsync(c => c.IdCategorieCompte == idCategorie);
            var off = await client.PutAsJsonAsync(
                $"/api/v1/categories-comptes/{idCategorie}",
                new UpdateCategorieCompteRequest(categorie.Libelle, categorie.Orientation, false));
            off.EnsureSuccessStatusCode();

            var liaisonApres = await db.ComptesCategories.AsNoTracking()
                .FirstAsync(cc => cc.IdCompteCategorie == liaison.IdCompteCategorie);
            Assert.Equal(liaison.FK_CategorieCompte, liaisonApres.FK_CategorieCompte);
            Assert.Equal(liaison.DateDebut, liaisonApres.DateDebut);
            Assert.Equal(liaison.DateFin, liaisonApres.DateFin);

            var on = await client.PutAsJsonAsync(
                $"/api/v1/categories-comptes/{idCategorie}",
                new UpdateCategorieCompteRequest(categorie.Libelle, categorie.Orientation, true));
            on.EnsureSuccessStatusCode();

            var dateFinInvalide = new DateOnly(2025, 12, 1);
            var dates = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO [pct].[COMPTE]
                        ([NumeroCompte], [LibelleCompte], [FK_Banque], [FK_Direction], [FK_TypeCompte], [FK_Devise], [Actif])
                    VALUES
                        ({numeroX}, N'Test dates', {banque.IdBanque}, {direction.IdDirection}, {codeFct}, {devise.IdDevise}, 1);
                    INSERT INTO [pct].[COMPTE_CATEGORIE]
                        ([FK_Compte], [FK_CategorieCompte], [DateDebut], [DateFin])
                    SELECT [IdCompte], {idCategorie}, {dateDebut}, {dateFinInvalide}
                    FROM [pct].[COMPTE] WHERE [NumeroCompte] = {numeroX};
                    """);
            });
            Assert.Contains("CK_PCT_COMPTE_CATEGORIE_Dates", dates.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [pct].[COMPTE_CATEGORIE] WHERE [FK_Compte] IN (SELECT [IdCompte] FROM [pct].[COMPTE] WHERE [NumeroCompte] IN ({NumeroCompteTest}, {numeroX}));");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] IN ({NumeroCompteTest}, {numeroX});");
        }
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

    private async Task<bool> ExistsInSql(
        long id,
        bool actif,
        string? libelle = null,
        string? orientation = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var row = await db.CategoriesCompte.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCategorieCompte == id);
        if (row is null) return false;
        if (row.Actif != actif) return false;
        if (libelle is not null && row.Libelle != libelle) return false;
        if (orientation is not null && row.Orientation != orientation) return false;
        return true;
    }
}
