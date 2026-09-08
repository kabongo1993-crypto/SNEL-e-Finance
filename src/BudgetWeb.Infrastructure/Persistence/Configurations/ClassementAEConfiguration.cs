using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class ClassementAEConfiguration : IEntityTypeConfiguration<ClassementAE>
{
    public void Configure(EntityTypeBuilder<ClassementAE> builder)
    {
        builder.ToTable("CLASSEMENT_AE", t =>
        {
            t.HasCheckConstraint(
                "CK_CLASSEMENT_AE_TypeLigne",
                "[TypeLigne] IN ('GROUPE', 'ITEM')");
            t.HasCheckConstraint(
                "CK_CLASSEMENT_AE_Discriminant",
                "([TypeLigne] = 'GROUPE' AND [FK_GroupeItemAE] IS NOT NULL AND [LibelleItemAE] IS NULL) "
                + "OR ([TypeLigne] = 'ITEM' AND [LibelleItemAE] IS NOT NULL AND [FK_GroupeItemAE] IS NULL)");
            t.HasCheckConstraint(
                "CK_CLASSEMENT_AE_Ordre",
                "[OrdreAffichage] >= 1");
        });

        builder.HasKey(e => e.IdClassementAE);
        builder.Property(e => e.IdClassementAE).HasColumnName("IdClassementAE").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_VersionBudgetaire).HasColumnName("FK_VersionBudgetaire");
        builder.Property(e => e.FK_UniteBudgetaire).HasColumnName("FK_UniteBudgetaire");
        builder.Property(e => e.TypeLigne).HasColumnName("TypeLigne").HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(e => e.FK_GroupeItemAE).HasColumnName("FK_GroupeItemAE");
        builder.Property(e => e.LibelleItemAE).HasColumnName("LibelleItemAE").HasMaxLength(500);
        builder.Property(e => e.OrdreAffichage).HasColumnName("OrdreAffichage");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");

        builder.HasIndex(e => new { e.FK_VersionBudgetaire, e.FK_UniteBudgetaire, e.FK_GroupeItemAE })
            .IsUnique()
            .HasFilter("[TypeLigne] = 'GROUPE' AND [FK_GroupeItemAE] IS NOT NULL")
            .HasDatabaseName("UX_CLASSEMENT_AE_VERSION_UB_GROUPE");

        builder.HasIndex(e => new { e.FK_VersionBudgetaire, e.FK_UniteBudgetaire, e.LibelleItemAE })
            .IsUnique()
            .HasFilter("[TypeLigne] = 'ITEM' AND [LibelleItemAE] IS NOT NULL")
            .HasDatabaseName("UX_CLASSEMENT_AE_VERSION_UB_ITEM");

        builder.HasIndex(e => new { e.FK_VersionBudgetaire, e.FK_UniteBudgetaire, e.OrdreAffichage })
            .IsUnique()
            .HasDatabaseName("UX_CLASSEMENT_AE_VERSION_UB_ORDRE");

        builder.HasOne(e => e.VersionBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_VersionBudgetaire)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CLASSEMENT_AE_VERSION");

        builder.HasOne(e => e.UniteBudgetaire)
            .WithMany()
            .HasForeignKey(e => e.FK_UniteBudgetaire)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CLASSEMENT_AE_UB");

        builder.HasOne(e => e.GroupeItemAE)
            .WithMany()
            .HasForeignKey(e => e.FK_GroupeItemAE)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CLASSEMENT_AE_GROUPE");
    }
}
