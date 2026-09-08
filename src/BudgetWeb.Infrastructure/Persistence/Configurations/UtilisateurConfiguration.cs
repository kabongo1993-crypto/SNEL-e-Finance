using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class UtilisateurConfiguration : IEntityTypeConfiguration<Utilisateur>
{
    public void Configure(EntityTypeBuilder<Utilisateur> builder)
    {
        builder.ToTable("UTILISATEUR");

        builder.HasKey(e => e.IdUtilisateur);

        builder.Property(e => e.IdUtilisateur).HasColumnName("IdUtilisateur");
        builder.Property(e => e.Matricule).HasColumnName("Matricule").HasMaxLength(50).IsUnicode(false);
        builder.Property(e => e.Nom).HasColumnName("Nom").HasMaxLength(100);
        builder.Property(e => e.Postnom).HasColumnName("Postnom").HasMaxLength(100);
        builder.Property(e => e.Prenom).HasColumnName("Prenom").HasMaxLength(100);
        builder.Property(e => e.NomUtilisateur).HasColumnName("NomUtilisateur").HasMaxLength(100).IsUnicode(false);
        builder.Property(e => e.MotDePasseHash).HasColumnName("MotDePasseHash").HasMaxLength(500).IsUnicode(false);
        builder.Property(e => e.Email).HasColumnName("Email").HasMaxLength(200).IsUnicode(false);
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.DateCreation).HasColumnName("DateCreation");
        builder.Property(e => e.DateDerniereConnexion).HasColumnName("DateDerniereConnexion");
        builder.Property(e => e.FK_StructureOrganisationnelle).HasColumnName("FK_StructureOrganisationnelle");
        builder.Property(e => e.FK_DepartementPrincipal).HasColumnName("FK_DepartementPrincipal");
        builder.Property(e => e.FK_StructureService).HasColumnName("FK_StructureService");

        // Aligné sur le script SQL OPTIONAL (FK sans ON DELETE → NO ACTION / Restrict).
        builder.HasOne(e => e.StructureOrganisationnelle)
            .WithMany()
            .HasForeignKey(e => e.FK_StructureOrganisationnelle)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UTILISATEUR_STRUCTURE");

        builder.HasOne(e => e.DepartementPrincipal)
            .WithMany()
            .HasForeignKey(e => e.FK_DepartementPrincipal)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UTILISATEUR_DEPT_PRINCIPAL");

        builder.HasOne(e => e.StructureService)
            .WithMany()
            .HasForeignKey(e => e.FK_StructureService)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UTILISATEUR_STRUCTURE_SERVICE");
    }
}
