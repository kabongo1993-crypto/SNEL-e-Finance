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
/// CRUD réel pct.DIRECTION (BD_SNEL) via l'API in-process.
/// Idempotent. Ne seed pas Kinshasa/Lubumbashi.
/// </summary>
public class DirectionReferentielSqlTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string LibelleTest = "Direction test CRUD DIR";
    private const string LibelleModifie = "Direction test CRUD DIR modifiée";
    private const string NumeroCompteTest = "__IT_DIRECTION_FK__";

    private readonly WebApplicationFactory<Program> _factory;

    public DirectionReferentielSqlTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Directions_CrudReel_EcritPctDirection()
    {
        var client = await CreateAuthenticatedClientAsync();

        var list0 = await client.GetFromJsonAsync<List<DirectionDto>>(
            "/api/v1/directions?actifsSeulement=false");
        Assert.NotNull(list0);
        Assert.All(list0, d => Assert.True(d.IdDirection > 0));

        var emptyResp = await client.PostAsJsonAsync(
            "/api/v1/directions",
            new CreateDirectionRequest("   ", true));
        Assert.Equal(HttpStatusCode.BadRequest, emptyResp.StatusCode);

        if (list0.Any(d => string.Equals(d.Libelle, "Kinshasa", StringComparison.OrdinalIgnoreCase)))
        {
            var dupHist = await client.PostAsJsonAsync(
                "/api/v1/directions",
                new CreateDirectionRequest(" Kinshasa ", true));
            Assert.Equal(HttpStatusCode.BadRequest, dupHist.StatusCode);
        }

        var existing = list0.FirstOrDefault(d =>
            string.Equals(d.Libelle, LibelleTest, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Libelle, LibelleModifie, StringComparison.OrdinalIgnoreCase));

        DirectionDto created;
        if (existing is null)
        {
            var createResp = await client.PostAsJsonAsync(
                "/api/v1/directions",
                new CreateDirectionRequest(LibelleTest, true));
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
            created = (await createResp.Content.ReadFromJsonAsync<DirectionDto>())!;
        }
        else
        {
            var reset = await client.PutAsJsonAsync(
                $"/api/v1/directions/{existing.IdDirection}",
                new UpdateDirectionRequest(LibelleTest, true));
            reset.EnsureSuccessStatusCode();
            created = (await reset.Content.ReadFromJsonAsync<DirectionDto>())!;
        }

        Assert.True(created.IdDirection > 0);
        Assert.DoesNotContain("-", created.IdDirection.ToString());
        Assert.Equal(LibelleTest, created.Libelle);
        Assert.True(created.Actif);

        var byId = await client.GetFromJsonAsync<DirectionDto>($"/api/v1/directions/{created.IdDirection}");
        Assert.NotNull(byId);
        Assert.Equal(created.IdDirection, byId.IdDirection);

        var dupResp = await client.PostAsJsonAsync(
            "/api/v1/directions",
            new CreateDirectionRequest(LibelleTest, true));
        Assert.Equal(HttpStatusCode.BadRequest, dupResp.StatusCode);

        var updateResp = await client.PutAsJsonAsync(
            $"/api/v1/directions/{created.IdDirection}",
            new UpdateDirectionRequest(LibelleModifie, true));
        updateResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(created.IdDirection, actif: true, libelle: LibelleModifie));

        var offResp = await client.PutAsJsonAsync(
            $"/api/v1/directions/{created.IdDirection}",
            new UpdateDirectionRequest(LibelleModifie, false));
        offResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(created.IdDirection, actif: false));

        var onResp = await client.PutAsJsonAsync(
            $"/api/v1/directions/{created.IdDirection}",
            new UpdateDirectionRequest(LibelleModifie, true));
        onResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(created.IdDirection, actif: true));

        var deleteResp = await client.DeleteAsync($"/api/v1/directions/{created.IdDirection}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleteResp.StatusCode);
        Assert.True(await ExistsInSql(created.IdDirection, actif: true));

        var list = await client.GetFromJsonAsync<List<DirectionDto>>("/api/v1/directions?actifsSeulement=false");
        Assert.NotNull(list);
        Assert.Contains(list, d => d.IdDirection == created.IdDirection);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        Assert.Equal(list.Count, await db.DirectionsTresorerie.CountAsync());
        Assert.True(await db.DirectionsTresorerie.AllAsync(d => d.IdDirection > 0));

        await AssertCompteFkDirectionAsync(db, client, created.IdDirection);
    }

    private async Task AssertCompteFkDirectionAsync(BudgetDbContext db, HttpClient client, long idDirection)
    {
        var banque = await db.Banques.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("pct.BANQUE est vide.");
        var devise = await db.Devises.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("dpm.DEVISE est vide.");
        var typeFct = await db.TypesCompte.AsNoTracking().FirstOrDefaultAsync(t => t.Code == "FCT")
            ?? throw new InvalidOperationException("TYPE_COMPTE FCT introuvable.");

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] = {NumeroCompteTest};");

        try
        {
            var codeFct = typeFct.Code;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [pct].[COMPTE]
                    ([NumeroCompte], [LibelleCompte], [FK_Banque], [FK_Direction], [FK_TypeCompte], [FK_Devise], [Actif])
                VALUES
                    ({NumeroCompteTest}, N'Test FK direction', {banque.IdBanque}, {idDirection}, {codeFct}, {devise.IdDevise}, 1);
                """);

            var compte = await db.ComptesFinanciers.AsNoTracking()
                .FirstAsync(c => c.NumeroCompte == NumeroCompteTest);
            Assert.Equal(idDirection, compte.FK_Direction);

            var sqlDelete = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM [pct].[DIRECTION] WHERE [IdDirection] = {idDirection};");
            });
            Assert.Contains("FK_PCT_COMPTE_Direction", sqlDelete.Message, StringComparison.OrdinalIgnoreCase);

            var direction = await db.DirectionsTresorerie.AsNoTracking()
                .FirstAsync(d => d.IdDirection == idDirection);
            var off = await client.PutAsJsonAsync(
                $"/api/v1/directions/{idDirection}",
                new UpdateDirectionRequest(direction.Libelle, false));
            off.EnsureSuccessStatusCode();

            var compteApres = await db.ComptesFinanciers.AsNoTracking()
                .FirstAsync(c => c.IdCompte == compte.IdCompte);
            Assert.Equal(idDirection, compteApres.FK_Direction);
            Assert.Equal(compte.NumeroCompte, compteApres.NumeroCompte);

            var on = await client.PutAsJsonAsync(
                $"/api/v1/directions/{idDirection}",
                new UpdateDirectionRequest(direction.Libelle, true));
            on.EnsureSuccessStatusCode();
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM [pct].[COMPTE] WHERE [NumeroCompte] = {NumeroCompteTest};");
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

    private async Task<bool> ExistsInSql(long id, bool actif, string? libelle = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var row = await db.DirectionsTresorerie.AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdDirection == id);
        if (row is null) return false;
        if (row.Actif != actif) return false;
        if (libelle is not null && row.Libelle != libelle) return false;
        return true;
    }
}
