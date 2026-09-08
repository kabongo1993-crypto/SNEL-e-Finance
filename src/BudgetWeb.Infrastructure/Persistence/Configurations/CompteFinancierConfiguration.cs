using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class CompteFinancierConfiguration : IEntityTypeConfiguration<CompteFinancier>
{
    public void Configure(EntityTypeBuilder<CompteFinancier> builder)
    {
        builder.ToTable("COMPTE", "pct");
        builder.HasKey(e => e.IdCompte);
        builder.Property(e => e.IdCompte)
            .HasColumnName("IdCompte")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd()
            .UseIdentityColumn();
        builder.Property(e => e.NumeroCompte).HasColumnName("NumeroCompte").HasMaxLength(50).IsRequired();
        builder.Property(e => e.LibelleCompte).HasColumnName("LibelleCompte").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FK_Banque)
            .HasColumnName("FK_Banque")
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(e => e.FK_Direction)
            .HasColumnName("FK_Direction")
            .HasColumnType("bigint")
            .IsRequired();
        builder.Property(e => e.FK_TypeCompte)
            .HasColumnName("FK_TypeCompte")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(e => e.FK_Devise).HasColumnName("FK_Devise");
        builder.Property(e => e.FK_Province)
            .HasColumnName("FK_Province")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired(false);
        builder.Property(e => e.FK_Utilisateur).HasColumnName("FK_Utilisateur");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateCloture).HasColumnName("DateCloture");
        builder.Property(e => e.DateModification).HasColumnName("DateModification");
        builder.Property(e => e.Actif).HasColumnName("Actif");

        builder.HasIndex(e => new { e.FK_Banque, e.NumeroCompte })
            .IsUnique()
            .HasDatabaseName("UX_PCT_COMPTE_Banque_NumeroCompte");
        builder.HasIndex(e => e.FK_Direction).HasDatabaseName("IX_PCT_COMPTE_FK_Direction");
        builder.HasIndex(e => e.FK_TypeCompte).HasDatabaseName("IX_PCT_COMPTE_FK_TypeCompte");
        builder.HasIndex(e => e.FK_Devise).HasDatabaseName("IX_PCT_COMPTE_FK_Devise");
        builder.HasIndex(e => e.FK_Province).HasDatabaseName("IX_PCT_COMPTE_FK_Province");
        builder.HasIndex(e => e.FK_Utilisateur).HasDatabaseName("IX_PCT_COMPTE_FK_Utilisateur");
        builder.HasIndex(e => e.Actif).HasDatabaseName("IX_PCT_COMPTE_Actif");

        builder.HasOne(e => e.Banque)
            .WithMany(b => b.Comptes)
            .HasForeignKey(e => e.FK_Banque)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PCT_COMPTE_Banque");

        builder.HasOne(e => e.Direction)
            .WithMany(d => d.Comptes)
            .HasForeignKey(e => e.FK_Direction)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PCT_COMPTE_Direction");

        builder.HasOne(e => e.TypeCompte)
            .WithMany(t => t.Comptes)
            .HasForeignKey(e => e.FK_TypeCompte)
            .HasPrincipalKey(e => e.Code)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PCT_COMPTE_TypeCompte");

        builder.HasOne(e => e.Devise)
            .WithMany()
            .HasForeignKey(e => e.FK_Devise)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PCT_COMPTE_Devise");

        builder.HasOne(e => e.Province)
            .WithMany(p => p.Comptes)
            .HasForeignKey(e => e.FK_Province)
            .HasPrincipalKey(e => e.IdProvince)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PCT_COMPTE_Province");

        builder.HasOne(e => e.Utilisateur)
            .WithMany()
            .HasForeignKey(e => e.FK_Utilisateur)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PCT_COMPTE_Utilisateur");
    }
}
