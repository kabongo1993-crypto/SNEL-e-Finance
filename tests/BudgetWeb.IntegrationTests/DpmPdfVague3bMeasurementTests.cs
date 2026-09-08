using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Auth;
using BudgetWeb.Infrastructure.Documents;
using BudgetWeb.Infrastructure.Documents.Experiments;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BudgetWeb.IntegrationTests;

public class DpmPdfVague3bMeasurementTests
{
    private readonly ITestOutputHelper _output;
    private const long IdDemande = 50;
    private const int WarmRuns = 5;

    public DpmPdfVague3bMeasurementTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Mesures_Vague_3B_Scenarios_A_D()
    {
        _output.WriteLine("=== VAGUE 3B — Mesures PDF DPM ===");
        _output.WriteLine("");

var results = new List<ScenarioResult>();

        results.Add(await RunScenarioAsync(
            "A — Baseline images originales",
            useBaselineRenderer: true,
            runWarmupBeforePdf: false));

        results.Add(await RunScenarioAsync(
            "B — Production assets optimises",
            useBaselineRenderer: false,
            runWarmupBeforePdf: false));

        results.Add(await RunScenarioAsync(
            "C — Production + cache memoire (Lazy)",
            useBaselineRenderer: false,
            runWarmupBeforePdf: false));

        results.Add(await RunScenarioAsync(
            "D — Production + cache + warmup avant PDF1",
            useBaselineRenderer: false,
            runWarmupBeforePdf: true));

        _output.WriteLine("=== TABLEAU COMPARATIF ===");
        _output.WriteLine("Scenario | PDF1 | PDF2 | PDF3 | Warm moy | QuestPDF warm | Pages | Size KB");
        foreach (var r in results)
        {
            _output.WriteLine(
                $"{r.Label} | {r.Pdf1Ms} | {r.Pdf2Ms} | {r.Pdf3Ms} | {r.WarmAvgMs:F0} | {r.WarmQuestPdfAvg:F0} | {r.Pages:F1} | {r.SizeKb:F0}");
        }

        var baseline = results[0];
        var prod = results[1];
        var gain = baseline.WarmQuestPdfAvg - prod.WarmQuestPdfAvg;
        var pct = baseline.WarmQuestPdfAvg > 0 ? gain * 100.0 / baseline.WarmQuestPdfAvg : 0;
        _output.WriteLine("");
        _output.WriteLine($"Gain QuestPDF warm A->B: {gain:+0;-0} ms ({pct:+0.0;-0.0}%)");
        _output.WriteLine($"PDF1 B sans warmup vs D avec warmup: {results[1].Pdf1QuestPdfMs} vs {results[3].Pdf1QuestPdfMs} ms QuestPDF");
    }

    private async Task<ScenarioResult> RunScenarioAsync(
        string label,
        bool useBaselineRenderer,
        bool runWarmupBeforePdf)
    {
        _output.WriteLine($"--- {label} ---");
        long warmupScenarioMs = 0;
        if (runWarmupBeforePdf)
        {
            warmupScenarioMs = QuestPdfWarmup.Run();
            _output.WriteLine($"  Warmup scenario: {warmupScenarioMs} ms");
        }

        PerfLogCaptureProvider.Instance.Clear();
        await using var factory = CreateFactory(useBaselineRenderer);
        var client = await CreateAuthenticatedClientAsync(factory);
        if (client is null)
        {
            _output.WriteLine("  Auth impossible — scenario ignore.");
            return new ScenarioResult { Label = label };
        }

        var url = $"/api/v1/demandes-paiement/{IdDemande}/document-pdf?inline=true";
        var detailResp = await client.GetAsync($"/api/v1/demandes-paiement/{IdDemande}");
        if (detailResp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _output.WriteLine($"  Demande #{IdDemande} introuvable.");
            return new ScenarioResult { Label = label };
        }
        detailResp.EnsureSuccessStatusCode();

        var cold = await GenerateOnceAsync(client, url);
        _output.WriteLine($"  COLD PDF1: HTTP {cold.HttpMs}ms QuestPDF {cold.QuestPdfMs}ms");

        var pdf2 = await GenerateOnceAsync(client, url);
        var pdf3 = await GenerateOnceAsync(client, url);

        var warmRuns = new List<RunMeasurement>();
        for (var i = 0; i < WarmRuns; i++)
            warmRuns.Add(await GenerateOnceAsync(client, url));

        var warmQuest = warmRuns.Average(r => r.QuestPdfMs);
        _output.WriteLine(
            $"  WARM avg ({WarmRuns}): HTTP {warmRuns.Average(r => r.HttpMs):F0}ms QuestPDF {warmQuest:F0}ms audit {warmRuns.Average(r => r.AuditMs):F0}ms data {warmRuns.Average(r => r.GetPdfDataMs):F0}ms");
        _output.WriteLine("");

        return new ScenarioResult
        {
            Label = label,
            WarmupMs = warmupScenarioMs,
            Pdf1Ms = cold.HttpMs,
            Pdf1QuestPdfMs = cold.QuestPdfMs,
            Pdf2Ms = pdf2.HttpMs,
            Pdf3Ms = pdf3.HttpMs,
            WarmAvgMs = warmRuns.Average(r => r.HttpMs),
            WarmQuestPdfAvg = warmQuest,
            Pages = warmRuns.Average(r => r.PageCount),
            SizeKb = warmRuns.Average(r => r.PdfSizeKb),
        };
    }

