using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Auth;
using BudgetWeb.Infrastructure.Documents.Experiments;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BudgetWeb.IntegrationTests;

/// <summary>
/// Expérience contrôlée PDF DPM #50 — variantes images / pagination (diagnostic, réversible).
/// </summary>
public class DpmPdfExperimentMeasurementTests
{
    private readonly ITestOutputHelper _output;

    public DpmPdfExperimentMeasurementTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Mesures_Variantes_Dpm_Pdf_50()
    {
        const long idDemande = 50;
        const int runsPerVariant = 3;
        var variants = new[]
        {
            DpmPdfExperimentVariant.Baseline,
            DpmPdfExperimentVariant.OptimizedImages,
            DpmPdfExperimentVariant.SimplifiedPagination,
            DpmPdfExperimentVariant.OptimizedImagesAndPagination,
        };

        var allResults = new List<VariantSummary>();

        foreach (var variant in variants)
        {
            PerfLogCaptureProvider.Instance.Clear();
            await using var factory = CreateFactory(variant);
            var client = await CreateAuthenticatedClientAsync(factory);
            if (client is null)
            {
                _output.WriteLine($"Auth impossible — variante {variant} ignorée.");
                continue;
            }

            var url = $"/api/v1/demandes-paiement/{idDemande}/document-pdf?inline=true";
            var detailResp = await client.GetAsync($"/api/v1/demandes-paiement/{idDemande}");
            if (detailResp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _output.WriteLine($"Demande #{idDemande} introuvable.");
                return;
            }

            detailResp.EnsureSuccessStatusCode();

            // Warm-up (hors statistiques)
            await GenerateOnceAsync(client, url);

            var runs = new List<RunMeasurement>();
            for (var i = 0; i < runsPerVariant; i++)
            {
                var run = await GenerateOnceAsync(client, url);
                runs.Add(run);
                _output.WriteLine(
                    $"  [{variant}] run {i + 1}/{runsPerVariant} — HTTP {run.HttpMs}ms | backend {run.BackendMs}ms | " +
                    $"data {run.GetPdfDataMs}ms | map {run.MappingMs}ms | audit {run.AuditMs}ms | " +
                    $"QuestPDF {run.QuestPdfMs}ms | {run.PdfSizeKb}KB | pages {run.PageCount}");
            }

            var summary = VariantSummary.From(variant, runs);
            allResults.Add(summary);
            _output.WriteLine(summary.ToReportLine());
            _output.WriteLine("");
        }

        _output.WriteLine("=== COMPARATIF (moyenne QuestPDF / backend / HTTP) ===");
        var baseline = allResults.FirstOrDefault(r => r.Variant == DpmPdfExperimentVariant.Baseline);
        foreach (var r in allResults)
        {
            _output.WriteLine(r.ToComparisonLine(baseline));
        }

        WriteConclusions(allResults, baseline);
    }

    private static WebApplicationFactory<Program> CreateFactory(DpmPdfExperimentVariant variant)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDemandePaiementDocumentRenderer>();
                services.AddSingleton<IDemandePaiementDocumentRenderer>(
                    _ => new QuestPdfDemandePaiementExperimentRenderer(variant));

