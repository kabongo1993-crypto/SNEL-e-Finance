using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BudgetWeb.Application.Diagnostics;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Entities;
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

/// <summary>Mesures réelles mutations DPM Vague 1 — logs [PERF][MUTATION].</summary>
public class MutationPerfMeasurementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public MutationPerfMeasurementTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.AddProvider(MutationPerfLogCaptureProvider.Instance));
            });
        });
        _output = output;
    }

    [Fact]
    public async Task Mesures_Mutations_Vague1_TroisRuns()
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

        MutationPerfLogCaptureProvider.Instance.Clear();

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

        var demandeur = await db.Demandeurs.AsNoTracking()
            .Where(d => d.Actif)
            .OrderBy(d => d.IdDemandeur)
            .FirstOrDefaultAsync();
        var casDossier = await db.CasDossiers.AsNoTracking()
            .Where(c => c.Actif)
            .OrderBy(c => c.IdCasDossier)
            .FirstOrDefaultAsync();
        var exercice = await db.ExercicesBudgetaires.AsNoTracking()
            .OrderByDescending(e => e.Annee)
            .FirstOrDefaultAsync();

        if (demandeur is null || casDossier is null || exercice is null)
        {
            _output.WriteLine("Référentiels manquants (demandeur/cas/exercice) — mesures ignorées.");
            return;
        }

        _output.WriteLine(
            $"Contexte test — Demandeur #{demandeur.IdDemandeur} | Cas #{casDossier.IdCasDossier} | Exercice #{exercice.IdExercice} ({exercice.Annee})");

        var updatePayload = JsonSerializer.Serialize(new
        {
            dateEmission = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            lieuEmission = "Lubumbashi",
            objet = "PERF-TEST modifié",
            compteSection = "S01",
            montantBrut = 1_750m,
            devise = "USD",
            typeBudgetSollicite = TypeBudgetCode.DepensesCourantes,
            modePaiementSollicite = ModePaiementDpm.Caisse,
        }, JsonOptions);

        var physiquePayload = JsonSerializer.Serialize(new
        {
            nomSignataire = "Test Signataire PERF",
            fonctionSignataire = "Chef service",
            dateSignature = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            commentaire = (string?)null,
        }, JsonOptions);

        // --- 1. CREATE x3 ---
        long? updateTargetId = null;
        for (var run = 1; run <= 3; run++)
        {
            var payload = BuildCreatePayload(exercice, demandeur, casDossier, run);
            var id = await MeasurePostAsync(
                client,
                "/api/v1/demandes-paiement",
                payload,
                MutationPerfOperations.Create,
                $"CREATE run {run}");
            if (run == 1 && id.HasValue)
                updateTargetId = id;
        }

        if (updateTargetId is null)
        {
            _output.WriteLine("CREATE a échoué — suite ignorée.");
            DumpAllLogs();
            return;
        }

        // --- 2. UPDATE x3 (same DPM) ---
        for (var run = 1; run <= 3; run++)
        {
            await MeasurePutAsync(
                client,
                $"/api/v1/demandes-paiement/{updateTargetId.Value}",
                updatePayload,
                MutationPerfOperations.Update,
                $"UPDATE run {run} (#{updateTargetId.Value})");
        }

        // --- 3. ENVOYER VALIDATION x3 (3 new brouillons) ---
        for (var run = 1; run <= 3; run++)
        {
            var id = await CreateBrouillonForScenarioAsync(client, run, run);
            if (id is null) continue;
            await MeasurePostAsync(
                client,
                $"/api/v1/demandes-paiement/{id.Value}/envoyer-validation",
                "{}",
                MutationPerfOperations.EnvoyerValidation,
                $"ENVOYER_VALIDATION run {run} (#{id.Value})");
        }

        // --- 4. VALIDATION PHYSIQUE N1 x3 ---
        for (var run = 1; run <= 3; run++)
        {
            var id = await CreateBrouillonForScenarioAsync(client, run + 10, run + 10);
            if (id is null) continue;
            await MeasurePostAsync(client, $"/api/v1/demandes-paiement/{id.Value}/envoyer-validation", "{}", null, "setup envoyer");
            await MeasurePostAsync(
                client,
                $"/api/v1/demandes-paiement/{id.Value}/validation/n1/physique",
                physiquePayload,
                MutationPerfOperations.ValidationPhysiqueN1,
                $"VALIDATION_PHYSIQUE_N1 run {run} (#{id.Value})");
        }

        // --- 5. VALIDATION PHYSIQUE N2 x3 ---
        for (var run = 1; run <= 3; run++)
        {
            var id = await CreateBrouillonForScenarioAsync(client, run + 20, run + 20);
            if (id is null) continue;
            await MeasurePostAsync(client, $"/api/v1/demandes-paiement/{id.Value}/envoyer-validation", "{}", null, "setup envoyer");
            await MeasurePostAsync(client, $"/api/v1/demandes-paiement/{id.Value}/validation/n1/physique", physiquePayload, null, "setup n1");
            await UploadDocumentSigneAsync(client, id.Value);
            await MeasurePostAsync(
                client,
                $"/api/v1/demandes-paiement/{id.Value}/validation/n2/physique",
                physiquePayload,
                MutationPerfOperations.ValidationPhysiqueN2,
                $"VALIDATION_PHYSIQUE_N2 run {run} (#{id.Value})");
        }

        // --- 6. SOUMETTRE x3 ---
        for (var run = 1; run <= 3; run++)
        {
            var id = await PrepareDemandeSoumettreAsync(client, physiquePayload, run + 30);
            if (id is null) continue;

            await MeasurePostAsync(
                client,
                $"/api/v1/demandes-paiement/{id.Value}/soumettre",
                "{}",
                MutationPerfOperations.Soumettre,
                $"SOUMETTRE run {run} (#{id.Value})",
                measureRefresh: true);
            await VerifyStatutSoumiseAsync(client, id.Value, run);
        }

        _output.WriteLine("");
        _output.WriteLine("=== SYNTHÈSE PAR OPÉRATION ===");
        PrintSummary(MutationPerfOperations.Create);
        PrintSummary(MutationPerfOperations.Update);
        PrintSummary(MutationPerfOperations.EnvoyerValidation);
        PrintSummary(MutationPerfOperations.ValidationPhysiqueN1);
        PrintSummary(MutationPerfOperations.ValidationPhysiqueN2);
        PrintSummary(MutationPerfOperations.Soumettre);

        DumpAllLogs();

        // --- 7. Régime chaud — 5 ENVOYER consécutifs (GetDetailAsync) ---
        _output.WriteLine("");
        _output.WriteLine("=== RÉGIME CHAUD — 5× ENVOYER_VALIDATION consécutifs ===");
        for (var run = 1; run <= 5; run++)
        {
            var id = await CreateBrouillonForScenarioAsync(client, run + 100, run + 100);
            if (id is null) continue;
            await MeasurePostAsync(
                client,
                $"/api/v1/demandes-paiement/{id.Value}/envoyer-validation",
                "{}",
                MutationPerfOperations.EnvoyerValidation,
                $"ENVOYER_VALIDATION warm run {run} (#{id.Value})");
        }

        // --- 8. GetDetailAsync direct — DPM légère (#126) et chargée (#43, #50) ---
        _output.WriteLine("");
        _output.WriteLine("=== GetDetailAsync DIRECT (repository) ===");
        await MeasureDirectGetDetailAsync(db, new[] { 126L, 43L, 50L });

        // --- 9. Vérification fonctionnelle GET complet ---
        _output.WriteLine("");
        _output.WriteLine("=== VÉRIFICATION FONCTIONNELLE GET complet ===");
        await VerifyGetCompletAsync(client, 43L);
        await VerifyGetCompletAsync(client, 50L);
    }

    private static string BuildCreatePayload(
        ExerciceBudgetaire exercice,
        Demandeur demandeur,
        CasDossier casDossier,
        int seed)
        => JsonSerializer.Serialize(new
        {
            dateEmission = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            lieuEmission = "Kinshasa",
            idExercice = exercice.IdExercice,
            idDemandeur = demandeur.IdDemandeur,
            idUB = demandeur.FK_UniteBudgetaire,
            idCasDossier = casDossier.IdCasDossier,
            objet = $"PERF-S{seed}-{Guid.NewGuid():N}",
            montantBrut = 1_500m,
            devise = "USD",
            typeBudgetSollicite = TypeBudgetCode.DepensesCourantes,
            modePaiementSollicite = ModePaiementDpm.Caisse,
        }, JsonOptions);

    private async Task<long?> CreateBrouillonForScenarioAsync(HttpClient client, int seed, int _)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var demandeur = await db.Demandeurs.AsNoTracking().Where(d => d.Actif).FirstAsync();
        var casDossier = await db.CasDossiers.AsNoTracking().Where(c => c.Actif).FirstAsync();
        var exercice = await db.ExercicesBudgetaires.AsNoTracking().OrderByDescending(e => e.Annee).FirstAsync();
        var payload = BuildCreatePayload(exercice, demandeur, casDossier, seed);
        return await MeasurePostAsync(client, "/api/v1/demandes-paiement", payload, null, $"setup create #{seed}");
    }

    private async Task<long?> PrepareDemandeSoumettreAsync(
        HttpClient client,
        string physiquePayload,
        int seed)
    {
        var id = await CreateBrouillonForScenarioAsync(client, seed, seed);
        if (id is null) return null;

        await AddAllRequiredPiecesAsync(client, id.Value);
        await MeasurePostAsync(client, $"/api/v1/demandes-paiement/{id.Value}/envoyer-validation", "{}", null, "setup envoyer");
        await MeasurePostAsync(client, $"/api/v1/demandes-paiement/{id.Value}/validation/n1/physique", physiquePayload, null, "setup n1 phys");
        await UploadDocumentSigneAsync(client, id.Value);
        await MeasurePostAsync(client, $"/api/v1/demandes-paiement/{id.Value}/validation/n2/physique", physiquePayload, null, "setup n2 phys");
        return id;
    }

    private async Task AddAllRequiredPiecesAsync(HttpClient client, long idDemande)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        var demande = await db.DemandesPaiement.AsNoTracking()
            .FirstAsync(d => d.IdDemandePaiement == idDemande);
        var piecesOblig = await db.CasDossierPiecesObligatoires.AsNoTracking()
            .Where(p => p.FK_CasDossier == demande.FK_CasDossier && p.Actif)
            .OrderBy(p => p.Ordre)
            .ToListAsync();

        _output.WriteLine(
            $"  Setup pièces obligatoires — cas #{demande.FK_CasDossier} | count={piecesOblig.Count}");

        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        for (var i = 0; i < piecesOblig.Count; i++)
        {
            var pieceOblig = piecesOblig[i];
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(pieceOblig.CodeTypePiece), "codeTypePiece");
            content.Add(new StringContent(pieceOblig.Libelle), "libelle");
            content.Add(new StringContent(pieceOblig.IdPieceObligatoire.ToString()), "idPieceObligatoire");
            content.Add(
                new ByteArrayContent(bytes),
                "fichier",
                $"perf-{pieceOblig.CodeTypePiece}-{i + 1}.pdf");
            var response = await client.PostAsync($"/api/v1/demandes-paiement/{idDemande}/pieces", content);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _output.WriteLine(
                    $"  WARN pièce « {pieceOblig.Libelle} » — HTTP {(int)response.StatusCode}: " +
                    $"{err[..Math.Min(err.Length, 200)]}");
            }
            else
            {
                _output.WriteLine($"  OK pièce « {pieceOblig.Libelle} » ({pieceOblig.CodeTypePiece})");
            }
        }
    }

    private async Task VerifyStatutSoumiseAsync(HttpClient client, long idDemande, int run)
    {
        var response = await client.GetAsync($"/api/v1/demandes-paiement/{idDemande}");
        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"  VERIFY SOUMETTRE run {run} — GET #{idDemande} HTTP {(int)response.StatusCode}");
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var statut = ExtractStatutFromGetResponse(doc.RootElement);
        var ok = string.Equals(statut, StatutDemandePaiement.Soumise, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine(
            $"  VERIFY SOUMETTRE run {run} — statut={statut ?? "?"} | attendu={StatutDemandePaiement.Soumise} | {(ok ? "OK" : "ECHEC")}");
    }

    private static string? ExtractStatutFromGetResponse(JsonElement root)
    {
        if (root.TryGetProperty("statut", out var direct))
            return direct.GetString();
        if (root.TryGetProperty("demande", out var demande)
            && demande.TryGetProperty("statut", out var nested))
            return nested.GetString();
        return null;
    }

    private async Task UploadDocumentSigneAsync(HttpClient client, long idDemande)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(TypePieceJointeDpm.DocumentDpmSigne), "codeTypePiece");
        content.Add(new StringContent("Document DPM signé"), "libelle");
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35 };
        content.Add(new ByteArrayContent(bytes), "fichier", "dpm-signe-perf.pdf");
        var response = await client.PostAsync($"/api/v1/demandes-paiement/{idDemande}/pieces", content);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"  WARN document signé — HTTP {(int)response.StatusCode}: {err[..Math.Min(err.Length, 200)]}");
        }
    }

    private async Task<long?> MeasurePostAsync(
        HttpClient client,
        string url,
        string jsonBody,
        string? expectedOperation,
        string label,
        bool measureRefresh = false)
    {
        MutationPerfLogCaptureProvider.Instance.ClearRecent();

        var sw = Stopwatch.StartNew();
        var response = await client.PostAsync(url, new StringContent(jsonBody, Encoding.UTF8, "application/json"));
        sw.Stop();

        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{label} — HTTP POST client {sw.ElapsedMilliseconds} ms | status {(int)response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"  ERREUR: {body[..Math.Min(body.Length, 500)]}");
            return null;
        }

        LogRecentPerf(expectedOperation);

        long? id = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("idDemandePaiement", out var idProp))
                id = idProp.GetInt64();
        }
        catch { /* ignore */ }

        if (measureRefresh)
        {
            MutationPerfLogCaptureProvider.Instance.ClearRecent();
            var swList = Stopwatch.StartNew();
            var listResp = await client.GetAsync("/api/v1/demandes-paiement?scope=mes-demandes");
            swList.Stop();
            var swCounts = Stopwatch.StartNew();
            var countsResp = await client.GetAsync("/api/v1/demandes-paiement/compteurs?scope=mes-demandes");
            swCounts.Stop();
            _output.WriteLine(
                $"  REFRESH post-soumission — GET list {swList.ElapsedMilliseconds} ms | GET compteurs {swCounts.ElapsedMilliseconds} ms | total refresh {swList.ElapsedMilliseconds + swCounts.ElapsedMilliseconds} ms");
        }

        return id;
    }

    private async Task MeasurePutAsync(
        HttpClient client,
        string url,
        string jsonBody,
        string? expectedOperation,
        string label)
    {
        MutationPerfLogCaptureProvider.Instance.ClearRecent();

        var sw = Stopwatch.StartNew();
        var response = await client.PutAsync(url, new StringContent(jsonBody, Encoding.UTF8, "application/json"));
        sw.Stop();

        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{label} — HTTP PUT client {sw.ElapsedMilliseconds} ms | status {(int)response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"  ERREUR: {body[..Math.Min(body.Length, 500)]}");
            return;
        }

        LogRecentPerf(expectedOperation);
    }

    private void LogRecentPerf(string? expectedOperation)
    {
        foreach (var line in MutationPerfLogCaptureProvider.Instance.RecentLines)
            _output.WriteLine("  " + line);

        var sqlSummary = MutationPerfLogCaptureProvider.Instance.RecentLines
            .FirstOrDefault(l => l.Contains("GetDetailAsync.SqlCount=", StringComparison.Ordinal));
        if (sqlSummary is not null)
        {
            _output.WriteLine("  --- Détail SQL GetDetailAsync ---");
            foreach (var line in MutationPerfLogCaptureProvider.Instance.RecentLines
                         .Where(l => l.Contains("GetDetailAsync.Sql", StringComparison.Ordinal)))
                _output.WriteLine("  " + line);
        }

        if (expectedOperation is not null)
        {
            var totalLine = MutationPerfLogCaptureProvider.Instance.RecentLines
                .LastOrDefault(l => l.Contains($"Operation={expectedOperation}", StringComparison.Ordinal)
                                    && l.Contains("TOTAL=", StringComparison.Ordinal));
            if (totalLine is not null)
                MutationPerfLogCaptureProvider.Instance.RecordMeasurement(expectedOperation, totalLine);

            var getDetailLine = MutationPerfLogCaptureProvider.Instance.RecentLines
                .LastOrDefault(l => l.Contains($"Operation={expectedOperation}", StringComparison.Ordinal)
                                    && l.Contains("Repo.GetDetailAsync", StringComparison.Ordinal));
            if (getDetailLine is not null)
                MutationPerfLogCaptureProvider.Instance.RecordGetDetailMeasurement(expectedOperation, getDetailLine);
        }
    }

    private async Task MeasureDirectGetDetailAsync(BudgetDbContext _, IReadOnlyList<long> ids)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            var exists = await _factory.Services.CreateAsyncScope().ServiceProvider
                .GetRequiredService<BudgetDbContext>()
                .DemandesPaiement.AsNoTracking()
                .AnyAsync(d => d.IdDemandePaiement == id);
            if (!exists)
            {
                _output.WriteLine($"  DPM #{id} introuvable — skip");
                continue;
            }

            MutationPerfLogCaptureProvider.Instance.ClearRecent();
            await using var scope = _factory.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDemandePaiementRepository>();
            var sw = Stopwatch.StartNew();
            var entity = await repo.GetDetailAsync(id);
            sw.Stop();

            _output.WriteLine(
                $"  GetDetailAsync direct DPM #{id} run {i + 1} — total {sw.ElapsedMilliseconds} ms | " +
                $"benef={entity?.Beneficiaires.Count ?? 0} imput={entity?.Imputations.Count ?? 0} " +
                $"pieces={entity?.PiecesJointes.Count ?? 0} val={entity?.ValidationsEntite.Count ?? 0}");

            foreach (var line in MutationPerfLogCaptureProvider.Instance.RecentLines
                         .Where(l => l.Contains("GetDetailAsync.Sql", StringComparison.Ordinal)))
                _output.WriteLine("    " + line);
        }
    }

    private async Task VerifyGetCompletAsync(HttpClient client, long idDemande)
    {
        var response = await client.GetAsync($"/api/v1/demandes-paiement/{idDemande}/complet");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _output.WriteLine($"  GET complet #{idDemande} — introuvable");
            return;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var hasBenef = root.TryGetProperty("beneficiaires", out var benef) && benef.GetArrayLength() >= 0;
        var hasPieces = root.TryGetProperty("piecesJointes", out var pieces);
        var hasVal = root.TryGetProperty("validationsEntite", out var val);
        var hasImput = root.TryGetProperty("imputations", out var imput);
        _output.WriteLine(
            $"  GET complet #{idDemande} OK — benef={hasBenef} pieces={hasPieces} val={hasVal} imput={hasImput} " +
            $"(pieces count={(hasPieces ? pieces.GetArrayLength() : 0)})");
    }

    private void PrintSummary(string operation)
    {
        var entries = MutationPerfLogCaptureProvider.Instance.GetMeasurements(operation);
        if (entries.Count == 0)
        {
            _output.WriteLine($"{operation}: aucune mesure TOTAL capturée.");
            return;
        }

        var totals = entries.Select(ParseTotalMs).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (totals.Count == 0)
        {
            _output.WriteLine($"{operation}: logs présents mais TOTAL non parsé.");
            return;
        }

        _output.WriteLine(
            $"{operation}: runs={totals.Count} | min={totals.Min()}ms | avg={(int)totals.Average()}ms | max={totals.Max()}ms");
    }

    private static long? ParseTotalMs(string logLine)
    {
        var idx = logLine.IndexOf("TOTAL=", StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + 6;
        var end = logLine.IndexOf("ms", start, StringComparison.Ordinal);
        if (end < 0) return null;
        return long.TryParse(logLine[start..end], out var ms) ? ms : null;
    }

    private void DumpAllLogs()
    {
        _output.WriteLine("");
        _output.WriteLine("=== TOUS LES LOGS [PERF][MUTATION] ===");
        foreach (var line in MutationPerfLogCaptureProvider.Instance.Lines)
            _output.WriteLine(line);
    }

    private sealed class MutationPerfLogCaptureProvider : ILoggerProvider
    {
        public static MutationPerfLogCaptureProvider Instance { get; } = new();

        public List<string> Lines { get; } = new();
        public List<string> RecentLines { get; } = new();
        private readonly Dictionary<string, List<string>> _totalsByOperation = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _getDetailByOperation = new(StringComparer.Ordinal);

        public void Clear()
        {
            Lines.Clear();
            RecentLines.Clear();
            _totalsByOperation.Clear();
            _getDetailByOperation.Clear();
        }

        public void ClearRecent() => RecentLines.Clear();

        public void RecordMeasurement(string operation, string totalLine)
        {
            if (!_totalsByOperation.TryGetValue(operation, out var list))
            {
                list = new List<string>();
                _totalsByOperation[operation] = list;
            }
            list.Add(totalLine);
        }

        public IReadOnlyList<string> GetMeasurements(string operation)
            => _totalsByOperation.TryGetValue(operation, out var list) ? list : Array.Empty<string>();

        public void RecordGetDetailMeasurement(string operation, string line)
        {
            if (!_getDetailByOperation.TryGetValue(operation, out var list))
            {
                list = new List<string>();
                _getDetailByOperation[operation] = list;
            }
            list.Add(line);
        }

        public IReadOnlyList<string> GetGetDetailMeasurements(string operation)
            => _getDetailByOperation.TryGetValue(operation, out var list) ? list : Array.Empty<string>();

        public ILogger CreateLogger(string categoryName) => new PerfLogger(this);

        public void Dispose() { }

        private sealed class PerfLogger(MutationPerfLogCaptureProvider owner) : ILogger
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
                if (!message.Contains("[PERF][MUTATION]", StringComparison.Ordinal))
                    return;

                owner.Lines.Add(message);
                owner.RecentLines.Add(message);
            }
        }
    }
}
