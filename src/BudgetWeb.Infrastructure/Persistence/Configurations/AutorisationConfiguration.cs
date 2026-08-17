using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class AutorisationConfiguration : IEntityTypeConfiguration<Autorisation>
{
    public void Configure(EntityTypeBuilder<Autorisation> builder)
    {
        builder.ToTable("AUTORISATION");

        builder.HasKey(e => e.IdAutorisation);

        builder.Property(e => e.IdAutorisation).HasColumnName("IdAutorisation");
        builder.Property(e => e.TypeOperation).HasColumnName("TypeOperation").HasMaxLength(50).IsUnicode(false);
        builder.Property(e => e.DateAutorisation).HasColumnName("DateAutorisation");
        builder.Property(e => e.FK_UtilisateurAutorisation).HasColumnName("FK_UtilisateurAutorisation");
        builder.Property(e => e.Motif).HasColumnName("Motif").HasMaxLength(1000);
        builder.Property(e => e.Statut).HasColumnName("Statut").HasMaxLength(30).IsUnicode(false);

        builder.HasOne(e => e.UtilisateurAutorisation)
            .WithMany(e => e.Autorisations)
            .HasForeignKey(e => e.FK_UtilisateurAutorisation)
            .HasConstraintName("FK_AUTORISATION_UTILISATEUR");
    }
}
