using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DirectionTresorerieConfiguration : IEntityTypeConfiguration<DirectionTresorerie>
{
    public void Configure(EntityTypeBuilder<DirectionTresorerie> builder)
    {
        builder.ToTable("DIRECTION", "pct");
        builder.HasKey(e => e.IdDirection);
        builder.Property(e => e.IdDirection)
            .HasColumnName("IdDirection")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd()
            .UseIdentityColumn();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Actif).HasColumnName("Actif").IsRequired();
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation").HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.DateModification).HasColumnName("DateModification").HasColumnType("datetime2");

        builder.HasIndex(e => e.Libelle).IsUnique().HasDatabaseName("UX_PCT_DIRECTION_Libelle");
    }
}
