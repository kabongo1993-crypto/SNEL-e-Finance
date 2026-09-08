using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BudgetWeb.Application.Diagnostics;

/// <summary>Instrumentation temporaire — workflow Établir (documents instrument / billet).</summary>
public sealed class EtablissementPerfScope : IDisposable
{
    private static readonly AsyncLocal<EtablissementPerfScope?> CurrentScope = new();

    public static EtablissementPerfScope? Current => CurrentScope.Value;

    private readonly ILogger _logger;
    private readonly string _tag;
    private readonly long _demandeId;
    private readonly Stopwatch _total = Stopwatch.StartNew();

    public long PermissionMs { get; set; }
    public long GetTrackedMs { get; set; }
    public long AccessMs { get; set; }
    public long StatutCoherenceMs { get; set; }
    public long BilletConversionLoadMs { get; set; }
    public long CheckExistingMs { get; set; }
    public long GetDetailExistingMs { get; set; }
    public long ValidationMetierMs { get; set; }
    public long ParametresMs { get; set; }
    public long TauxConversionMs { get; set; }
    public long GenererNumeroMs { get; set; }
    public long SaveInstrumentMs { get; set; }
    public long AuditMs { get; set; }
    public long GetDetailReloadMs { get; set; }
    public long MappingMs { get; set; }
    public long PdfGenerationMs { get; set; }
    public long AutresMs { get; set; }
    public int SqlOperations { get; set; }

    private EtablissementPerfScope(ILogger logger, string tag, long demandeId)
    {
        _logger = logger;
        _tag = tag;
        _demandeId = demandeId;
        CurrentScope.Value = this;
    }

    public static EtablissementPerfScope Begin(ILogger logger, string tag, long demandeId)
        => new(logger, tag, demandeId);

    public async Task<T> TrackAsync<T>(string etape, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            var ms = sw.ElapsedMilliseconds;
            SqlOperations++;
            _logger.LogInformation(
                "[PERF][ETABLISSEMENT] DemandeId={DemandeId} Tag={Tag} Etape={Etape} Duration={DurationMs}ms",
                _demandeId, _tag, etape, ms);
        }
    }

    public async Task TrackAsync(string etape, Func<Task> action)
    {
        await TrackAsync(etape, async () =>
        {
            await action();
            return true;
        });
    }

    public void LogTotal()
    {
        _logger.LogInformation(
            "[PERF][ETABLISSEMENT] DemandeId={DemandeId} Tag={Tag} " +
            "Permission={PermissionMs}ms GetTracked={GetTrackedMs}ms Access={AccessMs}ms " +
            "StatutCoherence={StatutCoherenceMs}ms BilletLoad={BilletConversionLoadMs}ms " +
            "CheckExisting={CheckExistingMs}ms GetDetailExisting={GetDetailExistingMs}ms " +
            "ValidationMetier={ValidationMetierMs}ms Parametres={ParametresMs}ms " +
            "TauxConversion={TauxConversionMs}ms GenererNumero={GenererNumeroMs}ms " +
            "SaveInstrument={SaveInstrumentMs}ms Audit={AuditMs}ms " +
            "GetDetailReload={GetDetailReloadMs}ms Mapping={MappingMs}ms " +
            "PdfGeneration={PdfGenerationMs}ms Autres={AutresMs}ms SqlOps={SqlOperations} " +
            "TOTAL={TotalMs}ms",
            _demandeId, _tag,
            PermissionMs, GetTrackedMs, AccessMs,
            StatutCoherenceMs, BilletConversionLoadMs,
            CheckExistingMs, GetDetailExistingMs,
            ValidationMetierMs, ParametresMs,
            TauxConversionMs, GenererNumeroMs,
            SaveInstrumentMs, AuditMs,
            GetDetailReloadMs, MappingMs,
            PdfGenerationMs, AutresMs, SqlOperations,
            _total.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        if (ReferenceEquals(CurrentScope.Value, this))
            CurrentScope.Value = null;
    }
}
