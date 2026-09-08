using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BudgetWeb.Infrastructure.Diagnostics;

/// <summary>Enregistre la durée de chaque DbCommand lors d'un DetailQuerySqlPerfScope actif.</summary>
public sealed class DetailQuerySqlInterceptor : DbCommandInterceptor
{
    private readonly Stopwatch _sw = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        if (DetailQuerySqlPerfScope.Current is not null)
            _sw.Restart();
        return base.ReaderExecuting(command, eventData, result);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        if (DetailQuerySqlPerfScope.Current is not null)
        {
            _sw.Stop();
            DetailQuerySqlPerfScope.Current.RecordCommand(_sw.ElapsedMilliseconds);
        }

        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (DetailQuerySqlPerfScope.Current is not null)
        {
            _sw.Stop();
            DetailQuerySqlPerfScope.Current.RecordCommand(_sw.ElapsedMilliseconds);
        }

        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (DetailQuerySqlPerfScope.Current is not null)
            _sw.Restart();
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
