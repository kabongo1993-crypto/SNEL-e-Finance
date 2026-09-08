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
/// Raccordement réel pct.BANQUE (BD_SNEL) via l'API in-process.
/// Idempotent : réexécutable sans seed Excel ni mocks.
/// </summary>
public class BanqueReferentielSqlTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BanqueReferentielSqlTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Banques_CrudEtImport_EcriventPctBanque()
    {
        var client = await CreateAuthenticatedClientAsync();
        Assert.NotNull(client);

        var list0 = await client.GetFromJsonAsync<List<BanqueDto>>("/api/v1/banques?actifsSeulement=false");
        Assert.NotNull(list0);

        var createResp = await client.PostAsJsonAsync(
            "/api/v1/banques",
            new CreateBanqueRequest("TESTBANK", "TEST BANK", "RDC", true));
        if (createResp.StatusCode == HttpStatusCode.BadRequest)
        {
            var updatedExisting = await client.PutAsJsonAsync(
                "/api/v1/banques/TESTBANK",
                new UpdateBanqueRequest("TEST BANK", "RDC", true));
            updatedExisting.EnsureSuccessStatusCode();
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        }

        var created = await client.GetFromJsonAsync<BanqueDto>("/api/v1/banques/TESTBANK");
        Assert.NotNull(created);
        Assert.Equal("TESTBANK", created.IdBanque);
        Assert.Equal("TEST BANK", created.LibelleBanque);
        Assert.Equal("RDC", created.Pays);
        Assert.True(created.Actif);

        Assert.True(await ExistsInSql("TESTBANK", actif: true, libelle: "TEST BANK"));

        var updateResp = await client.PutAsJsonAsync(
            "/api/v1/banques/TESTBANK",
            new UpdateBanqueRequest("TEST BANK SA", "RD Congo", true));
        updateResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql("TESTBANK", actif: true, libelle: "TEST BANK SA", pays: "RD Congo"));

        var offResp = await client.PutAsJsonAsync(
            "/api/v1/banques/TESTBANK",
            new UpdateBanqueRequest("TEST BANK SA", "RD Congo", false));
        offResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql("TESTBANK", actif: false));

        var onResp = await client.PutAsJsonAsync(
            "/api/v1/banques/TESTBANK",
            new UpdateBanqueRequest("TEST BANK SA", "RD Congo", true));
        onResp.EnsureSuccessStatusCode();
        Assert.True(await ExistsInSql("TESTBANK", actif: true));

        var importPayload = new ImportBanquesRequest(ExcelBanques);
        var importResp = await client.PostAsJsonAsync("/api/v1/banques/import", importPayload);
        importResp.EnsureSuccessStatusCode();
        var importResult = await importResp.Content.ReadFromJsonAsync<ImportBanquesResultDto>();
        Assert.NotNull(importResult);
        Assert.Equal(ExcelBanques.Count, importResult.Total);
        Assert.Equal(0, importResult.Erreurs);

        var list = await client.GetFromJsonAsync<List<BanqueDto>>("/api/v1/banques?actifsSeulement=false");
        Assert.NotNull(list);
        Assert.Contains(list, b => b.IdBanque == "TESTBANK");
        Assert.Contains(list, b => b.IdBanque == "AFRILAND");
        Assert.Contains(list, b => b.IdBanque == "RAWBANK");
        Assert.Contains(list, b => b.IdBanque == "BCC");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var sqlCount = await db.Banques.CountAsync();
        Assert.Equal(list.Count, sqlCount);
        Assert.True(sqlCount >= 26, $"pct.BANQUE devrait contenir TESTBANK + 25 lignes Excel (actuel={sqlCount}).");
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

    private async Task<bool> ExistsInSql(string id, bool actif, string? libelle = null, string? pays = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var row = await db.Banques.AsNoTracking().FirstOrDefaultAsync(b => b.IdBanque == id);
        if (row is null) return false;
        if (row.Actif != actif) return false;
        if (libelle is not null && row.LibelleBanque != libelle) return false;
        if (pays is not null && row.Pays != pays) return false;
        return true;
    }

    private static readonly IReadOnlyList<ImportBanqueItemRequest> ExcelBanques =
    [
        new("AFRILAND", "AFRILAND BANK", "Rdc"),
        new("ACCESSBANK", "ACCESS BANK", "Rdc"),
        new("BCC", "BCC", "Rdc"),
        new("BCDC", "BCDC", "Rdc"),
        new("BGFIBANK", "BGFIBANK", "Rdc"),
        new("RAWBANK", "RAWBANK", "Rdc"),
        new("TMB", "TRUST MERCHANT BANK", "Rdc"),
        new("ECOBANK", "ECOBANK RDC", "Rdc"),
        new("SOFIBANQUE", "SOFIBANQUE", "Rdc"),
        new("FBNBANK", "FBN BANK DRC", "Rdc"),
        new("CITIBANK", "CITIBANK RDC", "Rdc"),
        new("SCB", "STANDARD CHARTERED BANK", "Rdc"),
        new("UBA", "UNITED BANK FOR AFRICA", "Rdc"),
        new("BOA", "BANK OF AFRICA RDC", "Rdc"),
        new("BYBLOS", "BYBLOS BANK", "Rdc"),
        new("PROCREDIT", "PROCREDIT BANK", "Rdc"),
        new("ADVANS", "ADVANS BANQUE CONGO", "Rdc"),
        new("FIRSTBANK", "FIRST BANK DRC", "Rdc"),
        new("EQUITY", "EQUITY BCDC", "Rdc"),
        new("STANBIC", "STANBIC BANK", "Rdc"),
        new("BIC", "BANQUE INTERNATIONALE DE CREDIT", "Rdc"),
        new("IBANK", "I&M BANK", "Rdc"),
        new("SOFICOM", "SOFICOM", "Rdc"),
        new("CAISSESNEL", "CAISSE SNEL", "Rdc"),
        new("MINES", "BANQUE MINES", "Rdc"),
    ];
}
