using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DemandePaiementRoutageConfiguration : IEntityTypeConfiguration<DemandePaiementRoutage>
{
    public void Configure(EntityTypeBuilder<DemandePaiementRoutage> builder)
    {
        builder.ToTable("DEMANDE_PAIEMENT_ROUTAGE", "dpm");
        builder.HasKey(e => e.IdRoutage);
        builder.Property(e => e.IdRoutage).HasColumnName("IdRoutage").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_DemandePaiement).HasColumnName("FK_DemandePaiement");
        builder.Property(e => e.FK_UtilisateurSource).HasColumnName("FK_UtilisateurSource");
        builder.Property(e => e.FK_UtilisateurCible).HasColumnName("FK_UtilisateurCible");
        builder.Property(e => e.StatutSource).HasColumnName("StatutSource").HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(e => e.StatutCible).HasColumnName("StatutCible").HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(e => e.Action).HasColumnName("Action").HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(e => e.DateRoutage).HasColumnName("DateRoutage");
        builder.Property(e => e.EstActif).HasColumnName("EstActif");
        builder.Property(e => e.Motif).HasColumnName("Motif").HasMaxLength(500);

        builder.HasIndex(e => new { e.FK_DemandePaiement, e.EstActif })
            .HasDatabaseName("IX_DPM_ROUTAGE_Demande_Actif")
            .HasFilter("[EstActif] = 1");

        builder.HasOne(e => e.DemandePaiement)
            .WithMany(d => d.Routages)
            .HasForeignKey(e => e.FK_DemandePaiement)
            .HasConstraintName("FK_DPM_ROUTAGE_DEMANDE")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UtilisateurSource)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurSource)
            .HasConstraintName("FK_DPM_ROUTAGE_USER_SOURCE")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UtilisateurCible)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurCible)
            .HasConstraintName("FK_DPM_ROUTAGE_USER_CIBLE")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
