using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BudgetWeb.IntegrationTests;

/// <summary>
/// Mesure locale GET /api/v1/demandes-paiement/{id} (consultation légère).
/// Ignore si la base n'est pas disponible.
/// </summary>
public class DemandePaiementConsultationPerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public DemandePaiementConsultationPerformanceTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task GetConsultation_Mesure_Temps_Reponse()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var scope = _factory.Services.CreateAsyncScope();
        var authRepo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var utilisateur = await authRepo.FindByIdAsync(4);
        if (utilisateur is null)
            return;

        var profils = await authRepo.ListProfilsAsync(utilisateur.IdUtilisateur);
        var individuelles = await authRepo.ListPermissionsIndividuellesAsync(utilisateur.IdUtilisateur);
        var authUser = AuthService.MapUser(utilisateur, profils, individuelles);
        var (token, _) = tokenService.CreateToken(authUser);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        foreach (var id in new[] { 43L, 44L })
        {
            // Warm-up
            var warm = await client.GetAsync($"/api/v1/demandes-paiement/{id}");
            if (warm.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _output.WriteLine($"Demande #{id} introuvable — mesure ignorée.");
                continue;
            }

            warm.EnsureSuccessStatusCode();

            var sw1 = Stopwatch.StartNew();
            var r1 = await client.GetAsync($"/api/v1/demandes-paiement/{id}");
            sw1.Stop();
            r1.EnsureSuccessStatusCode();

            var sw2 = Stopwatch.StartNew();
            var r2 = await client.GetAsync($"/api/v1/demandes-paiement/{id}");
            sw2.Stop();
            r2.EnsureSuccessStatusCode();

            var json = await r1.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            Assert.False(doc.RootElement.TryGetProperty("motDePasseHash", out _));
            Assert.False(doc.RootElement.ToString().Contains("MotDePasseHash", StringComparison.Ordinal));

            _output.WriteLine($"GET consultation #{id} — 1er appel : {sw1.ElapsedMilliseconds} ms");
            _output.WriteLine($"GET consultation #{id} — 2e appel  : {sw2.ElapsedMilliseconds} ms");
        }
    }
}
