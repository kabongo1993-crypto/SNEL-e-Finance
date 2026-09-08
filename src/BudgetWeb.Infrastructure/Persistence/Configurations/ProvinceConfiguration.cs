using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable("PROVINCE", "pct");
        builder.HasKey(e => e.IdProvince);
        builder.Property(e => e.IdProvince)
            .HasColumnName("IdProvince")
            .HasMaxLength(20)
            .IsUnicode(false)
            .ValueGeneratedNever()
            .IsRequired();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Actif).HasColumnName("Actif").IsRequired();
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation").HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.DateModification).HasColumnName("DateModification").HasColumnType("datetime2");

        builder.HasIndex(e => e.Libelle).IsUnique().HasDatabaseName("UX_PCT_PROVINCE_Libelle");
    }
}
