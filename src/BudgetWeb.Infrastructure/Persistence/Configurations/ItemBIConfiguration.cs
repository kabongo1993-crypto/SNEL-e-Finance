using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class ItemBIConfiguration : IEntityTypeConfiguration<ItemBI>
{
    public void Configure(EntityTypeBuilder<ItemBI> builder)
    {
        builder.ToTable("ITEM_BI");

        builder.HasKey(e => e.IdItemBI);

        builder.Property(e => e.IdItemBI).HasColumnName("IdItemBI");
        builder.Property(e => e.CodeItem).HasColumnName("CodeItem").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(300);
        builder.Property(e => e.FK_ItemBIParent).HasColumnName("FK_ItemBIParent");
        builder.Property(e => e.Niveau).HasColumnName("Niveau");
        builder.Property(e => e.Categorie).HasColumnName("Categorie").HasMaxLength(200);
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");

        builder.HasOne(e => e.ItemParent)
            .WithMany(e => e.ItemsEnfants)
            .HasForeignKey(e => e.FK_ItemBIParent)
            .HasConstraintName("FK_ITEM_BI_PARENT");
    }
}
