using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class TauxChangeConfiguration : IEntityTypeConfiguration<TauxChange>
{
    public void Configure(EntityTypeBuilder<TauxChange> builder)
    {
        builder.ToTable("TAUX_CHANGE", "dpm");
        builder.HasKey(e => e.IdTauxChange);
        builder.Property(e => e.IdTauxChange).HasColumnName("IdTauxChange").ValueGeneratedOnAdd();
        builder.Property(e => e.DeviseSource).HasColumnName("DeviseSource").HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.DeviseCible).HasColumnName("DeviseCible").HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.Taux).HasColumnName("Taux").HasColumnType("decimal(19,8)");
        builder.Property(e => e.DateEffet).HasColumnName("DateEffet");
        builder.Property(e => e.Statut).HasColumnName("Statut").HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");
        builder.HasIndex(e => new { e.DeviseSource, e.DeviseCible, e.DateEffet, e.Statut }).HasDatabaseName("IX_DPM_TAUX_Devise_Date");
        builder.HasIndex(e => new { e.DeviseSource, e.DeviseCible, e.DateEffet }).IsUnique().HasFilter("[Statut] = 'ACTIF'").HasDatabaseName("UX_DPM_TAUX_ACTIF_Devise_Date");
        builder.HasOne(e => e.UtilisateurCreation).WithMany().HasForeignKey(e => e.FK_UtilisateurCreation).HasConstraintName("FK_DPM_TAUX_USER_CREATION");
        builder.HasOne(e => e.UtilisateurModification).WithMany().HasForeignKey(e => e.FK_UtilisateurModification).HasConstraintName("FK_DPM_TAUX_USER_MODIF");
    }
}
