using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class ProfilUtilisateurConfiguration : IEntityTypeConfiguration<ProfilUtilisateur>
{
    public void Configure(EntityTypeBuilder<ProfilUtilisateur> builder)
    {
        builder.ToTable("PROFIL_UTILISATEUR", "dpm");
        builder.HasKey(e => e.IdProfilUtilisateur);
        builder.Property(e => e.IdProfilUtilisateur).HasColumnName("IdProfilUtilisateur").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_Utilisateur).HasColumnName("FK_Utilisateur");
        builder.Property(e => e.CodeProfil).HasColumnName("CodeProfil").HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.HasIndex(e => new { e.FK_Utilisateur, e.CodeProfil }).IsUnique().HasDatabaseName("UX_DPM_PROFIL_USER_CODE");
        builder.HasOne(e => e.Utilisateur)
            .WithMany(u => u.Profils)
            .HasForeignKey(e => e.FK_Utilisateur)
            .HasConstraintName("FK_DPM_PROFIL_UTILISATEUR");
    }
}
