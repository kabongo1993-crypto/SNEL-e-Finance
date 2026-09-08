using BudgetWeb.Mcp.Client;
using BudgetWeb.Mcp.Security;

namespace BudgetWeb.Mcp.Tools;

public static class ToolRunner
{
    public static async Task<string> Run(
        BudgetWebApiClient api,
        string outil,
        object? parametres,
        Func<Task<string>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!api.HasBearer)
                return api.RequireAuthMessage();

            var result = await action();
            int? count = null;
            if (result.Contains("\"returned\"", StringComparison.Ordinal)
                || result.Contains("\"total\"", StringComparison.Ordinal))
            {
                // best-effort ; l'absence de compteur n'est pas bloquante
            }

            await api.JournaliserAsync(outil, parametres, count, cancellationToken);
            return result;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
        catch (HttpRequestException)
        {
            return "L’API Budget Web est indisponible. Réessayez plus tard.";
        }
        catch (TaskCanceledException)
        {
            return "Délai d’attente dépassé vers l’API Budget Web.";
        }
        catch (Exception)
        {
            return "Erreur interne Budget Web.";
        }
    }

    public static (int Skip, int Take) Page(int? skip, int? take)
        => (ParameterGuard.ClampSkip(skip), ParameterGuard.ClampTake(take));
}