    private static WebApplicationFactory<Program> CreateFactory(bool useBaselineRenderer)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                if (useBaselineRenderer)
                {
                    services.RemoveAll<IDemandePaiementDocumentRenderer>();
                    services.AddSingleton<IDemandePaiementDocumentRenderer>(
                        _ => new QuestPdfDemandePaiementExperimentRenderer(DpmPdfExperimentVariant.Baseline));
                }

                services.AddLogging(logging => logging.AddProvider(PerfLogCaptureProvider.Instance));
            });
        });

    private static async Task<HttpClient?> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var authRepo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var utilisateur = await authRepo.FindByIdAsync(4);
        if (utilisateur is null) return null;

        var profils = await authRepo.ListProfilsAsync(utilisateur.IdUtilisateur);
        var individuelles = await authRepo.ListPermissionsIndividuellesAsync(utilisateur.IdUtilisateur);
        var authUser = AuthService.MapUser(utilisateur, profils, individuelles);
        var (token, _) = tokenService.CreateToken(authUser);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<RunMeasurement> GenerateOnceAsync(HttpClient client, string url)
    {
        PerfLogCaptureProvider.Instance.ClearRecent();
        var sw = Stopwatch.StartNew();
        var response = await client.GetAsync(url);
        sw.Stop();
        response.EnsureSuccessStatusCode();
        var pdf = await response.Content.ReadAsByteArrayAsync();
        var perf = ParsePerfLog(PerfLogCaptureProvider.Instance.RecentLines.LastOrDefault());
        return new RunMeasurement(
            sw.ElapsedMilliseconds,
            perf.TotalMs,
            perf.GetPdfDataMs,
            perf.MappingMs,
            perf.AuditMs,
            perf.QuestPdfMs,
            pdf.Length / 1024,
            CountPdfPages(pdf));
    }

    private static PerfSnapshot ParsePerfLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return default;
        static long Extract(string text, string key)
        {
            var m = Regex.Match(text, $@"{Regex.Escape(key)}=(\d+)ms");
            return m.Success ? long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }
        return new PerfSnapshot(
            Extract(line, "GetDemandePaiementPdfData"),
            Extract(line, "Mapping"),
            Extract(line, "Audit"),
            Extract(line, "QuestPDF"),
            Extract(line, "Total"));
    }

    private static int CountPdfPages(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        return Regex.Matches(raw, @"/Type\s*/Page\b").Count;
    }

    private sealed record RunMeasurement(
        long HttpMs, long BackendMs, long GetPdfDataMs, long MappingMs,
        long AuditMs, long QuestPdfMs, int PdfSizeKb, int PageCount);

    private readonly struct PerfSnapshot(long g, long m, long a, long q, long t)
    {
        public long GetPdfDataMs { get; } = g;
        public long MappingMs { get; } = m;
        public long AuditMs { get; } = a;
        public long QuestPdfMs { get; } = q;
        public long TotalMs { get; } = t;
    }

    private sealed class ScenarioResult
    {
        public string Label { get; init; } = "";
        public long WarmupMs { get; init; }
        public long Pdf1Ms { get; init; }
        public long Pdf1QuestPdfMs { get; init; }
        public long Pdf2Ms { get; init; }
        public long Pdf3Ms { get; init; }
        public double WarmAvgMs { get; init; }
        public double WarmQuestPdfAvg { get; init; }
        public double Pages { get; init; }
        public double SizeKb { get; init; }
    }

    private sealed class PerfLogCaptureProvider : ILoggerProvider
    {
        public static PerfLogCaptureProvider Instance { get; } = new();
        public List<string> Lines { get; } = new();
        public List<string> RecentLines { get; } = new();
        public void Clear() { Lines.Clear(); RecentLines.Clear(); }
        public void ClearRecent() => RecentLines.Clear();
        public ILogger CreateLogger(string categoryName) => new PerfLogger(this);
        public void Dispose() { }

        private sealed class PerfLogger(PerfLogCaptureProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? ex, Func<TState, Exception?, string> fmt)
            {
                if (logLevel < LogLevel.Information) return;
                var msg = fmt(state, ex);
                if (!msg.Contains("[PERF][DPM-PDF]", StringComparison.Ordinal)) return;
                owner.Lines.Add(msg);
                owner.RecentLines.Add(msg);
            }
        }
    }
}