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
    public DbSet<GroupeRubriqueBudgetaire> GroupesRubriquesBudgetaires => Set<GroupeRubriqueBudgetaire>();
    public DbSet<TypeBudget> TypesBudget => Set<TypeBudget>();
    public DbSet<ModePrevision> ModesPrevision => Set<ModePrevision>();
    public DbSet<ExerciceBudgetaire> ExercicesBudgetaires => Set<ExerciceBudgetaire>();
    public DbSet<VersionBudgetaire> VersionsBudgetaires => Set<VersionBudgetaire>();
    public DbSet<GroupeItemAE> GroupesItemsAE => Set<GroupeItemAE>();
    public DbSet<ClassementAE> ClassementsAE => Set<ClassementAE>();
    public DbSet<ItemBI> ItemsBI => Set<ItemBI>();
    public DbSet<PrevisionBudgetaire> PrevisionsBudgetaires => Set<PrevisionBudgetaire>();
    public DbSet<RepartitionMensuelle> RepartitionsMensuelles => Set<RepartitionMensuelle>();
    public DbSet<Autorisation> Autorisations => Set<Autorisation>();
    public DbSet<TransfertBudgetaire> TransfertsBudgetaires => Set<TransfertBudgetaire>();
    public DbSet<OperationReduction> OperationsReduction => Set<OperationReduction>();
    public DbSet<AjustementBudgetaire> AjustementsBudgetaires => Set<AjustementBudgetaire>();
    public DbSet<JournalAudit> JournalAudits => Set<JournalAudit>();
    public DbSet<WorkflowPrevisionUb> WorkflowsPrevisionUb => Set<WorkflowPrevisionUb>();
    public DbSet<DocumentPrevision> DocumentsPrevision => Set<DocumentPrevision>();
    public DbSet<DocumentPrevisionSequence> DocumentPrevisionSequences => Set<DocumentPrevisionSequence>();
    public DbSet<VPrevisions> VPrevisions => Set<VPrevisions>();

    public DbSet<CasDossier> CasDossiers => Set<CasDossier>();
    public DbSet<Devise> Devises => Set<Devise>();
    public DbSet<CasDossierPieceObligatoire> CasDossierPiecesObligatoires => Set<CasDossierPieceObligatoire>();
    public DbSet<TauxChange> TauxChanges => Set<TauxChange>();
    public DbSet<PaireTauxChange> PairesTauxChange => Set<PaireTauxChange>();
    public DbSet<Demandeur> Demandeurs => Set<Demandeur>();
    public DbSet<DemandePaiement> DemandesPaiement => Set<DemandePaiement>();
    public DbSet<DemandePaiementRoutage> DemandePaiementRoutages => Set<DemandePaiementRoutage>();
    public DbSet<DemandePaiementValidation> DemandePaiementValidations => Set<DemandePaiementValidation>();
    public DbSet<DemandePaiementBeneficiaire> DemandePaiementBeneficiaires => Set<DemandePaiementBeneficiaire>();
    public DbSet<DemandePaiementImputation> DemandePaiementImputations => Set<DemandePaiementImputation>();
    public DbSet<DemandePaiementImputationSnapshot> DemandePaiementImputationSnapshots => Set<DemandePaiementImputationSnapshot>();
    public DbSet<PieceJointe> PiecesJointes => Set<PieceJointe>();
    public DbSet<BilletConversion> BilletsConversion => Set<BilletConversion>();
    public DbSet<PieceCaisse> PiecesCaisse => Set<PieceCaisse>();
    public DbSet<BonProvisoire> BonsProvisoire => Set<BonProvisoire>();
    public DbSet<MinuteCheque> MinutesCheque => Set<MinuteCheque>();
    public DbSet<ParametreInstrumentPaiement> ParametresInstrumentPaiement => Set<ParametreInstrumentPaiement>();
    public DbSet<ProfilUtilisateur> ProfilsUtilisateur => Set<ProfilUtilisateur>();
    public DbSet<PermissionUtilisateur> PermissionsUtilisateur => Set<PermissionUtilisateur>();
    public DbSet<PerimetreUtilisateur> PerimetresUtilisateur => Set<PerimetreUtilisateur>();
    public DbSet<PerimetreDepartement> PerimetresDepartement => Set<PerimetreDepartement>();
    public DbSet<PerimetreUniteBudgetaire> PerimetresUniteBudgetaire => Set<PerimetreUniteBudgetaire>();

    public DbSet<Banque> Banques => Set<Banque>();
    public DbSet<DirectionTresorerie> DirectionsTresorerie => Set<DirectionTresorerie>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<GroupeTypeCompte> GroupesTypeCompte => Set<GroupeTypeCompte>();
    public DbSet<TypeCompte> TypesCompte => Set<TypeCompte>();
    public DbSet<CategorieCompte> CategoriesCompte => Set<CategorieCompte>();
    public DbSet<CompteFinancier> ComptesFinanciers => Set<CompteFinancier>();
    public DbSet<CompteCategorie> ComptesCategories => Set<CompteCategorie>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BudgetDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
