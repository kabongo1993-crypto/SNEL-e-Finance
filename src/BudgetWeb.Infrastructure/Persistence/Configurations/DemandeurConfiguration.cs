using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DemandeurConfiguration : IEntityTypeConfiguration<Demandeur>
{
    public void Configure(EntityTypeBuilder<Demandeur> builder)
    {
        builder.ToTable("DEMANDEUR", "dpm");
        builder.HasKey(e => e.IdDemandeur);
        builder.Property(e => e.IdDemandeur).HasColumnName("IdDemandeur").ValueGeneratedOnAdd();
        builder.Property(e => e.Code).HasColumnName("Code").HasMaxLength(60).IsUnicode(false).IsRequired();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FK_UniteBudgetaire).HasColumnName("FK_UniteBudgetaire");
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");
        builder.Property(e => e.FK_UtilisateurCreation).HasColumnName("FK_UtilisateurCreation");
        builder.Property(e => e.FK_UtilisateurModification).HasColumnName("FK_UtilisateurModification");

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_DPM_DEMANDEUR_Code");
        builder.HasIndex(e => new { e.FK_UniteBudgetaire, e.Actif }).HasDatabaseName("IX_DPM_DEMANDEUR_UB");

        builder.HasOne(e => e.UniteBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_UniteBudgetaire)
            .HasConstraintName("FK_DPM_DEMANDEUR_UB")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UtilisateurCreation)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurCreation)
            .HasConstraintName("FK_DPM_DEMANDEUR_USER_CREATION");
        builder.HasOne(e => e.UtilisateurModification)
            .WithMany()
            .HasForeignKey(e => e.FK_UtilisateurModification)
            .HasConstraintName("FK_DPM_DEMANDEUR_USER_MODIF");
    }
}
