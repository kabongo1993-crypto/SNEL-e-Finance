using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DemandePaiementValidationConfiguration : IEntityTypeConfiguration<DemandePaiementValidation>
{
    public void Configure(EntityTypeBuilder<DemandePaiementValidation> builder)
    {
        builder.ToTable("DEMANDE_PAIEMENT_VALIDATION", "dpm");
        builder.HasKey(e => e.IdValidation);
        builder.Property(e => e.IdValidation).HasColumnName("IdValidation").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_DemandePaiement).HasColumnName("FK_DemandePaiement");
        builder.Property(e => e.Niveau).HasColumnName("Niveau");
        builder.Property(e => e.Ordre).HasColumnName("Ordre");
        builder.Property(e => e.Statut).HasColumnName("Statut").HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.ModeValidation).HasColumnName("ModeValidation").HasMaxLength(20).IsUnicode(false);
        builder.Property(e => e.FK_UtilisateurValidateur).HasColumnName("FK_UtilisateurValidateur");
        builder.Property(e => e.FK_UtilisateurDeclarant).HasColumnName("FK_UtilisateurDeclarant");
        builder.Property(e => e.NomSignatairePhysique).HasColumnName("NomSignatairePhysique").HasMaxLength(200);
        builder.Property(e => e.FonctionSignatairePhysique).HasColumnName("FonctionSignatairePhysique").HasMaxLength(200);
        builder.Property(e => e.DateSignaturePhysique).HasColumnName("DateSignaturePhysique");
        builder.Property(e => e.DateValidation).HasColumnName("DateValidation");
        builder.Property(e => e.Commentaire).HasColumnName("Commentaire").HasMaxLength(1000);
        builder.Property(e => e.EmpreinteDonnees).HasColumnName("EmpreinteDonnees").HasMaxLength(64).IsUnicode(false);

        builder.HasIndex(e => new { e.FK_DemandePaiement, e.Niveau })
            .IsUnique()
            .HasDatabaseName("UX_DPM_VAL_Demande_Niveau");

        builder.HasOne(e => e.DemandePaiement)
            .WithMany(d => d.ValidationsEntite)
            .HasForeignKey(e => e.FK_DemandePaiement)
            .HasConstraintName("FK_DPM_VAL_DEMANDE");

        builder.HasOne(e => e.UtilisateurValidateur)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurValidateur)
            .HasConstraintName("FK_DPM_VAL_USER_VALIDATEUR");

        builder.HasOne(e => e.UtilisateurDeclarant)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurDeclarant)
            .HasConstraintName("FK_DPM_VAL_USER_DECLARANT");
    }
}
