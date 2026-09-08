using BudgetWeb.API.Models;
using BudgetWeb.Domain.DemandePaiement;
using Microsoft.AspNetCore.Mvc;

namespace BudgetWeb.API.Helpers;

internal static class DemandePaiementApiResults
{
    internal static async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Status(401, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return Status(404, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Status(400, ex.Message);
        }
        catch (DemandePaiementConcurrencyException ex)
        {
            return Status(StatusCodes.Status409Conflict, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Status(ResolveInvalidOperationStatus(ex.Message), ex.Message);
        }
    }

    internal static IActionResult Status(int statusCode, string message)
        => new ObjectResult(new ApiErrorResponse { Status = statusCode, Message = message })
        {
            StatusCode = statusCode,
        };

    private static int ResolveInvalidOperationStatus(string message)
    {
        if (message.Contains("Transition interdite", StringComparison.OrdinalIgnoreCase)
            || message.Contains("doit être", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ne peut pas être visée", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCodes.Status409Conflict;
        }

        return StatusCodes.Status400BadRequest;
    }
}
