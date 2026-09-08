using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BudgetWeb.Infrastructure.Diagnostics;

/// <summary>Instrumentation temporaire — découpage SQL par requête lors de GetDetailAsync.</summary>
public sealed class DetailQuerySqlPerfScope : IDisposable
{
    private static readonly AsyncLocal<DetailQuerySqlPerfScope?> CurrentScope = new();

    public static DetailQuerySqlPerfScope? Current => CurrentScope.Value;

    private readonly ILogger? _logger;
    private readonly List<long> _commandMs = new();
    private readonly Stopwatch _total = Stopwatch.StartNew();

    private DetailQuerySqlPerfScope(ILogger? logger)
    {
        _logger = logger;
        CurrentScope.Value = this;
    }

    public static DetailQuerySqlPerfScope Begin(ILogger? logger = null) => new(logger);

    public void RecordCommand(long durationMs) => _commandMs.Add(durationMs);

    public void LogSummary(long getDetailAsyncTotalMs)
    {
        if (_logger is null)
            return;

        var sqlTotal = _commandMs.Sum();
        var efMat = Math.Max(0, getDetailAsyncTotalMs - sqlTotal);

        _logger.LogInformation(
            "[PERF][MUTATION] GetDetailAsync.SqlCount={SqlCount} SqlTotal={SqlTotalMs}ms EfMaterialization={EfMatMs}ms GetDetailAsyncTotal={TotalMs}ms",
            _commandMs.Count,
            sqlTotal,
            efMat,
            getDetailAsyncTotalMs);

        for (var i = 0; i < _commandMs.Count; i++)
        {
            _logger.LogInformation(
                "[PERF][MUTATION] GetDetailAsync.SqlQuery Index={Index} Duration={DurationMs}ms",
                i + 1,
                _commandMs[i]);
        }
    }

    public IReadOnlyList<long> CommandDurations => _commandMs;

    public void Dispose()
    {
        if (ReferenceEquals(CurrentScope.Value, this))
            CurrentScope.Value = null;
    }
}
