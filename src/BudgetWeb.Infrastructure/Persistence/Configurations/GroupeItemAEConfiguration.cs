using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class GroupeItemAEConfiguration : IEntityTypeConfiguration<GroupeItemAE>
{
    public void Configure(EntityTypeBuilder<GroupeItemAE> builder)
    {
        builder.ToTable("GROUPE_ITEM_AE");

        builder.HasKey(e => e.IdGroupeItemAE);

        builder.Property(e => e.IdGroupeItemAE).HasColumnName("IdGroupeItemAE");
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200);
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
    }
}
