using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class GroupeTypeCompteConfiguration : IEntityTypeConfiguration<GroupeTypeCompte>
{
    public void Configure(EntityTypeBuilder<GroupeTypeCompte> builder)
    {
        builder.ToTable("GROUPE_TYPE_COMPTE", "pct");
        builder.HasKey(e => e.IdGroupeTypeCompte);
        builder.Property(e => e.IdGroupeTypeCompte)
            .HasColumnName("IdGroupeTypeCompte")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd()
            .UseIdentityColumn();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");

        builder.HasIndex(e => e.Libelle).IsUnique().HasDatabaseName("UX_PCT_GROUPE_TYPE_COMPTE_Libelle");
    }
}
