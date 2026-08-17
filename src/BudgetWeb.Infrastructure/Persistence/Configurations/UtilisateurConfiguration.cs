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
    }
}
