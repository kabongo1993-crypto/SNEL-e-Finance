using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class PermissionUtilisateurConfiguration : IEntityTypeConfiguration<PermissionUtilisateur>
{
    public void Configure(EntityTypeBuilder<PermissionUtilisateur> builder)
    {
        builder.ToTable("PERMISSION_UTILISATEUR", "dpm");
        builder.HasKey(e => e.IdPermissionUtilisateur);
        builder.Property(e => e.IdPermissionUtilisateur).HasColumnName("IdPermissionUtilisateur").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_Utilisateur).HasColumnName("FK_Utilisateur");
        builder.Property(e => e.CodePermission).HasColumnName("CodePermission").HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(e => e.DateAttribution).HasColumnName("DateAttribution");
        builder.HasIndex(e => new { e.FK_Utilisateur, e.CodePermission })
            .IsUnique()
            .HasDatabaseName("UX_DPM_PERM_USER_CODE");
        builder.HasOne(e => e.Utilisateur)
            .WithMany(u => u.PermissionsIndividuelles)
            .HasForeignKey(e => e.FK_Utilisateur)
            .HasConstraintName("FK_DPM_PERM_UTILISATEUR");
    }
}

public class PerimetreUtilisateurConfiguration : IEntityTypeConfiguration<PerimetreUtilisateur>
{
    public void Configure(EntityTypeBuilder<PerimetreUtilisateur> builder)
    {
        builder.ToTable("PERIMETRE_UTILISATEUR", "dpm");
        builder.HasKey(e => e.IdPerimetreUtilisateur);
        builder.Property(e => e.IdPerimetreUtilisateur).HasColumnName("IdPerimetreUtilisateur").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_Utilisateur).HasColumnName("FK_Utilisateur");
        builder.Property(e => e.TousDepartements).HasColumnName("TousDepartements");
        builder.Property(e => e.ToutesUnitesBudgetaires).HasColumnName("ToutesUnitesBudgetaires");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");
        builder.HasIndex(e => e.FK_Utilisateur).IsUnique().HasDatabaseName("UX_DPM_PERIMETRE_USER");
        builder.HasOne(e => e.Utilisateur)
            .WithOne(u => u.Perimetre)
            .HasForeignKey<PerimetreUtilisateur>(e => e.FK_Utilisateur)
            .HasConstraintName("FK_DPM_PERIMETRE_UTILISATEUR");
    }
}

public class PerimetreDepartementConfiguration : IEntityTypeConfiguration<PerimetreDepartement>
{
    public void Configure(EntityTypeBuilder<PerimetreDepartement> builder)
    {
        builder.ToTable("PERIMETRE_DEPARTEMENT", "dpm");
        builder.HasKey(e => e.IdPerimetreDepartement);
        builder.Property(e => e.IdPerimetreDepartement).HasColumnName("IdPerimetreDepartement").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_PerimetreUtilisateur).HasColumnName("FK_PerimetreUtilisateur");
        builder.Property(e => e.FK_Departement).HasColumnName("FK_Departement");
        builder.HasIndex(e => new { e.FK_PerimetreUtilisateur, e.FK_Departement })
            .IsUnique()
            .HasDatabaseName("UX_DPM_PERIMETRE_DEPT");
        builder.HasOne(e => e.Perimetre)
            .WithMany(p => p.Departements)
            .HasForeignKey(e => e.FK_PerimetreUtilisateur)
            .HasConstraintName("FK_DPM_PERIMETRE_DEPT_HDR");
        builder.HasOne(e => e.Departement)
            .WithMany()
            .HasForeignKey(e => e.FK_Departement)
            .HasConstraintName("FK_DPM_PERIMETRE_DEPT_REF");
    }
}

public class PerimetreUniteBudgetaireConfiguration : IEntityTypeConfiguration<PerimetreUniteBudgetaire>
{
    public void Configure(EntityTypeBuilder<PerimetreUniteBudgetaire> builder)
    {
        builder.ToTable("PERIMETRE_UB", "dpm");
        builder.HasKey(e => e.IdPerimetreUniteBudgetaire);
        builder.Property(e => e.IdPerimetreUniteBudgetaire).HasColumnName("IdPerimetreUniteBudgetaire").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_PerimetreUtilisateur).HasColumnName("FK_PerimetreUtilisateur");
        builder.Property(e => e.FK_UniteBudgetaire).HasColumnName("FK_UniteBudgetaire");
        builder.HasIndex(e => new { e.FK_PerimetreUtilisateur, e.FK_UniteBudgetaire })
            .IsUnique()
            .HasDatabaseName("UX_DPM_PERIMETRE_UB");
        builder.HasOne(e => e.Perimetre)
            .WithMany(p => p.UnitesBudgetaires)
            .HasForeignKey(e => e.FK_PerimetreUtilisateur)
            .HasConstraintName("FK_DPM_PERIMETRE_UB_HDR");
        builder.HasOne(e => e.UniteBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_UniteBudgetaire)
            .HasConstraintName("FK_DPM_PERIMETRE_UB_REF");
    }
}
