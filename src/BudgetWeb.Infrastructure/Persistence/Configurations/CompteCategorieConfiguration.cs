using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class CompteCategorieConfiguration : IEntityTypeConfiguration<CompteCategorie>
{
    public void Configure(EntityTypeBuilder<CompteCategorie> builder)
    {
        builder.ToTable("COMPTE_CATEGORIE", "pct");
        builder.HasKey(e => e.IdCompteCategorie);
        builder.Property(e => e.IdCompteCategorie).HasColumnName("IdCompteCategorie").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_Compte).HasColumnName("FK_Compte");
        builder.Property(e => e.FK_CategorieCompte).HasColumnName("FK_CategorieCompte");
        builder.Property(e => e.DateDebut).HasColumnName("DateDebut");
        builder.Property(e => e.DateFin).HasColumnName("DateFin");

        builder.HasIndex(e => e.FK_Compte)
            .IsUnique()
            .HasFilter("[DateFin] IS NULL")
            .HasDatabaseName("UX_PCT_COMPTE_CATEGORIE_Compte_Actif");

        builder.HasIndex(e => new { e.FK_Compte, e.DateDebut, e.DateFin })
            .HasDatabaseName("IX_PCT_COMPTE_CATEGORIE_Compte_Dates");

        builder.HasOne(e => e.Compte)
            .WithMany(c => c.Categories)
            .HasForeignKey(e => e.FK_Compte)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PCT_COMPTE_CATEGORIE_Compte");

        builder.HasOne(e => e.CategorieCompte)
            .WithMany(c => c.ComptesCategories)
            .HasForeignKey(e => e.FK_CategorieCompte)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PCT_COMPTE_CATEGORIE_Categorie");
    }
}
