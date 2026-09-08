using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class TypeBudgetConfiguration : IEntityTypeConfiguration<TypeBudget>
{
    public void Configure(EntityTypeBuilder<TypeBudget> builder)
    {
        builder.ToTable("TYPE_BUDGET");

        builder.HasKey(e => e.IdTypeBudget);

        builder.Property(e => e.IdTypeBudget).HasColumnName("IdTypeBudget").ValueGeneratedOnAdd();
        builder.Property(e => e.CodeType).HasColumnName("CodeType").HasMaxLength(10).IsUnicode(false);
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(100);
        builder.Property(e => e.OrdreAffichage).HasColumnName("OrdreAffichage");
        builder.Property(e => e.Actif).HasColumnName("Actif");
    }
}
