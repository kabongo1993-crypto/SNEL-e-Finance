using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class CasDossierConfiguration : IEntityTypeConfiguration<CasDossier>
{
    public void Configure(EntityTypeBuilder<CasDossier> builder)
    {
        builder.ToTable("CAS_DOSSIER", "dpm");
        builder.HasKey(e => e.IdCasDossier);
        builder.Property(e => e.IdCasDossier).HasColumnName("IdCasDossier").ValueGeneratedOnAdd();
        builder.Property(e => e.Code).HasColumnName("Code").HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Ordre).HasColumnName("Ordre");
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_DPM_CAS_DOSSIER_Code");
    }
}
