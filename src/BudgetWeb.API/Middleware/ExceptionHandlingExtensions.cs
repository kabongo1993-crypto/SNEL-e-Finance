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

                context.Response.ContentType = "application/json";

                var (statusCode, message) = exception switch
                {
                    InvalidOperationException => (HttpStatusCode.BadRequest, exception!.Message),
                    _ => (HttpStatusCode.InternalServerError, "Une erreur interne est survenue.")
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
