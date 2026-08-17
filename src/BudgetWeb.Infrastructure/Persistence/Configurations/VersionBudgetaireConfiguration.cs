using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class VersionBudgetaireConfiguration : IEntityTypeConfiguration<VersionBudgetaire>
{
    public void Configure(EntityTypeBuilder<VersionBudgetaire> builder)
    {
        builder.ToTable("VERSION_BUDGETAIRE");

        builder.HasKey(e => e.IdVersion);

        builder.Property(e => e.IdVersion).HasColumnName("IdVersion");
        builder.Property(e => e.FK_ExerciceBudgetaire).HasColumnName("FK_ExerciceBudgetaire");
        builder.Property(e => e.NumeroVersion).HasColumnName("NumeroVersion");
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200);
        builder.Property(e => e.FK_VersionBudgetairePrecedente).HasColumnName("FK_VersionBudgetairePrecedente");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateDebutEffet).HasColumnName("DateDebutEffet");
        builder.Property(e => e.DateFinEffet).HasColumnName("DateFinEffet");
        builder.Property(e => e.Motif).HasColumnName("Motif").HasMaxLength(1000);
        builder.Property(e => e.Statut).HasColumnName("Statut").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");
        builder.Property(e => e.FK_UtilisateurValidation).HasColumnName("FK_UtilisateurValidation");
        builder.Property(e => e.DateValidation).HasColumnName("DateValidation");

        builder.HasOne(e => e.ExerciceBudgetaire)
            .WithMany(e => e.VersionsBudgetaires)
            .HasForeignKey(e => e.FK_ExerciceBudgetaire)
            .HasConstraintName("FK_VERSION_EXERCICE");

        builder.HasOne(e => e.VersionPrecedente)
            .WithMany(e => e.VersionsSuivantes)
            .HasForeignKey(e => e.FK_VersionBudgetairePrecedente)
            .HasConstraintName("FK_VERSION_PRECEDENTE");

        builder.HasOne(e => e.UtilisateurCreation)
            .WithMany(e => e.VersionsCreees)
            .HasForeignKey(e => e.FK_UtilisateurCreation)
            .HasConstraintName("FK_VERSION_UTILISATEUR_CREATION");

        builder.HasOne(e => e.UtilisateurValidation)
            .WithMany(e => e.VersionsValidees)
            .HasForeignKey(e => e.FK_UtilisateurValidation)
            .HasConstraintName("FK_VERSION_UTILISATEUR_VALIDATION");
    }
}
