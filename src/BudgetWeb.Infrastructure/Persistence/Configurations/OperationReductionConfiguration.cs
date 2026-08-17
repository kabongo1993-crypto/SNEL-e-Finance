using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class OperationReductionConfiguration : IEntityTypeConfiguration<OperationReduction>
{
    public void Configure(EntityTypeBuilder<OperationReduction> builder)
    {
        builder.ToTable("OPERATION_REDUCTION");

        builder.HasKey(e => e.IdOperation);

        builder.Property(e => e.IdOperation).HasColumnName("IdOperation");
        builder.Property(e => e.FK_PrevisionBudgetaire).HasColumnName("FK_PrevisionBudgetaire");
        builder.Property(e => e.Montant).HasColumnName("Montant").HasColumnType("decimal(19,4)");
        builder.Property(e => e.DateEffet).HasColumnName("DateEffet");
        builder.Property(e => e.Motif).HasColumnName("Motif").HasMaxLength(1000);
        builder.Property(e => e.FK_Autorisation).HasColumnName("FK_Autorisation");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");

        builder.HasOne(e => e.PrevisionBudgetaire)
            .WithMany(e => e.OperationsReduction)
            .HasForeignKey(e => e.FK_PrevisionBudgetaire)
            .HasConstraintName("FK_REDUCTION_PREVISION");

        builder.HasOne(e => e.Autorisation)
            .WithMany(e => e.OperationsReduction)
            .HasForeignKey(e => e.FK_Autorisation)
            .HasConstraintName("FK_REDUCTION_AUTORISATION");

        builder.HasOne(e => e.UtilisateurCreation)
            .WithMany(e => e.OperationsReductionCreees)
            .HasForeignKey(e => e.FK_UtilisateurCreation)
            .HasConstraintName("FK_REDUCTION_UTILISATEUR_CREATION");
    }
}
