using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Persistence;

public class BudgetDbContext : DbContext
{
    public BudgetDbContext(DbContextOptions<BudgetDbContext> options)
        : base(options)
    {
    }

    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<Departement> Departements => Set<Departement>();
    public DbSet<StructureOrganisationnelle> StructuresOrganisationnelles => Set<StructureOrganisationnelle>();
    public DbSet<UniteBudgetaire> UnitesBudgetaires => Set<UniteBudgetaire>();
    public DbSet<RubriqueBudgetaire> RubriquesBudgetaires => Set<RubriqueBudgetaire>();
    public DbSet<TypeBudget> TypesBudget => Set<TypeBudget>();
    public DbSet<ModePrevision> ModesPrevision => Set<ModePrevision>();
    public DbSet<ExerciceBudgetaire> ExercicesBudgetaires => Set<ExerciceBudgetaire>();
    public DbSet<VersionBudgetaire> VersionsBudgetaires => Set<VersionBudgetaire>();
    public DbSet<GroupeItemAE> GroupesItemsAE => Set<GroupeItemAE>();
    public DbSet<ItemBI> ItemsBI => Set<ItemBI>();
    public DbSet<PrevisionBudgetaire> PrevisionsBudgetaires => Set<PrevisionBudgetaire>();
    public DbSet<RepartitionMensuelle> RepartitionsMensuelles => Set<RepartitionMensuelle>();
    public DbSet<Autorisation> Autorisations => Set<Autorisation>();
    public DbSet<TransfertBudgetaire> TransfertsBudgetaires => Set<TransfertBudgetaire>();
    public DbSet<OperationReduction> OperationsReduction => Set<OperationReduction>();
    public DbSet<JournalAudit> JournalAudits => Set<JournalAudit>();
    public DbSet<VPrevisions> VPrevisions => Set<VPrevisions>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BudgetDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
