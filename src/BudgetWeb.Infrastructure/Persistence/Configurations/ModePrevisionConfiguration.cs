using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class ModePrevisionConfiguration : IEntityTypeConfiguration<ModePrevision>
{
    public void Configure(EntityTypeBuilder<ModePrevision> builder)
    {
        builder.ToTable("MODE_PREVISION");

        builder.HasKey(e => e.IdModePrevision);

        builder.Property(e => e.IdModePrevision).HasColumnName("IdModePrevision");
        builder.Property(e => e.CodeMode).HasColumnName("CodeMode").HasMaxLength(20).IsUnicode(false);
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(100);
        builder.Property(e => e.Actif).HasColumnName("Actif");
    }
}
