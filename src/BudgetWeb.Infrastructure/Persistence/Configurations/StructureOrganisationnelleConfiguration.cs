using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class StructureOrganisationnelleConfiguration : IEntityTypeConfiguration<StructureOrganisationnelle>
{
    public void Configure(EntityTypeBuilder<StructureOrganisationnelle> builder)
    {
        builder.ToTable("STRUCTURE_ORGANISATIONNELLE");

        builder.HasKey(e => e.IdStructure);

        builder.Property(e => e.IdStructure).HasColumnName("IdStructure");
        builder.Property(e => e.FK_StructureOrganisationnelleParent).HasColumnName("FK_StructureOrganisationnelleParent");
        builder.Property(e => e.TypeStructure).HasColumnName("TypeStructure").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.Code).HasColumnName("Code").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200);
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");

        builder.HasOne(e => e.StructureParent)
            .WithMany(e => e.StructuresEnfants)
            .HasForeignKey(e => e.FK_StructureOrganisationnelleParent)
            .HasConstraintName("FK_STRUCTURE_PARENT");
    }
}
