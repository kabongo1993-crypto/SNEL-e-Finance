using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class PieceJointeConfiguration : IEntityTypeConfiguration<PieceJointe>
{
    public void Configure(EntityTypeBuilder<PieceJointe> builder)
    {
        builder.ToTable("PIECE_JOINTE", "dpm");
        builder.HasKey(e => e.IdPieceJointe);
        builder.Property(e => e.IdPieceJointe).HasColumnName("IdPieceJointe").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_DemandePaiement).HasColumnName("FK_DemandePaiement");
        builder.Property(e => e.FK_PieceObligatoire).HasColumnName("FK_PieceObligatoire");
        builder.Property(e => e.CodeTypePiece).HasColumnName("CodeTypePiece").HasMaxLength(60).IsUnicode(false).IsRequired();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(300).IsRequired();
        builder.Property(e => e.EstObligatoire).HasColumnName("EstObligatoire");
        builder.Property(e => e.NomFichierOriginal).HasColumnName("NomFichierOriginal").HasMaxLength(260).IsRequired();
        builder.Property(e => e.CheminRelatif).HasColumnName("CheminRelatif").HasMaxLength(500).IsRequired();
        builder.Property(e => e.HashSha256).HasColumnName("HashSha256").HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(e => e.TailleOctets).HasColumnName("TailleOctets");
        builder.Property(e => e.FK_Utilisateur).HasColumnName("FK_Utilisateur");
        builder.Property(e => e.DateUpload).HasColumnName("DateUpload");

        builder.HasIndex(e => e.FK_DemandePaiement).HasDatabaseName("IX_DPM_PJ_DEMANDE");
        builder.HasIndex(e => e.FK_PieceObligatoire).HasFilter("[FK_PieceObligatoire] IS NOT NULL").HasDatabaseName("IX_DPM_PJ_PIECE_OBLIGATOIRE");

        builder.HasOne(e => e.DemandePaiement).WithMany(d => d.PiecesJointes).HasForeignKey(e => e.FK_DemandePaiement).HasConstraintName("FK_DPM_PJ_DEMANDE");
        builder.HasOne(e => e.PieceObligatoire).WithMany(p => p.PiecesJointes).HasForeignKey(e => e.FK_PieceObligatoire).HasConstraintName("FK_DPM_PJ_PIECE_OBLIGATOIRE");
        builder.HasOne(e => e.Utilisateur).WithMany().HasForeignKey(e => e.FK_Utilisateur).HasConstraintName("FK_DPM_PJ_UTILISATEUR");
    }
}
