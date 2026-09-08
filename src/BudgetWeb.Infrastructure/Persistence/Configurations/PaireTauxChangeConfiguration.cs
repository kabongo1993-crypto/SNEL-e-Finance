using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class PaireTauxChangeConfiguration : IEntityTypeConfiguration<PaireTauxChange>
{
    public void Configure(EntityTypeBuilder<PaireTauxChange> builder)
    {
        builder.ToTable("PAIRE_TAUX_CHANGE", "dpm");
        builder.HasKey(e => e.IdPaireTauxChange);
        builder.Property(e => e.IdPaireTauxChange).HasColumnName("IdPaireTauxChange").ValueGeneratedOnAdd();
        builder.Property(e => e.DeviseBase).HasColumnName("DeviseBase").HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.DeviseQuote).HasColumnName("DeviseQuote").HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.Actif).HasColumnName("Actif").HasDefaultValue(true);
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => new { e.DeviseBase, e.DeviseQuote })
            .IsUnique()
            .HasDatabaseName("UX_DPM_PAIRE_TAUX_DeviseBase_Quote");
    }
}
