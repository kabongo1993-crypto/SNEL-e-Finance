using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class AjustementBudgetaireConfiguration : IEntityTypeConfiguration<AjustementBudgetaire>
{
    public void Configure(EntityTypeBuilder<AjustementBudgetaire> builder)
    {
        builder.ToTable("AJUSTEMENT_BUDGETAIRE");

        builder.HasKey(e => e.IdAjustement);

        builder.Property(e => e.IdAjustement).HasColumnName("IdAjustement");
        builder.Property(e => e.Reference).HasColumnName("Reference").HasMaxLength(40).IsRequired();
        builder.Property(e => e.FK_PrevisionBudgetaire).HasColumnName("FK_PrevisionBudgetaire");
        builder.Property(e => e.FK_VersionBudgetaire).HasColumnName("FK_VersionBudgetaire");
        builder.Property(e => e.FK_UniteBudgetaire).HasColumnName("FK_UniteBudgetaire");
        builder.Property(e => e.FK_ExerciceBudgetaire).HasColumnName("FK_ExerciceBudgetaire");
        builder.Property(e => e.MontantAncien).HasColumnName("MontantAncien").HasColumnType("decimal(19,4)");
        builder.Property(e => e.MontantNouveau).HasColumnName("MontantNouveau").HasColumnType("decimal(19,4)");
        builder.Property(e => e.Variation).HasColumnName("Variation").HasColumnType("decimal(19,4)");
        builder.Property(e => e.Motif).HasColumnName("Motif").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.Statut).HasColumnName("Statut").HasMaxLength(30).IsRequired();
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.FK_UtilisateurValidation).HasColumnName("FK_UtilisateurValidation");
        builder.Property(e => e.DateValidation).HasColumnName("DateValidation");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");

        builder.HasIndex(e => e.Reference).IsUnique().HasDatabaseName("UX_AJUSTEMENT_REFERENCE");

        builder.HasOne(e => e.PrevisionBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_PrevisionBudgetaire)
            .HasConstraintName("FK_AJUST_PREVISION");

        builder.HasOne(e => e.VersionBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_VersionBudgetaire)
            .HasConstraintName("FK_AJUST_VERSION");

        builder.HasOne(e => e.UniteBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_UniteBudgetaire)
            .HasConstraintName("FK_AJUST_UB");

        builder.HasOne(e => e.ExerciceBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_ExerciceBudgetaire)
            .HasConstraintName("FK_AJUST_EXERCICE");

        builder.HasOne(e => e.UtilisateurCreation)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurCreation)
            .HasConstraintName("FK_AJUST_USER_CREATION");

        builder.HasOne(e => e.UtilisateurValidation)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurValidation)
            .HasConstraintName("FK_AJUST_USER_VALIDATION");

        builder.HasOne(e => e.UtilisateurModification)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurModification)
            .HasConstraintName("FK_AJUST_USER_MODIF");
    }
}
