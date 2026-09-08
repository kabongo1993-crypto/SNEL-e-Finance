using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DemandePaiementBeneficiaireConfiguration : IEntityTypeConfiguration<DemandePaiementBeneficiaire>
{
    public void Configure(EntityTypeBuilder<DemandePaiementBeneficiaire> builder)
    {
        builder.ToTable("DEMANDE_PAIEMENT_BENEFICIAIRE", "dpm");
        builder.HasKey(e => e.IdBeneficiaire);
        builder.Property(e => e.IdBeneficiaire).HasColumnName("IdBeneficiaire").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_DemandePaiement).HasColumnName("FK_DemandePaiement");
        builder.Property(e => e.TypeBeneficiaire).HasColumnName("TypeBeneficiaire").HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.NomComplet).HasColumnName("NomComplet").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Matricule).HasColumnName("Matricule").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.Fonction).HasColumnName("Fonction").HasMaxLength(200);
        builder.Property(e => e.RaisonSociale).HasColumnName("RaisonSociale").HasMaxLength(300);
        builder.Property(e => e.Rccm).HasColumnName("Rccm").HasMaxLength(50).IsUnicode(false);
        builder.Property(e => e.Adresse).HasColumnName("Adresse").HasMaxLength(500);
        builder.Property(e => e.Banque).HasColumnName("Banque").HasMaxLength(200);
        builder.Property(e => e.NumeroCompte).HasColumnName("NumeroCompte").HasMaxLength(50).IsUnicode(false);
        builder.Property(e => e.EstPrincipal).HasColumnName("EstPrincipal");
        builder.Property(e => e.Ordre).HasColumnName("Ordre");
        builder.HasIndex(e => new { e.FK_DemandePaiement, e.Ordre }).HasDatabaseName("IX_DPM_BENEF_DEMANDE");
        builder.HasIndex(e => e.NomComplet).HasDatabaseName("IX_DPM_BENEF_NomComplet");
        builder.HasOne(e => e.DemandePaiement).WithMany(d => d.Beneficiaires).HasForeignKey(e => e.FK_DemandePaiement).HasConstraintName("FK_DPM_BENEF_DEMANDE");
    }
}
