using System.Diagnostics;
using System.Net.Http.Headers;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BudgetWeb.IntegrationTests;

/// <summary>Mesure POST /api/v1/demandes-paiement/{id}/pieces après optimisation upload.</summary>
public class DemandePaiementPieceUploadPerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public DemandePaiementPieceUploadPerformanceTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task PostPiece_Mesure_Temps_Reponse()
    {
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

        const long idDemande = 43;
        var sizes = new (string label, int bytes)[]
        {
            ("100Ko", 100 * 1024),
            ("1Mo", 1024 * 1024),
            ("5Mo", 5 * 1024 * 1024),
            ("10Mo", 10 * 1024 * 1024),
        };

        foreach (var (label, size) in sizes)
        {
            var bytes = new byte[size];
            Random.Shared.NextBytes(bytes);
            bytes[0] = (byte)'%';
            bytes[1] = (byte)'P';
            bytes[2] = (byte)'D';
            bytes[3] = (byte)'F';

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fileContent, "fichier", $"bench-upload-{label}.pdf");
            form.Add(new StringContent("AUTRE"), "codeTypePiece");
            form.Add(new StringContent($"Bench upload {label}"), "libelle");

            var sw = Stopwatch.StartNew();
            var response = await client.PostAsync($"/api/v1/demandes-paiement/{idDemande}/pieces", form);
            sw.Stop();

            _output.WriteLine($"POST piece #{idDemande} {label} — {sw.ElapsedMilliseconds} ms — status {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                _output.WriteLine(await response.Content.ReadAsStringAsync());
                break;
            }
        }
    }
}
