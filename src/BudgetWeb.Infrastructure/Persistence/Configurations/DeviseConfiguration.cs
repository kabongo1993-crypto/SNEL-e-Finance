using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DeviseConfiguration : IEntityTypeConfiguration<Devise>
{
    public void Configure(EntityTypeBuilder<Devise> builder)
    {
        builder.ToTable("DEVISE", "dpm");
        builder.HasKey(e => e.IdDevise);
        builder.Property(e => e.IdDevise).HasColumnName("IdDevise").ValueGeneratedOnAdd();
        builder.Property(e => e.Code).HasColumnName("Code").HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Symbole).HasColumnName("Symbole").HasMaxLength(10);
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_DPM_DEVISE_Code");
    }
}
