using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class ExerciceBudgetaireConfiguration : IEntityTypeConfiguration<ExerciceBudgetaire>
{
    public void Configure(EntityTypeBuilder<ExerciceBudgetaire> builder)
    {
        builder.ToTable("EXERCICE_BUDGETAIRE");

        builder.HasKey(e => e.IdExercice);

        builder.Property(e => e.IdExercice).HasColumnName("IdExercice");
        builder.Property(e => e.Annee).HasColumnName("Annee");
        builder.Property(e => e.Statut).HasColumnName("Statut").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.DateOuverture).HasColumnName("DateOuverture");
        builder.Property(e => e.DateCloture).HasColumnName("DateCloture");
    }
}
