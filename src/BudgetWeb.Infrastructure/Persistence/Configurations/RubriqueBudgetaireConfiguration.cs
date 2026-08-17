using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class RubriqueBudgetaireConfiguration : IEntityTypeConfiguration<RubriqueBudgetaire>
{
    public void Configure(EntityTypeBuilder<RubriqueBudgetaire> builder)
    {
        builder.ToTable("RUBRIQUE_BUDGETAIRE");

        builder.HasKey(e => e.IdRB);

        builder.Property(e => e.IdRB).HasColumnName("IdRB");
        builder.Property(e => e.CodeRB).HasColumnName("CodeRB").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(300);
        builder.Property(e => e.FK_RubriqueBudgetaireParent).HasColumnName("FK_RubriqueBudgetaireParent");
        builder.Property(e => e.Niveau).HasColumnName("Niveau");
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");

        builder.HasOne(e => e.RubriqueParent)
            .WithMany(e => e.RubriquesEnfants)
            .HasForeignKey(e => e.FK_RubriqueBudgetaireParent)
            .HasConstraintName("FK_RB_PARENT");
    }
}
