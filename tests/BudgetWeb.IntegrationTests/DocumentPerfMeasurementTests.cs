using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BudgetWeb.IntegrationTests;

/// <summary>Mesures réelles Lot perf documents — lit les logs [PERF] côté serveur.</summary>
public class DocumentPerfMeasurementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public DocumentPerfMeasurementTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging =>
                {
                    logging.AddProvider(PerfLogCaptureProvider.Instance);
                });
            });
        });
        _output = output;
    }

    [Fact]
    public async Task Mesures_Documents_A_H()
    {
        PerfLogCaptureProvider.Instance.Clear();

        await using var scope = _factory.Services.CreateAsyncScope();
        var authRepo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

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

        const long idDemande = 50;

        // Warm-up consultation
        var detailResp = await client.GetAsync($"/api/v1/demandes-paiement/{idDemande}");
        if (detailResp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _output.WriteLine($"Demande #{idDemande} introuvable — mesures ignorées.");
            return;
        }

        detailResp.EnsureSuccessStatusCode();

        // A — DPM PDF
        await MeasureGet(client, $"/api/v1/demandes-paiement/{idDemande}/document-pdf?inline=true", "Test A DPM PDF");

        // B — Billet conversion
        await MeasureGet(client, $"/api/v1/demandes-paiement/{idDemande}/billet-conversion/pdf?inline=true", "Test B Billet");

        // C — Pièce caisse
        await MeasureGet(client, $"/api/v1/demandes-paiement/{idDemande}/piece-caisse/pdf?inline=true", "Test C Pièce caisse");

        // D — Bon provisoire
        await MeasureGet(client, $"/api/v1/demandes-paiement/{idDemande}/bon-provisoire/pdf?inline=true", "Test D Bon provisoire");

        // E — Minute chèque
        await MeasureGet(client, $"/api/v1/demandes-paiement/{idDemande}/minute-cheque/pdf?inline=true", "Test E Minute chèque");

        // F — Upload 2 Mo
        var pieceSmall = await UploadPiece(client, idDemande, 2 * 1024 * 1024, "perf-upload-2mo.pdf", "Test F Upload 2Mo");

        // G — Upload 9 Mo
        var pieceLarge = await UploadPiece(client, idDemande, 9 * 1024 * 1024, "perf-upload-9mo.pdf", "Test G Upload 9Mo");

        // H — Aperçu pièces importées
        if (pieceSmall is not null)
            await MeasureGet(client, $"/api/v1/demandes-paiement/{idDemande}/pieces/{pieceSmall}/apercu", "Test H Aperçu 2Mo");
        if (pieceLarge is not null)
            await MeasureGet(client, $"/api/v1/demandes-paiement/{idDemande}/pieces/{pieceLarge}/apercu", "Test H Aperçu 9Mo");

        _output.WriteLine("");
        _output.WriteLine("=== LOGS [PERF] CAPTURÉS ===");
        foreach (var line in PerfLogCaptureProvider.Instance.Lines)
            _output.WriteLine(line);
    }

    private async Task MeasureGet(HttpClient client, string url, string label)
    {
        PerfLogCaptureProvider.Instance.ClearRecent();
        var sw = Stopwatch.StartNew();
        var response = await client.GetAsync(url);
        sw.Stop();
        var size = response.Content.Headers.ContentLength ?? 0;
        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"{label} — HTTP {(int)response.StatusCode} — {sw.ElapsedMilliseconds} ms client");
            _output.WriteLine(await response.Content.ReadAsStringAsync());
            return;
        }

        await response.Content.ReadAsByteArrayAsync();
        _output.WriteLine($"{label} — client total {sw.ElapsedMilliseconds} ms — payload {size / 1024} KB");
        foreach (var line in PerfLogCaptureProvider.Instance.RecentLines)
            _output.WriteLine("  " + line);
    }

    private async Task<long?> UploadPiece(HttpClient client, long idDemande, int size, string fileName, string label)
    {
        PerfLogCaptureProvider.Instance.ClearRecent();
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        bytes[0] = (byte)'%';
        bytes[1] = (byte)'P';
        bytes[2] = (byte)'D';
        bytes[3] = (byte)'F';

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "fichier", fileName);
        form.Add(new StringContent("AUTRE"), "codeTypePiece");
        form.Add(new StringContent($"Perf bench {fileName}"), "libelle");

        var sw = Stopwatch.StartNew();
        var response = await client.PostAsync($"/api/v1/demandes-paiement/{idDemande}/pieces", form);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"{label} — HTTP {(int)response.StatusCode} — {sw.ElapsedMilliseconds} ms client");
            _output.WriteLine(await response.Content.ReadAsStringAsync());
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var idPiece = doc.RootElement.GetProperty("idPieceJointe").GetInt64();
        _output.WriteLine($"{label} — client total {sw.ElapsedMilliseconds} ms — idPiece={idPiece}");
        foreach (var line in PerfLogCaptureProvider.Instance.RecentLines)
            _output.WriteLine("  " + line);
        return idPiece;
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
                if (!message.Contains("[PERF]", StringComparison.Ordinal))
                    return;

                owner.Lines.Add(message);
                owner.RecentLines.Add(message);
            }
        }
    }
}
