using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class VPrevisionsConfiguration : IEntityTypeConfiguration<VPrevisions>
{
    public void Configure(EntityTypeBuilder<VPrevisions> builder)
    {
        builder.ToView("V_PREVISIONS");
        builder.HasNoKey();

        builder.Property(e => e.IdPrevision).HasColumnName("IdPrevision");
        builder.Property(e => e.Annee).HasColumnName("Annee");
        builder.Property(e => e.IdVersion).HasColumnName("IdVersion");
        builder.Property(e => e.NumeroVersion).HasColumnName("NumeroVersion");
        builder.Property(e => e.LibelleVersion).HasColumnName("LibelleVersion").HasMaxLength(200);
        builder.Property(e => e.StatutVersion).HasColumnName("StatutVersion").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.CodeType).HasColumnName("CodeType").HasMaxLength(10).IsUnicode(false);
        builder.Property(e => e.TypeBudget).HasColumnName("TypeBudget").HasMaxLength(100);
        builder.Property(e => e.CodeUB).HasColumnName("CodeUB").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.LibelleUB).HasColumnName("LibelleUB").HasMaxLength(200);
        builder.Property(e => e.CodeRB).HasColumnName("CodeRB").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.LibelleRB).HasColumnName("LibelleRB").HasMaxLength(300);
        builder.Property(e => e.CodeItemBI).HasColumnName("CodeItemBI").HasMaxLength(30).IsUnicode(false);
        builder.Property(e => e.LibelleItemBI).HasColumnName("LibelleItemBI").HasMaxLength(300);
        builder.Property(e => e.GroupeItemAE).HasColumnName("GroupeItemAE").HasMaxLength(200);
        builder.Property(e => e.LibelleItemAE).HasColumnName("LibelleItemAE").HasMaxLength(500);
        builder.Property(e => e.DetailBI).HasColumnName("DetailBI").HasMaxLength(1000);
        builder.Property(e => e.CodeMode).HasColumnName("CodeMode").HasMaxLength(20).IsUnicode(false);
        builder.Property(e => e.ModePrevision).HasColumnName("ModePrevision").HasMaxLength(100);
        builder.Property(e => e.MontantAnnuel).HasColumnName("MontantAnnuel").HasColumnType("decimal(19,4)");
        builder.Property(e => e.MontantVentile).HasColumnName("MontantVentile").HasColumnType("decimal(38,4)");
        builder.Property(e => e.MontantNonVentile).HasColumnName("MontantNonVentile").HasColumnType("decimal(38,4)");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");
    }
}
