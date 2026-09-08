using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class WorkflowPrevisionUbConfiguration : IEntityTypeConfiguration<WorkflowPrevisionUb>
{
    public void Configure(EntityTypeBuilder<WorkflowPrevisionUb> builder)
    {
        builder.ToTable("WORKFLOW_PREVISION_UB");

        builder.HasKey(e => e.IdWorkflowPrevisionUB);
        builder.Property(e => e.IdWorkflowPrevisionUB).HasColumnName("IdWorkflowPrevisionUB").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_VersionBudgetaire).HasColumnName("FK_VersionBudgetaire");
        builder.Property(e => e.FK_UniteBudgetaire).HasColumnName("FK_UniteBudgetaire");
        builder.Property(e => e.Statut).HasColumnName("Statut").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.FK_UtilisateurSoumission).HasColumnName("FK_UtilisateurSoumission");
        builder.Property(e => e.DateSoumission).HasColumnName("DateSoumission");
        builder.Property(e => e.FK_UtilisateurControle).HasColumnName("FK_UtilisateurControle");
        builder.Property(e => e.DateControle).HasColumnName("DateControle");
        builder.Property(e => e.FK_UtilisateurValidation).HasColumnName("FK_UtilisateurValidation");
        builder.Property(e => e.DateValidation).HasColumnName("DateValidation");
        builder.Property(e => e.FK_UtilisateurRejet).HasColumnName("FK_UtilisateurRejet");
        builder.Property(e => e.DateRejet).HasColumnName("DateRejet");
        builder.Property(e => e.MotifRejet).HasColumnName("MotifRejet").HasMaxLength(1000);
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");

        builder.HasIndex(e => new { e.FK_VersionBudgetaire, e.FK_UniteBudgetaire })
            .IsUnique()
            .HasDatabaseName("UX_WORKFLOW_PREVISION_UB_VERSION_UB");

        builder.HasOne(e => e.VersionBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_VersionBudgetaire)
            .HasConstraintName("FK_WF_UB_VERSION");

        builder.HasOne(e => e.UniteBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_UniteBudgetaire)
            .HasConstraintName("FK_WF_UB_UNITE");

        builder.HasOne(e => e.UtilisateurCreation)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurCreation)
            .HasConstraintName("FK_WF_UB_USER_CREATION");

        builder.HasOne(e => e.UtilisateurModification)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurModification)
            .HasConstraintName("FK_WF_UB_USER_MODIF");

        builder.HasOne(e => e.UtilisateurSoumission)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurSoumission)
            .HasConstraintName("FK_WF_UB_USER_SOUMISSION");

        builder.HasOne(e => e.UtilisateurControle)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurControle)
            .HasConstraintName("FK_WF_UB_USER_CONTROLE");

        builder.HasOne(e => e.UtilisateurValidation)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurValidation)
            .HasConstraintName("FK_WF_UB_USER_VALIDATION");

        builder.HasOne(e => e.UtilisateurRejet)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurRejet)
            .HasConstraintName("FK_WF_UB_USER_REJET");
    }
}
