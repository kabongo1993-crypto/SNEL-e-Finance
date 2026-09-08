using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class PieceCaisseConfiguration : IEntityTypeConfiguration<PieceCaisse>
{
    public void Configure(EntityTypeBuilder<PieceCaisse> builder)
    {
        builder.ToTable("PIECE_CAISSE", "dpm");
        builder.HasKey(e => e.IdPieceCaisse);
        builder.Property(e => e.NumeroPiece).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.Statut).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.MontantFc).HasColumnType("decimal(19,4)");
        builder.Property(e => e.MontantEnLettres).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ReferenceDemande).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(e => e.IdentifiantVerification).HasMaxLength(120).IsRequired();
        builder.Property(e => e.FK_UtilisateurEtabli).HasColumnName("FK_UtilisateurEtabli");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");
        builder.HasIndex(e => e.FK_DemandePaiement).IsUnique();
        builder.HasIndex(e => e.NumeroPiece).IsUnique();
        builder.HasOne(e => e.DemandePaiement).WithOne(d => d.PieceCaisse)
            .HasForeignKey<PieceCaisse>(e => e.FK_DemandePaiement).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UtilisateurEtabli).WithMany()
            .HasForeignKey(e => e.FK_UtilisateurEtabli).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UtilisateurModification).WithMany()
            .HasForeignKey(e => e.FK_UtilisateurModification).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BonProvisoireConfiguration : IEntityTypeConfiguration<BonProvisoire>
{
    public void Configure(EntityTypeBuilder<BonProvisoire> builder)
    {
        builder.ToTable("BON_PROVISOIRE", "dpm");
        builder.HasKey(e => e.IdBonProvisoire);
        builder.Property(e => e.NumeroBon).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.Statut).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.MontantFc).HasColumnType("decimal(19,4)");
        builder.Property(e => e.MontantEnLettres).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ReferenceDemande).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(e => e.IdentifiantVerification).HasMaxLength(120).IsRequired();
        builder.Property(e => e.FK_UtilisateurEtabli).HasColumnName("FK_UtilisateurEtabli");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");
        builder.HasIndex(e => e.FK_DemandePaiement).IsUnique();
        builder.HasIndex(e => e.NumeroBon).IsUnique();
        builder.HasOne(e => e.DemandePaiement).WithOne(d => d.BonProvisoire)
            .HasForeignKey<BonProvisoire>(e => e.FK_DemandePaiement).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UtilisateurEtabli).WithMany()
            .HasForeignKey(e => e.FK_UtilisateurEtabli).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UtilisateurModification).WithMany()
            .HasForeignKey(e => e.FK_UtilisateurModification).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MinuteChequeConfiguration : IEntityTypeConfiguration<MinuteCheque>
{
    public void Configure(EntityTypeBuilder<MinuteCheque> builder)
    {
        builder.ToTable("MINUTE_CHEQUE", "dpm");
        builder.HasKey(e => e.IdMinuteCheque);
        builder.Property(e => e.NumeroOp).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.Statut).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.DevisePaiement).HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.MontantPaiement).HasColumnType("decimal(19,4)");
        builder.Property(e => e.MontantEnLettres).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ReferenceDemande).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(e => e.IdentifiantVerification).HasMaxLength(120).IsRequired();
        builder.Property(e => e.MontantSuiviExtraComptable).HasColumnType("decimal(19,4)");
        builder.Property(e => e.FK_UtilisateurEtabli).HasColumnName("FK_UtilisateurEtabli");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");
        builder.HasIndex(e => e.FK_DemandePaiement).IsUnique();
        builder.HasIndex(e => e.NumeroOp).IsUnique();
        builder.HasOne(e => e.DemandePaiement).WithOne(d => d.MinuteCheque)
            .HasForeignKey<MinuteCheque>(e => e.FK_DemandePaiement).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UtilisateurEtabli).WithMany()
            .HasForeignKey(e => e.FK_UtilisateurEtabli).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UtilisateurModification).WithMany()
            .HasForeignKey(e => e.FK_UtilisateurModification).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ParametreInstrumentPaiementConfiguration : IEntityTypeConfiguration<ParametreInstrumentPaiement>
{
    public void Configure(EntityTypeBuilder<ParametreInstrumentPaiement> builder)
    {
        builder.ToTable("PARAMETRE_INSTRUMENT_PAIEMENT", "dpm");
        builder.HasKey(e => e.IdParametreInstrumentPaiement);
        builder.Property(e => e.TypeInstrument).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.MontantSuiviExtraComptable).HasColumnType("decimal(19,4)");
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");
        builder.HasIndex(e => e.TypeInstrument).IsUnique();
    }
}
