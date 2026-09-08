using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class BilletConversionConfiguration : IEntityTypeConfiguration<BilletConversion>
{
    public void Configure(EntityTypeBuilder<BilletConversion> builder)
    {
        builder.ToTable("BILLET_CONVERSION", "dpm");
        builder.HasKey(e => e.IdBilletConversion);
        builder.Property(e => e.IdBilletConversion).HasColumnName("IdBilletConversion").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_DemandePaiement).HasColumnName("FK_DemandePaiement");
        builder.Property(e => e.DateConversion).HasColumnName("DateConversion");
        builder.Property(e => e.DeviseOrigine).HasColumnName("DeviseOrigine").HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.MontantDeviseOrigine).HasColumnName("MontantDeviseOrigine").HasColumnType("decimal(19,4)");
        builder.Property(e => e.TauxApplique).HasColumnName("TauxApplique").HasColumnType("decimal(19,8)");
        builder.Property(e => e.MontantCdf).HasColumnName("MontantCdf").HasColumnType("decimal(19,4)");
        builder.Property(e => e.FK_TauxChange).HasColumnName("FK_TauxChange");
        builder.Property(e => e.DemandeChequeNumero).HasColumnName("DemandeChequeNumero").HasMaxLength(80);
        builder.Property(e => e.CoursEchangeBanque).HasColumnName("CoursEchangeBanque").HasMaxLength(200);
        builder.Property(e => e.SoldeAPayerDevise).HasColumnName("SoldeAPayerDevise").HasColumnType("decimal(19,4)");
        builder.Property(e => e.Statut).HasColumnName("Statut").HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.FK_UtilisateurEtabli).HasColumnName("FK_UtilisateurEtabli");
        builder.Property(e => e.DateEtabli).HasColumnName("DateEtabli");
        builder.Property(e => e.FK_UtilisateurApprouve).HasColumnName("FK_UtilisateurApprouve");
        builder.Property(e => e.FK_UtilisateurVisa).HasColumnName("FK_UtilisateurVisa");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");

        builder.HasIndex(e => e.FK_DemandePaiement).IsUnique();

        builder.HasOne(e => e.DemandePaiement)
            .WithOne(d => d.BilletConversion)
            .HasForeignKey<BilletConversion>(e => e.FK_DemandePaiement)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TauxChange)
            .WithMany()
            .HasForeignKey(e => e.FK_TauxChange)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UtilisateurEtabli)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurEtabli)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UtilisateurApprouve)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurApprouve)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UtilisateurVisa)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurVisa)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UtilisateurModification)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurModification)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
