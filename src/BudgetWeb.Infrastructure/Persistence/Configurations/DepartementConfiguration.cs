using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DepartementConfiguration : IEntityTypeConfiguration<Departement>
{
    public void Configure(EntityTypeBuilder<Departement> builder)
    {
        builder.ToTable("DEPARTEMENT");

        builder.HasKey(e => e.IdDepartement);

        builder.Property(e => e.IdDepartement).HasColumnName("IdDepartement").ValueGeneratedOnAdd();
        builder.Property(e => e.Code).HasColumnName("Code").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200);
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
    }
}
