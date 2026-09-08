using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Interfaces.Referentiels;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Services;
using BudgetWeb.Application.Services;
using BudgetWeb.Application.SnelComptes.Import.Interfaces;
using BudgetWeb.Application.SnelComptes.Import.Services;
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
        services.AddScoped<IStructureService, StructureService>();
        services.AddScoped<IUniteBudgetaireService, UniteBudgetaireService>();
        services.AddScoped<IExerciceService, ExerciceService>();
        services.AddScoped<ITypeBudgetService, TypeBudgetService>();
        services.AddScoped<IVersionBudgetaireService, VersionBudgetaireService>();
        services.AddScoped<IRubriqueBudgetaireService, RubriqueBudgetaireService>();
        services.AddScoped<IGroupeRubriqueBudgetaireService, GroupeRubriqueBudgetaireService>();
        services.AddScoped<IItemBIService, ItemBIService>();
        services.AddScoped<IPrevisionBudgetaireService, PrevisionBudgetaireService>();
        services.AddScoped<IGroupeItemAEService, GroupeItemAEService>();
        services.AddScoped<IClassementAeService, ClassementAeService>();
        services.AddScoped<ISuiviPrevisionService, SuiviPrevisionService>();
        services.AddScoped<IWorkflowPrevisionUbService, WorkflowPrevisionUbService>();
        services.AddScoped<IHistoriquePrevisionService, HistoriquePrevisionService>();
        services.AddScoped<IDocumentPrevisionService, DocumentPrevisionService>();
        services.AddScoped<IAjustementBudgetaireService, AjustementBudgetaireService>();
        services.AddScoped<IDemandePaiementService, DemandePaiementService>();
        services.AddScoped<IDocumentsEtablisService, DocumentsEtablisService>();
        services.AddScoped<IDemandePaiementBatchService, DemandePaiementBatchService>();
        services.AddSingleton<PieceJointeUploadValidator>();
        services.AddScoped<IDemandeurService, DemandeurService>();
        services.AddScoped<ITauxChangeService, TauxChangeService>();
        services.AddScoped<ICasDossierService, CasDossierService>();
        services.AddScoped<IDeviseService, DeviseService>();
        services.AddScoped<IBanqueService, BanqueService>();
        services.AddScoped<IGroupeTypeCompteService, GroupeTypeCompteService>();
        services.AddScoped<ITypeCompteService, TypeCompteService>();
        services.AddScoped<ICategorieCompteService, CategorieCompteService>();
        services.AddScoped<IDirectionService, DirectionService>();
        services.AddScoped<IProvinceService, ProvinceService>();
        services.AddScoped<ICompteFinancierService, CompteFinancierService>();
        services.AddScoped<ICompteCategorieService, CompteCategorieService>();
        services.AddScoped<IParametreInstrumentPaiementService, ParametreInstrumentPaiementService>();
        services.AddScoped<IRapportOrganisationResolver, RapportOrganisationResolver>();
        services.AddScoped<IRapportPrevisionDcService, RapportPrevisionDcService>();
        services.AddScoped<IRapportPrevisionDcConsolideService, RapportPrevisionDcConsolideService>();
        services.AddScoped<IRapportPrevisionAeService, RapportPrevisionAeService>();
        services.AddScoped<IRapportPrevisionBiService, RapportPrevisionBiService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUtilisateurAdminService, UtilisateurAdminService>();
        services.AddScoped<ISnelComptesImportService, SnelComptesImportService>();
        services.AddScoped<IPerimetreAccesService, PerimetreAccesService>();
        services.AddScoped<IMcpAuditService, McpAuditService>();

        return services;
    }
}
