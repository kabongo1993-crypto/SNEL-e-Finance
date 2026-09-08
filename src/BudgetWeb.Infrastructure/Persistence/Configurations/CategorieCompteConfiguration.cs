using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class CategorieCompteConfiguration : IEntityTypeConfiguration<CategorieCompte>
{
    public void Configure(EntityTypeBuilder<CategorieCompte> builder)
    {
        builder.ToTable("CATEGORIE_COMPTE", "pct");
        builder.HasKey(e => e.IdCategorieCompte);
        builder.Property(e => e.IdCategorieCompte)
            .HasColumnName("IdCategorieCompte")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd()
            .UseIdentityColumn();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Orientation).HasColumnName("Orientation").HasMaxLength(200).IsRequired(false);
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");

        builder.HasIndex(e => e.Libelle).IsUnique().HasDatabaseName("UX_PCT_CATEGORIE_COMPTE_Libelle");
        builder.HasIndex(e => e.Orientation).HasDatabaseName("IX_PCT_CATEGORIE_COMPTE_Orientation");
    }
}
