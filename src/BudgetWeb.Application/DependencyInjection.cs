using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Services;
using BudgetWeb.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetWeb.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IReferentielService, ReferentielService>();
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IReferentielImportService, ReferentielImportService>();
        services.AddScoped<IReferentielOrganisationnelQueryService, ReferentielOrganisationnelQueryService>();
        services.AddScoped<IDepartementService, DepartementService>();

        return services;
    }
}