                services.AddLogging(logging =>
                {
                    logging.AddProvider(PerfLogCaptureProvider.Instance);
                });
            });
        });

    private static async Task<HttpClient?> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var authRepo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var utilisateur = await authRepo.FindByIdAsync(4);
        if (utilisateur is null)
            return null;

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

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {body}");
        }

        var pdf = await response.Content.ReadAsByteArrayAsync();
        var perf = ParsePerfLog(PerfLogCaptureProvider.Instance.RecentLines.LastOrDefault());

        return new RunMeasurement(
            HttpMs: sw.ElapsedMilliseconds,
            BackendMs: perf.TotalMs,
            GetPdfDataMs: perf.GetPdfDataMs,
            MappingMs: perf.MappingMs,
            AuditMs: perf.AuditMs,
            QuestPdfMs: perf.QuestPdfMs,
            PdfSizeKb: pdf.Length / 1024,
            PageCount: CountPdfPages(pdf));
    }

    private static PerfSnapshot ParsePerfLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return default;

        static long Extract(string text, string key)
        {
            var m = Regex.Match(text, $@"{Regex.Escape(key)}=(\d+)ms");
            return m.Success ? long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }

        return new PerfSnapshot(
            GetPdfDataMs: Extract(line, "GetDemandePaiementPdfData"),
            MappingMs: Extract(line, "Mapping"),
            AuditMs: Extract(line, "Audit"),
            QuestPdfMs: Extract(line, "QuestPDF"),
            TotalMs: Extract(line, "Total"));
    }

    private static int CountPdfPages(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        return Regex.Matches(raw, @"/Type\s*/Page\b").Count;
    }

    private void WriteConclusions(IReadOnlyList<VariantSummary> results, VariantSummary? baseline)
    {
        if (baseline is null)
            return;

        _output.WriteLine("");
        _output.WriteLine("=== CONCLUSIONS EXPÉRIMENTALES ===");

        foreach (var experiment in new[]
                 {
                     (Label: "A — images optimisées", Variant: DpmPdfExperimentVariant.OptimizedImages),
                     (Label: "B — pagination simplifiée", Variant: DpmPdfExperimentVariant.SimplifiedPagination),
                     (Label: "A+B — combiné", Variant: DpmPdfExperimentVariant.OptimizedImagesAndPagination),
                 })
        {
            var r = results.FirstOrDefault(x => x.Variant == experiment.Variant);
            if (r is null)
                continue;

            var gainQuest = baseline.AvgQuestPdfMs - r.AvgQuestPdfMs;
            var gainPct = baseline.AvgQuestPdfMs > 0 ? gainQuest * 100.0 / baseline.AvgQuestPdfMs : 0;
            var confirmed = gainQuest >= 50; // seuil diagnostic : ≥50 ms moyenne QuestPDF
            _output.WriteLine(
                $"{experiment.Label}: QuestPDF {r.AvgQuestPdfMs:F0}ms ({gainQuest:+0;-0}ms, {gainPct:+0.0;-0.0}%) — " +
                $"{(confirmed ? "CONFIRMÉE" : "NON CONFIRMÉE")} | pages={r.AvgPageCount:F1} size={r.AvgPdfSizeKb:F0}KB");
        }
    }

    private sealed record RunMeasurement(
        long HttpMs,
        long BackendMs,
        long GetPdfDataMs,
        long MappingMs,
        long AuditMs,
        long QuestPdfMs,
        int PdfSizeKb,
        int PageCount);

    private readonly struct PerfSnapshot(
        long GetPdfDataMs,
        long MappingMs,
        long AuditMs,
        long QuestPdfMs,
        long TotalMs)
    {
        public long GetPdfDataMs { get; } = GetPdfDataMs;
        public long MappingMs { get; } = MappingMs;
        public long AuditMs { get; } = AuditMs;
        public long QuestPdfMs { get; } = QuestPdfMs;
        public long TotalMs { get; } = TotalMs;
    }

    private sealed class VariantSummary
    {
        public DpmPdfExperimentVariant Variant { get; init; }
        public double AvgHttpMs { get; init; }
        public double AvgBackendMs { get; init; }
        public double AvgGetPdfDataMs { get; init; }
        public double AvgMappingMs { get; init; }
        public double AvgAuditMs { get; init; }
        public double AvgQuestPdfMs { get; init; }
        public double AvgPdfSizeKb { get; init; }
        public double AvgPageCount { get; init; }

        public static VariantSummary From(DpmPdfExperimentVariant variant, IReadOnlyList<RunMeasurement> runs)
            => new()
            {
                Variant = variant,
                AvgHttpMs = runs.Average(r => r.HttpMs),
                AvgBackendMs = runs.Average(r => r.BackendMs),
                AvgGetPdfDataMs = runs.Average(r => r.GetPdfDataMs),
                AvgMappingMs = runs.Average(r => r.MappingMs),
                AvgAuditMs = runs.Average(r => r.AuditMs),
                AvgQuestPdfMs = runs.Average(r => r.QuestPdfMs),
                AvgPdfSizeKb = runs.Average(r => r.PdfSizeKb),
                AvgPageCount = runs.Average(r => r.PageCount),
            };

        public string ToReportLine()
            => $"[{Variant}] avg HTTP={AvgHttpMs:F0}ms backend={AvgBackendMs:F0}ms data={AvgGetPdfDataMs:F0}ms " +
               $"map={AvgMappingMs:F0}ms audit={AvgAuditMs:F0}ms QuestPDF={AvgQuestPdfMs:F0}ms " +
               $"size={AvgPdfSizeKb:F0}KB pages={AvgPageCount:F1}";

        public string ToComparisonLine(VariantSummary? baseline)
        {
            if (baseline is null || Variant == DpmPdfExperimentVariant.Baseline)
                return ToReportLine() + " (référence)";

            var dQuest = baseline.AvgQuestPdfMs - AvgQuestPdfMs;
            var dHttp = baseline.AvgHttpMs - AvgHttpMs;
            var pct = baseline.AvgQuestPdfMs > 0 ? dQuest * 100 / baseline.AvgQuestPdfMs : 0;
            return ToReportLine() + $" | ΔQuestPDF {dQuest:+0;-0}ms ({pct:+0.0;-0.0}%) | ΔHTTP {dHttp:+0;-0}ms";
        }
    }

    private sealed class PerfLogCaptureProvider : ILoggerProvider
    {
        public static PerfLogCaptureProvider Instance { get; } = new();

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
                if (!message.Contains("[PERF][DPM-PDF]", StringComparison.Ordinal))
                    return;

                owner.Lines.Add(message);
                owner.RecentLines.Add(message);
            }
        }
    }
}
