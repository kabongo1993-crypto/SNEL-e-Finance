using System.Net;
using System.Text.Json;
using BudgetWeb.API.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace BudgetWeb.API.Middleware;

public static class ExceptionHandlingExtensions
{
    public static void UseGlobalExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = exceptionFeature?.Error;

                var logger = context.RequestServices.GetService<ILoggerFactory>()
                    ?.CreateLogger("BudgetWeb.API.ExceptionHandler");
                if (exception is not null)
                {
                    logger?.LogError(exception, "Unhandled exception on {Method} {Path}",
                        context.Request.Method, context.Request.Path);
                }

                context.Response.ContentType = "application/json";

                var isDev = string.Equals(
                    context.RequestServices.GetService<IHostEnvironment>()?.EnvironmentName,
                    "Development",
                    StringComparison.OrdinalIgnoreCase);

                var (statusCode, message) = exception switch
                {
                    UnauthorizedAccessException => (HttpStatusCode.Unauthorized, exception!.Message),
                    KeyNotFoundException => (HttpStatusCode.NotFound, exception!.Message),
                    InvalidOperationException => (HttpStatusCode.BadRequest, exception!.Message),
                    _ => (HttpStatusCode.InternalServerError,
                        isDev && exception is not null
                            ? exception.GetBaseException().Message
                            : "Une erreur interne est survenue.")
                };

                context.Response.StatusCode = (int)statusCode;

                var response = new ApiErrorResponse
                {
                    Status = (int)statusCode,
                    Message = message
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            });
        });
    }
}
