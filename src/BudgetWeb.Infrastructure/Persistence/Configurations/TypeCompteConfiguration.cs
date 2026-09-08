using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class TypeCompteConfiguration : IEntityTypeConfiguration<TypeCompte>
{
    public void Configure(EntityTypeBuilder<TypeCompte> builder)
    {
        builder.ToTable("TYPE_COMPTE", "pct");
        builder.HasKey(e => e.Code);
        builder.Property(e => e.Code)
            .HasColumnName("Code")
            .HasMaxLength(20)
            .IsUnicode(false)
            .ValueGeneratedNever()
            .IsRequired();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FK_GroupeTypeCompte).HasColumnName("FK_GroupeTypeCompte");
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");

        builder.HasIndex(e => e.FK_GroupeTypeCompte).HasDatabaseName("IX_PCT_TYPE_COMPTE_FK_GroupeTypeCompte");

        builder.HasOne(e => e.GroupeTypeCompte)
            .WithMany(g => g.TypesCompte)
            .HasForeignKey(e => e.FK_GroupeTypeCompte)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PCT_TYPE_COMPTE_Groupe");
    }
}
