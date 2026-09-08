using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class PrevisionBudgetaireConfiguration : IEntityTypeConfiguration<PrevisionBudgetaire>
{
    public void Configure(EntityTypeBuilder<PrevisionBudgetaire> builder)
    {
        builder.ToTable("PREVISION_BUDGETAIRE", t =>
        {
            // SQL Server refuse OUTPUT sans INTO quand un trigger est actif.
            t.HasTrigger("TR_PREVISION_COHERENCE");
        });

        builder.HasKey(e => e.IdPrevision);

        builder.Property(e => e.IdPrevision).HasColumnName("IdPrevision").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_VersionBudgetaire).HasColumnName("FK_VersionBudgetaire");
        builder.Property(e => e.FK_TypeBudget).HasColumnName("FK_TypeBudget");
        builder.Property(e => e.FK_UniteBudgetaire).HasColumnName("FK_UniteBudgetaire");
        builder.Property(e => e.FK_RubriqueBudgetaire).HasColumnName("FK_RubriqueBudgetaire");
        builder.Property(e => e.FK_ItemBI).HasColumnName("FK_ItemBI");
        builder.Property(e => e.FK_GroupeItemAE).HasColumnName("FK_GroupeItemAE");
        builder.Property(e => e.LibelleItemAE).HasColumnName("LibelleItemAE").HasMaxLength(500);
        builder.Property(e => e.DetailBI).HasColumnName("DetailBI").HasMaxLength(1000);
        builder.Property(e => e.FK_ModePrevision).HasColumnName("FK_ModePrevision");
        builder.Property(e => e.MontantAnnuel).HasColumnName("MontantAnnuel").HasColumnType("decimal(19,4)");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");

        builder.HasOne(e => e.VersionBudgetaire)
            .WithMany(e => e.PrevisionsBudgetaires)
            .HasForeignKey(e => e.FK_VersionBudgetaire)
            .HasConstraintName("FK_PREVISION_VERSION");

        builder.HasOne(e => e.TypeBudget)
            .WithMany(e => e.PrevisionsBudgetaires)
            .HasForeignKey(e => e.FK_TypeBudget)
            .HasConstraintName("FK_PREVISION_TYPE");

        builder.HasOne(e => e.UniteBudgetaire)
            .WithMany(e => e.PrevisionsBudgetaires)
            .HasForeignKey(e => e.FK_UniteBudgetaire)
            .HasConstraintName("FK_PREVISION_UB");

        builder.HasOne(e => e.RubriqueBudgetaire)
            .WithMany(e => e.PrevisionsBudgetaires)
            .HasForeignKey(e => e.FK_RubriqueBudgetaire)
            .HasConstraintName("FK_PREVISION_RB");

        builder.HasOne(e => e.ItemBI)
            .WithMany(e => e.PrevisionsBudgetaires)
            .HasForeignKey(e => e.FK_ItemBI)
            .HasConstraintName("FK_PREVISION_ITEM_BI");

        builder.HasOne(e => e.GroupeItemAE)
            .WithMany(e => e.PrevisionsBudgetaires)
            .HasForeignKey(e => e.FK_GroupeItemAE)
            .HasConstraintName("FK_PREVISION_GROUPE_AE");

        builder.HasOne(e => e.ModePrevision)
            .WithMany(e => e.PrevisionsBudgetaires)
            .HasForeignKey(e => e.FK_ModePrevision)
            .HasConstraintName("FK_PREVISION_MODE");

        builder.HasOne(e => e.UtilisateurCreation)
            .WithMany(e => e.PrevisionsCreees)
            .HasForeignKey(e => e.FK_UtilisateurCreation)
            .HasConstraintName("FK_PREVISION_UTILISATEUR_CREATION");

        builder.HasOne(e => e.UtilisateurModification)
            .WithMany(e => e.PrevisionsModifiees)
            .HasForeignKey(e => e.FK_UtilisateurModification)
            .HasConstraintName("FK_PREVISION_UTILISATEUR_MODIFICATION");
    }
}
