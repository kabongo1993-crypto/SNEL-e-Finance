using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Interfaces.Referentiels;
using BudgetWeb.Application.Options;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using BudgetWeb.Application.SnelComptes.Import.Interfaces;
using BudgetWeb.Infrastructure.Auth;
using BudgetWeb.Infrastructure.Diagnostics;
using BudgetWeb.Infrastructure.Documents;
using BudgetWeb.Infrastructure.Import;
using BudgetWeb.Infrastructure.Import.Excel;
using BudgetWeb.Infrastructure.Import.Repositories;
using BudgetWeb.Infrastructure.Persistence;
using BudgetWeb.Infrastructure.Repositories;
using BudgetWeb.Infrastructure.Storage;
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

        services.Configure<PieceJointeStorageOptions>(
            configuration.GetSection(PieceJointeStorageOptions.SectionName));
        services.Configure<QuestPdfWarmupOptions>(
            configuration.GetSection(QuestPdfWarmupOptions.SectionName));
        services.AddHostedService<QuestPdfWarmupHostedService>();

        services.AddDbContext<BudgetDbContext>(options =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(new DetailQuerySqlInterceptor()));

        services.AddScoped<IReferentielRepository, ReferentielRepository>();
        services.AddScoped<IHealthRepository, HealthRepository>();
        services.AddScoped<ICorrespondanceWorkbookReader, CorrespondanceWorkbookReader>();
        services.AddScoped<IReferentielImportRepository, ReferentielImportRepository>();
        services.AddScoped<IReferentielOrganisationnelQueryRepository, ReferentielOrganisationnelQueryRepository>();
        services.AddScoped<IDepartementRepository, DepartementRepository>();
        services.AddScoped<IStructureRepository, StructureRepository>();
        services.AddScoped<IUniteBudgetaireRepository, UniteBudgetaireRepository>();
        services.AddScoped<IExerciceRepository, ExerciceRepository>();
        services.AddScoped<ITypeBudgetRepository, TypeBudgetRepository>();
        services.AddScoped<IVersionBudgetaireRepository, VersionBudgetaireRepository>();
        services.AddScoped<IRubriqueBudgetaireRepository, RubriqueBudgetaireRepository>();
        services.AddScoped<IGroupeRubriqueBudgetaireRepository, GroupeRubriqueBudgetaireRepository>();
        services.AddScoped<IItemBIRepository, ItemBIRepository>();
        services.AddScoped<IPrevisionBudgetaireRepository, PrevisionBudgetaireRepository>();
        services.AddScoped<IGroupeItemAERepository, GroupeItemAERepository>();
        services.AddScoped<IClassementAeRepository, ClassementAeRepository>();
        services.AddScoped<ISuiviPrevisionRepository, SuiviPrevisionRepository>();
        services.AddScoped<IWorkflowPrevisionUbRepository, WorkflowPrevisionUbRepository>();
        services.AddScoped<IHistoriquePrevisionRepository, HistoriquePrevisionRepository>();
        services.AddScoped<IDocumentPrevisionRepository, DocumentPrevisionRepository>();
        services.AddScoped<IAjustementBudgetaireRepository, AjustementBudgetaireRepository>();
        services.AddScoped<IDemandePaiementRepository, DemandePaiementRepository>();
        services.AddScoped<IDemandeurRepository, DemandeurRepository>();
        services.AddScoped<ITauxChangeRepository, TauxChangeRepository>();
        services.AddScoped<IPaireTauxChangeRepository, PaireTauxChangeRepository>();
        services.AddScoped<ICasDossierRepository, CasDossierRepository>();
        services.AddScoped<IDeviseRepository, DeviseRepository>();
        services.AddScoped<IBanqueRepository, BanqueRepository>();
        services.AddScoped<IGroupeTypeCompteRepository, GroupeTypeCompteRepository>();
        services.AddScoped<ITypeCompteRepository, TypeCompteRepository>();
        services.AddScoped<ICategorieCompteRepository, CategorieCompteRepository>();
        services.AddScoped<IDirectionRepository, DirectionRepository>();
        services.AddScoped<IProvinceRepository, ProvinceRepository>();
        services.AddScoped<ICompteFinancierRepository, CompteFinancierRepository>();
        services.AddScoped<ICompteCategorieRepository, CompteCategorieRepository>();
        services.AddScoped<IRapportPrevisionDcRepository, RapportPrevisionDcRepository>();
        services.AddScoped<IRapportPrevisionAeRepository, RapportPrevisionAeRepository>();
        services.AddScoped<IRapportPrevisionBiRepository, RapportPrevisionBiRepository>();
        services.AddSingleton<IDocumentPdfRenderer, QuestPdfDocumentRenderer>();
        services.AddSingleton<IDemandePaiementDocumentRenderer, QuestPdfDemandePaiementRenderer>();
        services.AddSingleton<IBilletConversionDocumentRenderer, QuestPdfBilletConversionRenderer>();
        services.AddSingleton<IPieceCaisseDocumentRenderer, QuestPdfPieceCaisseRenderer>();
        services.AddSingleton<IBonProvisoireDocumentRenderer, QuestPdfBonProvisoireRenderer>();
        services.AddSingleton<IMinuteChequeDocumentRenderer, QuestPdfMinuteChequeRenderer>();
        services.AddSingleton<IFicheImputationBudgetaireRenderer, QuestPdfFicheImputationBudgetaireRenderer>();
        services.AddSingleton<IDocumentsEtablisListePdfRenderer, QuestPdfDocumentsEtablisListeRenderer>();
        services.AddSingleton<IDocumentsEtablisPdfMerger, PdfSharpDocumentsEtablisMerger>();
        services.AddSingleton<IRapportPrevisionDcPdfRenderer, RapportPrevisionDcPdfRenderer>();
        services.AddSingleton<IRapportPrevisionDcConsolidePdfRenderer, RapportPrevisionDcConsolidePdfRenderer>();
        services.AddSingleton<IRapportPrevisionAePdfRenderer, RapportPrevisionAePdfRenderer>();
        services.AddSingleton<IRapportPrevisionBiPdfRenderer, RapportPrevisionBiPdfRenderer>();
        services.AddSingleton<IDocumentFileStore, LocalDocumentFileStore>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<IAuthRepository, AuthRepository>();        services.AddScoped<IUtilisateurAdminRepository, UtilisateurAdminRepository>();
        services.AddScoped<IPerimetreUtilisateurReader, PerimetreUtilisateurReader>();
        services.AddScoped<IMcpAuditRepository, McpAuditRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ISnelComptesWorkbookReader, SnelComptesWorkbookReader>();
        services.AddScoped<ISnelComptesImportTransaction, SnelComptesImportTransaction>();

        return services;
    }
}
