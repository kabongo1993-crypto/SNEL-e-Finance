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

        builder.Property(e => e.IdVersion).HasColumnName("IdVersion").ValueGeneratedOnAdd();
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
        builder.Property(e => e.FK_UtilisateurSoumission).HasColumnName("FK_UtilisateurSoumission");
        builder.Property(e => e.DateSoumission).HasColumnName("DateSoumission");
        builder.Property(e => e.FK_UtilisateurControle).HasColumnName("FK_UtilisateurControle");
        builder.Property(e => e.DateControle).HasColumnName("DateControle");
        builder.Property(e => e.FK_UtilisateurRejet).HasColumnName("FK_UtilisateurRejet");
        builder.Property(e => e.DateRejet).HasColumnName("DateRejet");
        builder.Property(e => e.MotifRejet).HasColumnName("MotifRejet").HasMaxLength(1000);

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

        builder.HasOne(e => e.UtilisateurSoumission)
            .WithMany(e => e.VersionsSoumises)
            .HasForeignKey(e => e.FK_UtilisateurSoumission)
            .HasConstraintName("FK_VERSION_UTILISATEUR_SOUMISSION");

        builder.HasOne(e => e.UtilisateurControle)
            .WithMany(e => e.VersionsControlees)
            .HasForeignKey(e => e.FK_UtilisateurControle)
            .HasConstraintName("FK_VERSION_UTILISATEUR_CONTROLE");

        builder.HasOne(e => e.UtilisateurRejet)
            .WithMany(e => e.VersionsRejetees)
            .HasForeignKey(e => e.FK_UtilisateurRejet)
            .HasConstraintName("FK_VERSION_UTILISATEUR_REJET");
    }
}
