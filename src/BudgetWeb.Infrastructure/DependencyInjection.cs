using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using BudgetWeb.Infrastructure.Import.Excel;
using BudgetWeb.Infrastructure.Import.Repositories;
using BudgetWeb.Infrastructure.Persistence;
using BudgetWeb.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetWeb.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("La chaîne de connexion 'DefaultConnection' est introuvable.");

        services.AddDbContext<BudgetDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IReferentielRepository, ReferentielRepository>();
        services.AddScoped<IHealthRepository, HealthRepository>();
        services.AddScoped<ICorrespondanceWorkbookReader, CorrespondanceWorkbookReader>();
        services.AddScoped<IReferentielImportRepository, ReferentielImportRepository>();
        services.AddScoped<IReferentielOrganisationnelQueryRepository, ReferentielOrganisationnelQueryRepository>();
        services.AddScoped<IDepartementRepository, DepartementRepository>();

        return services;
    }
}
