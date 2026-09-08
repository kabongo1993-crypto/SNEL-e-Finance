using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class UniteBudgetaireConfiguration : IEntityTypeConfiguration<UniteBudgetaire>
{
    public void Configure(EntityTypeBuilder<UniteBudgetaire> builder)
    {
        builder.ToTable("UNITE_BUDGETAIRE");

        builder.HasKey(e => e.IdUB);

        builder.Property(e => e.IdUB).HasColumnName("IdUB").ValueGeneratedOnAdd();
        builder.Property(e => e.CodeUB).HasColumnName("CodeUB").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200);
        builder.Property(e => e.FK_Departement).HasColumnName("FK_Departement");
        builder.Property(e => e.FK_StructureOrganisationnelle).HasColumnName("FK_StructureOrganisationnelle");
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");

        builder.HasOne(e => e.Departement)
            .WithMany(e => e.UnitesBudgetaires)
            .HasForeignKey(e => e.FK_Departement)
            .HasConstraintName("FK_UB_DEPARTEMENT");

        builder.HasOne(e => e.StructureOrganisationnelle)
            .WithMany(e => e.UnitesBudgetaires)
            .HasForeignKey(e => e.FK_StructureOrganisationnelle)
            .HasConstraintName("FK_UB_STRUCTURE");
    }
}
