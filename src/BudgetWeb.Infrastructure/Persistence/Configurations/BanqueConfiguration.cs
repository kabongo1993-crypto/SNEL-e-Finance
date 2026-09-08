using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class BanqueConfiguration : IEntityTypeConfiguration<Banque>
{
    public void Configure(EntityTypeBuilder<Banque> builder)
    {
        builder.ToTable("BANQUE", "pct");
        builder.HasKey(e => e.IdBanque);
        builder.Property(e => e.IdBanque)
            .HasColumnName("IdBanque")
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired()
            .ValueGeneratedNever();
        builder.Property(e => e.LibelleBanque).HasColumnName("LibelleBanque").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Pays).HasColumnName("Pays").HasMaxLength(100);
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");

        builder.HasIndex(e => e.Actif).HasDatabaseName("IX_PCT_BANQUE_Actif");
    }
}
