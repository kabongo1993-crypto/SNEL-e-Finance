using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class JournalAuditConfiguration : IEntityTypeConfiguration<JournalAudit>
{
    public void Configure(EntityTypeBuilder<JournalAudit> builder)
    {
        builder.ToTable("JOURNAL_AUDIT");

        builder.HasKey(e => e.IdAudit);

        builder.Property(e => e.IdAudit).HasColumnName("IdAudit");
        builder.Property(e => e.FK_Utilisateur).HasColumnName("FK_Utilisateur");
        builder.Property(e => e.DateHeure).HasColumnName("DateHeure");
        builder.Property(e => e.Operation).HasColumnName("Operation").HasMaxLength(50).IsUnicode(false);
        builder.Property(e => e.Entite).HasColumnName("Entite").HasMaxLength(100).IsUnicode(false);
        builder.Property(e => e.IdEntite).HasColumnName("IdEntite");
        builder.Property(e => e.AnciennesValeurs).HasColumnName("AnciennesValeurs");
        builder.Property(e => e.NouvellesValeurs).HasColumnName("NouvellesValeurs");
        builder.Property(e => e.AdresseIP).HasColumnName("AdresseIP").HasMaxLength(45).IsUnicode(false);

        builder.HasOne(e => e.Utilisateur)
            .WithMany(e => e.JournalAudits)
            .HasForeignKey(e => e.FK_Utilisateur)
            .HasConstraintName("FK_AUDIT_UTILISATEUR");
    }
}
