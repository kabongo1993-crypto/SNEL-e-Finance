using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BudgetWeb.IntegrationTests;

/// <summary>
/// CRUD réel pct.GROUPE_TYPE_COMPTE (BD_SNEL) via l'API in-process.
/// Idempotent. Ne seed pas les groupes historiques.
/// </summary>
public class GroupeTypeCompteReferentielSqlTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string LibelleTest = "Groupe test CRUD GTC";
    private const string LibelleTestModifie = "Groupe test CRUD GTC modifié";

    private readonly WebApplicationFactory<Program> _factory;

    public GroupeTypeCompteReferentielSqlTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GroupesTypesComptes_CrudReel_EcritPctGroupeTypeCompte()
    {
        var client = await CreateAuthenticatedClientAsync();

        var list0 = await client.GetFromJsonAsync<List<GroupeTypeCompteDto>>(
            "/api/v1/groupes-types-comptes?actifsSeulement=false");
        Assert.NotNull(list0);
        Assert.All(list0, g => Assert.True(g.IdGroupeTypeCompte > 0));

        var emptyResp = await client.PostAsJsonAsync(
            "/api/v1/groupes-types-comptes",
            new CreateGroupeTypeCompteRequest("   ", true));
        Assert.Equal(HttpStatusCode.BadRequest, emptyResp.StatusCode);

        if (list0.Any(g => string.Equals(g.Libelle, "Comptes courants", StringComparison.OrdinalIgnoreCase)))
        {
            var dupHist = await client.PostAsJsonAsync(
                "/api/v1/groupes-types-comptes",
                new CreateGroupeTypeCompteRequest("Comptes courants", true));
            Assert.Equal(HttpStatusCode.BadRequest, dupHist.StatusCode);
            var dupBody = await dupHist.Content.ReadFromJsonAsync<ApiMessage>();
            Assert.Contains("existe déjà", dupBody?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        var existing = list0.FirstOrDefault(g =>
            string.Equals(g.Libelle, LibelleTest, StringComparison.OrdinalIgnoreCase)
            || string.Equals(g.Libelle, LibelleTestModifie, StringComparison.OrdinalIgnoreCase));

        GroupeTypeCompteDto created;
        if (existing is null)
        {
            var createResp = await client.PostAsJsonAsync(
                "/api/v1/groupes-types-comptes",
                new CreateGroupeTypeCompteRequest(LibelleTest, true));
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
            created = (await createResp.Content.ReadFromJsonAsync<GroupeTypeCompteDto>())!;
        }
        else
        {
            var reset = await client.PutAsJsonAsync(
                $"/api/v1/groupes-types-comptes/{existing.IdGroupeTypeCompte}",
                new UpdateGroupeTypeCompteRequest(LibelleTest, true));
            reset.EnsureSuccessStatusCode();
            created = (await reset.Content.ReadFromJsonAsync<GroupeTypeCompteDto>())!;
        }

        Assert.True(created.IdGroupeTypeCompte > 0);
        Assert.DoesNotContain("-", created.IdGroupeTypeCompte.ToString());
        Assert.Equal(LibelleTest, created.Libelle);
        Assert.True(created.Actif);

        var byId = await client.GetFromJsonAsync<GroupeTypeCompteDto>(
            $"/api/v1/groupes-types-comptes/{created.IdGroupeTypeCompte}");
        Assert.NotNull(byId);
        Assert.Equal(created.IdGroupeTypeCompte, byId.IdGroupeTypeCompte);

        var dupResp = await client.PostAsJsonAsync(
            "/api/v1/groupes-types-comptes",
            new CreateGroupeTypeCompteRequest(LibelleTest, true));
        Assert.Equal(HttpStatusCode.BadRequest, dupResp.StatusCode);

        var updateResp = await client.PutAsJsonAsync(
            $"/api/v1/groupes-types-comptes/{created.IdGroupeTypeCompte}",
            new UpdateGroupeTypeCompteRequest(LibelleTestModifie, true));
        updateResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(created.IdGroupeTypeCompte, actif: true, libelle: LibelleTestModifie));

        var offResp = await client.PutAsJsonAsync(
            $"/api/v1/groupes-types-comptes/{created.IdGroupeTypeCompte}",
            new UpdateGroupeTypeCompteRequest(LibelleTestModifie, false));
        offResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(created.IdGroupeTypeCompte, actif: false));

        var onResp = await client.PutAsJsonAsync(
            $"/api/v1/groupes-types-comptes/{created.IdGroupeTypeCompte}",
            new UpdateGroupeTypeCompteRequest(LibelleTestModifie, true));
        onResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql(created.IdGroupeTypeCompte, actif: true));

        var list = await client.GetFromJsonAsync<List<GroupeTypeCompteDto>>(
            "/api/v1/groupes-types-comptes?actifsSeulement=false");
        Assert.NotNull(list);
        Assert.Contains(list, g => g.IdGroupeTypeCompte == created.IdGroupeTypeCompte);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var sqlCount = await db.GroupesTypeCompte.CountAsync();
        Assert.Equal(list.Count, sqlCount);
        Assert.True(await db.GroupesTypeCompte.AllAsync(g => g.IdGroupeTypeCompte > 0));
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
        var row = await db.GroupesTypeCompte.AsNoTracking()
            .FirstOrDefaultAsync(g => g.IdGroupeTypeCompte == id);
        if (row is null) return false;
        if (row.Actif != actif) return false;
        if (libelle is not null && row.Libelle != libelle) return false;
        return true;
    }

    private sealed class ApiMessage
    {
        public string? Message { get; set; }
    }
}
