using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DemandePaiementConfiguration : IEntityTypeConfiguration<DemandePaiement>
{
    public void Configure(EntityTypeBuilder<DemandePaiement> builder)
    {
        builder.ToTable("DEMANDE_PAIEMENT", "dpm");
        builder.HasKey(e => e.IdDemandePaiement);
        builder.Property(e => e.IdDemandePaiement).HasColumnName("IdDemandePaiement").ValueGeneratedOnAdd();
        builder.Property(e => e.Reference).HasColumnName("Reference").HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(e => e.DateEmission).HasColumnName("DateEmission");
        builder.Property(e => e.LieuEmission).HasColumnName("LieuEmission").HasMaxLength(100);
        builder.Property(e => e.FK_ExerciceBudgetaire).HasColumnName("FK_ExerciceBudgetaire");
        builder.Property(e => e.FK_VersionBudgetaire).HasColumnName("FK_VersionBudgetaire");
        builder.Property(e => e.FK_UniteBudgetaire).HasColumnName("FK_UniteBudgetaire");
        builder.Property(e => e.FK_Demandeur).HasColumnName("FK_Demandeur");
        builder.Property(e => e.FK_CasDossier).HasColumnName("FK_CasDossier");
        builder.Property(e => e.TypeBudgetSollicite).HasColumnName("TypeBudgetSollicite").HasMaxLength(3).IsUnicode(false);
        builder.Property(e => e.ItemSollicite).HasColumnName("ItemSollicite").HasMaxLength(100);
        builder.Property(e => e.FK_TypeBudget).HasColumnName("FK_TypeBudget");
        builder.Property(e => e.Objet).HasColumnName("Objet").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.CompteSection).HasColumnName("CompteSection").HasMaxLength(20).IsUnicode(false);
        builder.Property(e => e.MontantBrut).HasColumnName("MontantBrut").HasColumnType("decimal(19,4)");
        builder.Property(e => e.FK_Devise).HasColumnName("FK_Devise");
        builder.Property(e => e.Devise).HasColumnName("Devise").HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.TauxConversion).HasColumnName("TauxConversion").HasColumnType("decimal(19,8)");
        builder.Property(e => e.MontantUsd).HasColumnName("MontantUsd").HasColumnType("decimal(19,4)");
        builder.Property(e => e.FK_TauxChange).HasColumnName("FK_TauxChange");
        builder.Property(e => e.ModePaiementSollicite).HasColumnName("ModePaiementSollicite").HasMaxLength(10).IsUnicode(false);
        builder.Property(e => e.TypeInstrumentPaiement).HasColumnName("TypeInstrumentPaiement").HasMaxLength(40).IsUnicode(false);
        builder.Property(e => e.DevisePaiement).HasColumnName("DevisePaiement").HasMaxLength(3).IsUnicode(false);
        builder.Property(e => e.MontantPaiement).HasColumnName("MontantPaiement").HasColumnType("decimal(19,4)");
        builder.Property(e => e.TauxPaiement).HasColumnName("TauxPaiement").HasColumnType("decimal(19,8)");
        builder.Property(e => e.FK_TauxChangePaiement).HasColumnName("FK_TauxChangePaiement");
        builder.Property(e => e.Statut).HasColumnName("Statut").HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(e => e.MotifRetour).HasColumnName("MotifRetour").HasMaxLength(500);
        builder.Property(e => e.CommentaireRetour).HasColumnName("CommentaireRetour").HasMaxLength(2000);
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");
        builder.Property(e => e.FK_UtilisateurSoumission).HasColumnName("FK_UtilisateurSoumission");
        builder.Property(e => e.DateSoumission).HasColumnName("DateSoumission");
        builder.Property(e => e.FK_UtilisateurReception).HasColumnName("FK_UtilisateurReception");
        builder.Property(e => e.DateReception).HasColumnName("DateReception");
        builder.Property(e => e.FK_UtilisateurControle).HasColumnName("FK_UtilisateurControle");
        builder.Property(e => e.DateControle).HasColumnName("DateControle");
        builder.Property(e => e.FK_UtilisateurVisa).HasColumnName("FK_UtilisateurVisa");
        builder.Property(e => e.DateVisa).HasColumnName("DateVisa");
        builder.Property(e => e.FK_UtilisateurRetour).HasColumnName("FK_UtilisateurRetour");
        builder.Property(e => e.DateRetour).HasColumnName("DateRetour");
        builder.Property(e => e.FK_UtilisateurAssigne).HasColumnName("FK_UtilisateurAssigne");

        builder.HasIndex(e => e.Reference).IsUnique().HasDatabaseName("UX_DPM_DP_Reference");
        builder.HasIndex(e => new { e.Statut, e.DateSoumission }).HasDatabaseName("IX_DPM_DP_Statut_DateSoumission");
        builder.HasIndex(e => new { e.FK_UniteBudgetaire, e.Statut }).HasDatabaseName("IX_DPM_DP_UB_Statut");
        builder.HasIndex(e => new { e.FK_ExerciceBudgetaire, e.FK_CasDossier }).HasDatabaseName("IX_DPM_DP_Exercice_CasDossier");
        builder.HasIndex(e => e.FK_Demandeur).HasDatabaseName("IX_DPM_DP_Demandeur");

        builder.HasOne(e => e.ExerciceBudgetaire).WithMany().HasForeignKey(e => e.FK_ExerciceBudgetaire).HasConstraintName("FK_DPM_DP_EXERCICE");
        builder.HasOne(e => e.VersionBudgetaire).WithMany().HasForeignKey(e => e.FK_VersionBudgetaire).HasConstraintName("FK_DPM_DP_VERSION");
        builder.HasOne(e => e.UniteBudgetaire).WithMany().HasForeignKey(e => e.FK_UniteBudgetaire).HasConstraintName("FK_DPM_DP_UB");
        builder.HasOne(e => e.Demandeur).WithMany(d => d.DemandesPaiement).HasForeignKey(e => e.FK_Demandeur).HasConstraintName("FK_DPM_DP_DEMANDEUR");
        builder.HasOne(e => e.CasDossier).WithMany(c => c.DemandesPaiement).HasForeignKey(e => e.FK_CasDossier).HasConstraintName("FK_DPM_DP_CAS_DOSSIER");
        builder.HasOne(e => e.DeviseRef).WithMany(d => d.DemandesPaiement).HasForeignKey(e => e.FK_Devise).HasConstraintName("FK_DPM_DP_Devise");
        builder.HasOne(e => e.TypeBudget).WithMany().HasForeignKey(e => e.FK_TypeBudget).HasConstraintName("FK_DPM_DP_TYPE_BUDGET");
        builder.HasOne(e => e.TauxChange).WithMany(t => t.DemandesPaiement).HasForeignKey(e => e.FK_TauxChange).HasConstraintName("FK_DPM_DP_TAUX_CHANGE");
        builder.HasOne(e => e.TauxChangePaiement).WithMany().HasForeignKey(e => e.FK_TauxChangePaiement).HasConstraintName("FK_DPM_DP_TAUX_PAIEMENT");
        builder.HasOne(e => e.UtilisateurCreation).WithMany().HasForeignKey(e => e.FK_UtilisateurCreation).HasConstraintName("FK_DPM_DP_USER_CREATION");
        builder.HasOne(e => e.UtilisateurModification).WithMany().HasForeignKey(e => e.FK_UtilisateurModification).HasConstraintName("FK_DPM_DP_USER_MODIF");
        builder.HasOne(e => e.UtilisateurSoumission).WithMany().HasForeignKey(e => e.FK_UtilisateurSoumission).HasConstraintName("FK_DPM_DP_USER_SOUMISSION");
        builder.HasOne(e => e.UtilisateurReception).WithMany().HasForeignKey(e => e.FK_UtilisateurReception).HasConstraintName("FK_DPM_DP_USER_RECEPTION");
        builder.HasOne(e => e.UtilisateurControle).WithMany().HasForeignKey(e => e.FK_UtilisateurControle).HasConstraintName("FK_DPM_DP_USER_CONTROLE");
        builder.HasOne(e => e.UtilisateurVisa).WithMany().HasForeignKey(e => e.FK_UtilisateurVisa).HasConstraintName("FK_DPM_DP_USER_VISA");
        builder.HasOne(e => e.UtilisateurRetour).WithMany().HasForeignKey(e => e.FK_UtilisateurRetour).HasConstraintName("FK_DPM_DP_USER_RETOUR");
        builder.HasOne(e => e.UtilisateurAssigne).WithMany().HasForeignKey(e => e.FK_UtilisateurAssigne).HasConstraintName("FK_DPM_DP_USER_ASSIGNE");
    }
}
