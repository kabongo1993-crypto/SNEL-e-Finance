using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DemandePaiementImputationConfiguration : IEntityTypeConfiguration<DemandePaiementImputation>
{
    public void Configure(EntityTypeBuilder<DemandePaiementImputation> builder)
    {
        builder.ToTable("DEMANDE_PAIEMENT_IMPUTATION", "dpm");
        builder.HasKey(e => e.IdImputation);
        builder.Property(e => e.IdImputation).HasColumnName("IdImputation").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_DemandePaiement).HasColumnName("FK_DemandePaiement");
        builder.Property(e => e.Ordre).HasColumnName("Ordre");
        builder.Property(e => e.FK_TypeBudget).HasColumnName("FK_TypeBudget");
        builder.Property(e => e.FK_UniteBudgetaire).HasColumnName("FK_UniteBudgetaire");
        builder.Property(e => e.FK_ExerciceBudgetaire).HasColumnName("FK_ExerciceBudgetaire");
        builder.Property(e => e.FK_RubriqueBudgetaire).HasColumnName("FK_RubriqueBudgetaire");
        builder.Property(e => e.Mois).HasColumnName("Mois");
        builder.Property(e => e.LibelleItemAE).HasColumnName("LibelleItemAE").HasMaxLength(500);
        builder.Property(e => e.FK_GroupeItemAE).HasColumnName("FK_GroupeItemAE");
        builder.Property(e => e.FK_ItemBI).HasColumnName("FK_ItemBI");
        builder.Property(e => e.DetailBI).HasColumnName("DetailBI").HasMaxLength(1000);
        builder.Property(e => e.FK_BudgetLigne).HasColumnName("FK_BudgetLigne");
        builder.Property(e => e.MontantBrut).HasColumnName("MontantBrut").HasColumnType("decimal(19,4)");
        builder.Property(e => e.Devise).HasColumnName("Devise").HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.TauxConversion).HasColumnName("TauxConversion").HasColumnType("decimal(19,8)");
        builder.Property(e => e.MontantUsd).HasColumnName("MontantUsd").HasColumnType("decimal(19,4)");
        builder.Property(e => e.NumeroFicheSuivi).HasColumnName("NumeroFicheSuivi");
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");
        builder.Property(e => e.DateImputation).HasColumnName("DateImputation");

        builder.HasIndex(e => new { e.FK_DemandePaiement, e.Ordre }).HasDatabaseName("IX_DPM_IMPUT_DEMANDE_ORDRE");
        builder.HasIndex(e => new { e.FK_ExerciceBudgetaire, e.FK_UniteBudgetaire, e.FK_RubriqueBudgetaire, e.Mois }).HasFilter("[Mois] IS NOT NULL AND [FK_RubriqueBudgetaire] IS NOT NULL").HasDatabaseName("IX_DPM_IMPUT_DC_CLE");
        builder.HasIndex(e => new { e.FK_ExerciceBudgetaire, e.FK_UniteBudgetaire, e.FK_RubriqueBudgetaire, e.LibelleItemAE, e.FK_GroupeItemAE }).HasFilter("[LibelleItemAE] IS NOT NULL").HasDatabaseName("IX_DPM_IMPUT_AE_CLE");
        builder.HasIndex(e => new { e.FK_ExerciceBudgetaire, e.FK_UniteBudgetaire, e.FK_ItemBI }).HasFilter("[FK_ItemBI] IS NOT NULL AND [DetailBI] IS NOT NULL").HasDatabaseName("IX_DPM_IMPUT_BI_CLE");
        builder.HasIndex(e => e.FK_BudgetLigne).HasFilter("[FK_BudgetLigne] IS NOT NULL").HasDatabaseName("IX_DPM_IMPUT_BUDGET_LIGNE");

        builder.HasOne(e => e.DemandePaiement).WithMany(d => d.Imputations).HasForeignKey(e => e.FK_DemandePaiement).HasConstraintName("FK_DPM_IMPUT_DEMANDE");
        builder.HasOne(e => e.TypeBudget).WithMany().HasForeignKey(e => e.FK_TypeBudget).HasConstraintName("FK_DPM_IMPUT_TYPE_BUDGET");
        builder.HasOne(e => e.UniteBudgetaire).WithMany().HasForeignKey(e => e.FK_UniteBudgetaire).HasConstraintName("FK_DPM_IMPUT_UB");
        builder.HasOne(e => e.ExerciceBudgetaire).WithMany().HasForeignKey(e => e.FK_ExerciceBudgetaire).HasConstraintName("FK_DPM_IMPUT_EXERCICE");
        builder.HasOne(e => e.RubriqueBudgetaire).WithMany().HasForeignKey(e => e.FK_RubriqueBudgetaire).HasConstraintName("FK_DPM_IMPUT_RB");
        builder.HasOne(e => e.GroupeItemAE).WithMany().HasForeignKey(e => e.FK_GroupeItemAE).HasConstraintName("FK_DPM_IMPUT_GROUPE_AE");
        builder.HasOne(e => e.ItemBI).WithMany().HasForeignKey(e => e.FK_ItemBI).HasConstraintName("FK_DPM_IMPUT_ITEM_BI");
        builder.HasOne(e => e.PrevisionBudgetaire).WithMany().HasForeignKey(e => e.FK_BudgetLigne).HasConstraintName("FK_DPM_IMPUT_BUDGET_LIGNE");
        builder.HasOne(e => e.UtilisateurCreation).WithMany().HasForeignKey(e => e.FK_UtilisateurCreation).HasConstraintName("FK_DPM_IMPUT_USER_CREATION");
    }
}
