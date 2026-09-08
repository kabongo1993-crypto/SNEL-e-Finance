using System.Diagnostics;
using System.Net.Http.Headers;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BudgetWeb.IntegrationTests;

/// <summary>Validation HTTP du pipeline PDF pièce caisse optimisé.</summary>
public class PieceCaissePdfPipelineIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public PieceCaissePdfPipelineIntegrationTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.AddProvider(PerfLogCaptureProvider.Instance));
            });
        });
        _output = output;
    }

    [Fact]
    public async Task PieceCaissePdf_CasEtabli_Et_NonEtabli_Et_Inexistant()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
            return;

        await using var scope = _factory.Services.CreateAsyncScope();
        var authRepo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var utilisateur = await authRepo.FindByIdAsync(4);
        if (utilisateur is null)
        {
            _output.WriteLine("Utilisateur #4 introuvable — test ignoré.");
            return;
        }

        var profils = await authRepo.ListProfilsAsync(utilisateur.IdUtilisateur);
        var individuelles = await authRepo.ListPermissionsIndividuellesAsync(utilisateur.IdUtilisateur);
        var authUser = AuthService.MapUser(utilisateur, profils, individuelles);
        var (token, _) = tokenService.CreateToken(authUser);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        const long idDemandeEtablie = 50;

        // Cas 1 — pièce établie (demande #50)
        PerfLogCaptureProvider.Instance.Clear();
        var sw = Stopwatch.StartNew();
        var ok = await client.GetAsync(
            $"/api/v1/demandes-paiement/{idDemandeEtablie}/piece-caisse/pdf?inline=true");
        sw.Stop();

        if (ok.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _output.WriteLine($"Demande #{idDemandeEtablie} introuvable — cas établi ignoré.");
        }
        else
        {
            var pdf = await ok.Content.ReadAsByteArrayAsync();
            _output.WriteLine($"Cas1 établi — HTTP {(int)ok.StatusCode} — client {sw.ElapsedMilliseconds} ms — {pdf.Length / 1024} KB");
            if (ok.IsSuccessStatusCode)
            {
                Assert.Equal(0x25, pdf[0]);
                Assert.Equal(0x50, pdf[1]);
            }

            foreach (var line in PerfLogCaptureProvider.Instance.Lines)
                _output.WriteLine("  " + line);
        }

        // Cas 2 — pièce caisse non établie : couvert par les tests unitaires Cas2_*

        // Cas 4 — DPM inexistante
        PerfLogCaptureProvider.Instance.Clear();
        sw.Restart();
        var inexistant = await client.GetAsync(
            "/api/v1/demandes-paiement/999999999/piece-caisse/pdf?inline=true");
        sw.Stop();
        _output.WriteLine($"Cas4 inexistant — HTTP {(int)inexistant.StatusCode} — client {sw.ElapsedMilliseconds} ms");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, inexistant.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 5000, "Le 404 doit être quasi immédiat.");
    }

    private sealed class PerfLogCaptureProvider : ILoggerProvider
    {
        public static PerfLogCaptureProvider Instance { get; } = new();

        public List<string> Lines { get; } = new();

        public void Clear() => Lines.Clear();

        public ILogger CreateLogger(string categoryName) => new PerfLogger(this);

        public void Dispose() { }

        private sealed class PerfLogger(PerfLogCaptureProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel < LogLevel.Information)
                    return;

                var message = formatter(state, exception);
                if (!message.Contains("[PERF]", StringComparison.Ordinal))
                    return;

                owner.Lines.Add(message);
            }
        }
    }
}
