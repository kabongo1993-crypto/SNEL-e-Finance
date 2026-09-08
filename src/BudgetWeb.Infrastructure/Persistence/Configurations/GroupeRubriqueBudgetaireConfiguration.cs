using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class GroupeRubriqueBudgetaireConfiguration : IEntityTypeConfiguration<GroupeRubriqueBudgetaire>
{
    public void Configure(EntityTypeBuilder<GroupeRubriqueBudgetaire> builder)
    {
        builder.ToTable("GROUPE_RUBRIQUE_BUDGETAIRE");

        builder.HasKey(e => e.IdGroupeRB);

        builder.Property(e => e.IdGroupeRB).HasColumnName("IdGroupeRB").ValueGeneratedOnAdd();
        builder.Property(e => e.CodeGroupe).HasColumnName("CodeGroupe").HasMaxLength(10).IsUnicode(false);
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200);
        builder.Property(e => e.OrdreAffichage).HasColumnName("OrdreAffichage");
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
    }
}
