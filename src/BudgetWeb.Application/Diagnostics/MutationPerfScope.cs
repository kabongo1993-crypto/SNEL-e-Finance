using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BudgetWeb.Application.Diagnostics;

/// <summary>Instrumentation temporaire — mutations DPM Vague 1 (mesures réelles).</summary>
public sealed class MutationPerfScope : IDisposable
{
    private static readonly AsyncLocal<MutationPerfScope?> CurrentScope = new();

    public static MutationPerfScope? Current => CurrentScope.Value;

    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly long? _demandeId;
    private readonly Stopwatch _total = Stopwatch.StartNew();
    private readonly Dictionary<string, long> _steps = new(StringComparer.Ordinal);
    private int _sqlOperations;

    private MutationPerfScope(ILogger logger, string operation, long? demandeId)
    {
        _logger = logger;
        _operation = operation;
        _demandeId = demandeId;
        CurrentScope.Value = this;
    }

    public static MutationPerfScope Begin(ILogger logger, string operation, long? demandeId = null)
        => new(logger, operation, demandeId);

    public void Record(string etape, long durationMs)
    {
        _steps[etape] = _steps.TryGetValue(etape, out var existing)
            ? existing + durationMs
            : durationMs;
        _sqlOperations++;
        _logger.LogInformation(
            "[PERF][MUTATION] Operation={Operation} DemandeId={DemandeId} Etape={Etape} Duration={DurationMs}ms",
            _operation,
            _demandeId,
            etape,
            durationMs);
    }

    public async Task<T> TrackAsync<T>(string etape, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            Record(etape, sw.ElapsedMilliseconds);
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

    public T Track<T>(string etape, Func<T> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return action();
        }
        finally
        {
            Record(etape, sw.ElapsedMilliseconds);
        }
    }

    public void LogTotal()
    {
        var stepsSummary = string.Join(
            " | ",
            _steps.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}ms"));

        _logger.LogInformation(
            "[PERF][MUTATION] Operation={Operation} DemandeId={DemandeId} TOTAL={TotalMs}ms SqlOps={SqlOps} Steps={Steps}",
            _operation,
            _demandeId,
            _total.ElapsedMilliseconds,
            _sqlOperations,
            stepsSummary);
    }

    public IReadOnlyDictionary<string, long> Steps => _steps;

    public long TotalMs => _total.ElapsedMilliseconds;

    public void Dispose()
    {
        if (ReferenceEquals(CurrentScope.Value, this))
            CurrentScope.Value = null;
    }
}

public static class MutationPerfOperations
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string EnvoyerValidation = "ENVOYER_VALIDATION";
    public const string ValidationPhysiqueN1 = "VALIDATION_PHYSIQUE_N1";
    public const string ValidationPhysiqueN2 = "VALIDATION_PHYSIQUE_N2";
    public const string Soumettre = "SOUMETTRE";
}
