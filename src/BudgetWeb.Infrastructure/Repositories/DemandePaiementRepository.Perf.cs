using System.Diagnostics;
using BudgetWeb.Application.Diagnostics;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    private async Task<T> TrackRepoAsync<T>(string etape, Func<Task<T>> action)
    {
        var perf = MutationPerfScope.Current;
        if (perf is null)
            return await action();

        var sw = Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            perf.Record($"Repo.{etape}", sw.ElapsedMilliseconds);
        }
    }

    private async Task TrackRepoAsync(string etape, Func<Task> action)
    {
        await TrackRepoAsync(etape, async () =>
        {
            await action();
            return true;
        });
    }
}
