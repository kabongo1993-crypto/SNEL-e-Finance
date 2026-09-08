using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Auth;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BudgetWeb.IntegrationTests;

/// <summary>Mesures réelles workflow Établir — lit les logs [PERF][ETABLISSEMENT] côté serveur.</summary>
public class EtablissementPerfMeasurementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public EtablissementPerfMeasurementTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.AddProvider(EtablissementPerfLogCaptureProvider.Instance));
            });
        });
        _output = output;
    }

    [Fact]
    public async Task Mesures_Etablissement_Cas1_Cas2_Cas3()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
        {
            _output.WriteLine("Connection string absente — mesures ignorées.");
            return;
        }

        EtablissementPerfLogCaptureProvider.Instance.Clear();

        await using var scope = _factory.Services.CreateAsyncScope();
        var authRepo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();

        var utilisateur = await authRepo.FindByIdAsync(4);
        if (utilisateur is null)
        {
            _output.WriteLine("Utilisateur #4 introuvable — mesures ignorées.");
            return;
        }

        var profils = await authRepo.ListProfilsAsync(utilisateur.IdUtilisateur);
        var individuelles = await authRepo.ListPermissionsIndividuellesAsync(utilisateur.IdUtilisateur);
        var authUser = AuthService.MapUser(utilisateur, profils, individuelles);
        var (token, _) = tokenService.CreateToken(authUser);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        const long idDemandePrincipal = 50;

        await LogDemandeContextAsync(db, idDemandePrincipal, "Cas principal");

        // Warm-up
        var warm = await client.GetAsync($"/api/v1/demandes-paiement/{idDemandePrincipal}/complet");
        if (warm.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _output.WriteLine($"Demande #{idDemandePrincipal} introuvable — mesures ignorées.");
            return;
        }

        warm.EnsureSuccessStatusCode();

        // Cas 1 — idempotent pièce caisse (instrument déjà établi sur #50)
        await MeasurePostThenRefresh(
            client,
            idDemandePrincipal,
            "/api/v1/demandes-paiement/{0}/piece-caisse",
            "{}",
            "Cas 1 Pièce caisse (idempotent)");

        // Cas 2 — refresh seul (simule onRefresh frontend sans POST)
        await MeasureGet(
            client,
            $"/api/v1/demandes-paiement/{idDemandePrincipal}/complet",
            "Cas 2 Refresh GET /complet seul");

        // Cas 3 — idempotent billet conversion si applicable
        var demande = await db.DemandesPaiement.AsNoTracking()
            .Include(d => d.BilletConversion)
            .Include(d => d.PieceCaisse)
            .FirstOrDefaultAsync(d => d.IdDemandePaiement == idDemandePrincipal);

        if (demande is not null
            && BilletConversionRules.NecessiteBillet(demande.ModePaiementSollicite, demande.Devise)
            && demande.BilletConversion is not null)
        {
            await MeasurePostThenRefresh(
                client,
                idDemandePrincipal,
                "/api/v1/demandes-paiement/{0}/billet-conversion",
                "{}",
                "Cas 3 Billet conversion (idempotent)");
        }
        else
        {
            _output.WriteLine("Cas 3 Billet conversion — ignoré (non applicable ou non établi).");
        }

        // Cas complexité — demande EN_TRAITEMENT_DPM avec le plus de pièces jointes
        var complexId = await db.DemandesPaiement.AsNoTracking()
            .Where(d => d.Statut == StatutDemandePaiement.EnTraitementDpm)
            .Select(d => new
            {
                d.IdDemandePaiement,
                NbPieces = d.PiecesJointes.Count,
            })
            .OrderByDescending(x => x.NbPieces)
            .Select(x => x.IdDemandePaiement)
            .FirstOrDefaultAsync();

        if (complexId > 0 && complexId != idDemandePrincipal)
        {
            await LogDemandeContextAsync(db, complexId, "Cas complexité");
            await MeasurePostThenRefresh(
                client,
                complexId,
                "/api/v1/demandes-paiement/{0}/piece-caisse",
                "{}",
                $"Cas complexité Pièce caisse #{complexId}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== VÉRIFICATION GetDetailReload=0 sur POST ===");
        foreach (var line in EtablissementPerfLogCaptureProvider.Instance.Lines)
        {
            if (line.Contains("Tag=PIECE_CAISSE", StringComparison.Ordinal)
                && line.Contains("GetDetailReload=", StringComparison.Ordinal))
            {
                Assert.Contains("GetDetailReload=0ms", line);
            }
        }

        _output.WriteLine("");
        _output.WriteLine("=== LOGS [PERF][ETABLISSEMENT] CAPTURÉS ===");
        foreach (var line in EtablissementPerfLogCaptureProvider.Instance.Lines)
            _output.WriteLine(line);
    }

    private async Task LogDemandeContextAsync(BudgetDbContext db, long idDemande, string label)
    {
        var row = await db.DemandesPaiement.AsNoTracking()
            .Include(d => d.Beneficiaires)
            .Include(d => d.Imputations)
            .Include(d => d.PiecesJointes)
            .Include(d => d.ValidationsEntite)
            .Include(d => d.PieceCaisse)
            .Include(d => d.BonProvisoire)
            .Include(d => d.MinuteCheque)
            .Include(d => d.BilletConversion)
            .FirstOrDefaultAsync(d => d.IdDemandePaiement == idDemande);

        if (row is null)
        {
            _output.WriteLine($"{label} — demande #{idDemande} introuvable.");
            return;
        }

        _output.WriteLine(
            $"{label} — Demande #{idDemande} | Statut={row.Statut} | Mode={row.ModePaiementSollicite} | Devise={row.Devise} | " +
            $"Bénéf={row.Beneficiaires.Count} | Imput={row.Imputations.Count} | Pièces={row.PiecesJointes.Count} | " +
            $"Valid={row.ValidationsEntite.Count} | PieceCaisse={(row.PieceCaisse is not null ? "oui" : "non")} | " +
            $"Bon={(row.BonProvisoire is not null ? "oui" : "non")} | Minute={(row.MinuteCheque is not null ? "oui" : "non")} | " +
            $"Billet={(row.BilletConversion is not null ? "oui" : "non")}");
    }

    private async Task MeasurePostThenRefresh(
        HttpClient client,
        long idDemande,
        string postUrlTemplate,
        string jsonBody,
        string label)
    {
        var postUrl = string.Format(postUrlTemplate, idDemande);
        EtablissementPerfLogCaptureProvider.Instance.ClearRecent();

        var swPost = Stopwatch.StartNew();
        var postResponse = await client.PostAsync(
            postUrl,
            new StringContent(jsonBody, Encoding.UTF8, "application/json"));
        swPost.Stop();

        if (!postResponse.IsSuccessStatusCode)
        {
            _output.WriteLine($"{label} POST — HTTP {(int)postResponse.StatusCode} — {swPost.ElapsedMilliseconds} ms client");
            _output.WriteLine(await postResponse.Content.ReadAsStringAsync());
            return;
        }

        await postResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"{label} POST — client {swPost.ElapsedMilliseconds} ms");
        foreach (var line in EtablissementPerfLogCaptureProvider.Instance.RecentLines)
            _output.WriteLine("  POST " + line);

        EtablissementPerfLogCaptureProvider.Instance.ClearRecent();
        var swRefresh = Stopwatch.StartNew();
        var refreshResponse = await client.GetAsync($"/api/v1/demandes-paiement/{idDemande}/complet");
        swRefresh.Stop();

        if (!refreshResponse.IsSuccessStatusCode)
        {
            _output.WriteLine($"{label} REFRESH — HTTP {(int)refreshResponse.StatusCode} — {swRefresh.ElapsedMilliseconds} ms client");
            return;
        }

        await refreshResponse.Content.ReadAsStringAsync();
        var totalClient = swPost.ElapsedMilliseconds + swRefresh.ElapsedMilliseconds;
        _output.WriteLine(
            $"{label} REFRESH — client {swRefresh.ElapsedMilliseconds} ms | TOTAL POST+REFRESH client {totalClient} ms");
        foreach (var line in EtablissementPerfLogCaptureProvider.Instance.RecentLines)
            _output.WriteLine("  REFRESH " + line);
    }

    private async Task MeasureGet(HttpClient client, string url, string label)
    {
        EtablissementPerfLogCaptureProvider.Instance.ClearRecent();
        var sw = Stopwatch.StartNew();
        var response = await client.GetAsync(url);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"{label} — HTTP {(int)response.StatusCode} — {sw.ElapsedMilliseconds} ms client");
            return;
        }

        await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{label} — client {sw.ElapsedMilliseconds} ms");
        foreach (var line in EtablissementPerfLogCaptureProvider.Instance.RecentLines)
            _output.WriteLine("  " + line);
    }

    private sealed class EtablissementPerfLogCaptureProvider : ILoggerProvider
    {
        public static EtablissementPerfLogCaptureProvider Instance { get; } = new();

        public List<string> Lines { get; } = new();
        public List<string> RecentLines { get; } = new();

        public void Clear()
        {
            Lines.Clear();
            RecentLines.Clear();
        }

        public void ClearRecent() => RecentLines.Clear();

        public ILogger CreateLogger(string categoryName) => new PerfLogger(this);

        public void Dispose() { }

        private sealed class PerfLogger(EtablissementPerfLogCaptureProvider owner) : ILogger
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
                if (!message.Contains("[PERF][ETABLISSEMENT]", StringComparison.Ordinal))
                    return;

                owner.Lines.Add(message);
                owner.RecentLines.Add(message);
            }
        }
    }
}
