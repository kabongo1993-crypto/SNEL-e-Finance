using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class TransfertBudgetaireConfiguration : IEntityTypeConfiguration<TransfertBudgetaire>
{
    public void Configure(EntityTypeBuilder<TransfertBudgetaire> builder)
    {
        builder.ToTable("TRANSFERT_BUDGETAIRE");

        builder.HasKey(e => e.IdTransfert);

        builder.Property(e => e.IdTransfert).HasColumnName("IdTransfert");
        builder.Property(e => e.FK_VersionBudgetaire).HasColumnName("FK_VersionBudgetaire");
        builder.Property(e => e.FK_PrevisionBudgetaireSource).HasColumnName("FK_PrevisionBudgetaireSource");
        builder.Property(e => e.FK_PrevisionBudgetaireDestination).HasColumnName("FK_PrevisionBudgetaireDestination");
        builder.Property(e => e.Montant).HasColumnName("Montant").HasColumnType("decimal(19,4)");
        builder.Property(e => e.DateEffet).HasColumnName("DateEffet");
        builder.Property(e => e.Motif).HasColumnName("Motif").HasMaxLength(1000);
        builder.Property(e => e.FK_Autorisation).HasColumnName("FK_Autorisation");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");

        builder.HasOne(e => e.VersionBudgetaire)
            .WithMany(e => e.TransfertsBudgetaires)
            .HasForeignKey(e => e.FK_VersionBudgetaire)
            .HasConstraintName("FK_TRANSFERT_VERSION");

        builder.HasOne(e => e.PrevisionSource)
            .WithMany(e => e.TransfertsSource)
            .HasForeignKey(e => e.FK_PrevisionBudgetaireSource)
            .HasConstraintName("FK_TRANSFERT_SOURCE");

        builder.HasOne(e => e.PrevisionDestination)
            .WithMany(e => e.TransfertsDestination)
            .HasForeignKey(e => e.FK_PrevisionBudgetaireDestination)
            .HasConstraintName("FK_TRANSFERT_DESTINATION");

        builder.HasOne(e => e.Autorisation)
            .WithMany(e => e.TransfertsBudgetaires)
            .HasForeignKey(e => e.FK_Autorisation)
            .HasConstraintName("FK_TRANSFERT_AUTORISATION");

        builder.HasOne(e => e.UtilisateurCreation)
            .WithMany(e => e.TransfertsCrees)
            .HasForeignKey(e => e.FK_UtilisateurCreation)
            .HasConstraintName("FK_TRANSFERT_UTILISATEUR_CREATION");
    }
}
