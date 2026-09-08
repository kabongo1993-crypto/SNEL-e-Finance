using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class RepartitionMensuelleConfiguration : IEntityTypeConfiguration<RepartitionMensuelle>
{
    public void Configure(EntityTypeBuilder<RepartitionMensuelle> builder)
    {
        builder.ToTable("REPARTITION_MENSUELLE", t =>
        {
            t.HasTrigger("TR_REPARTITION_RECALCUL_MONTANT");
        });

        builder.HasKey(e => e.IdRepartition);

        builder.Property(e => e.IdRepartition).HasColumnName("IdRepartition").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_PrevisionBudgetaire).HasColumnName("FK_PrevisionBudgetaire");
        builder.Property(e => e.Mois).HasColumnName("Mois");
        builder.Property(e => e.Montant).HasColumnName("Montant").HasColumnType("decimal(19,4)");

        builder.HasOne(e => e.PrevisionBudgetaire)
            .WithMany(e => e.RepartitionsMensuelles)
            .HasForeignKey(e => e.FK_PrevisionBudgetaire)
            .HasConstraintName("FK_REPARTITION_PREVISION");
    }
}
