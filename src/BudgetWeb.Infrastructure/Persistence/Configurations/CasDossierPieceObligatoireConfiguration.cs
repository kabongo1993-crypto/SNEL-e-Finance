using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class CasDossierPieceObligatoireConfiguration : IEntityTypeConfiguration<CasDossierPieceObligatoire>
{
    public void Configure(EntityTypeBuilder<CasDossierPieceObligatoire> builder)
    {
        builder.ToTable("CAS_DOSSIER_PIECE_OBLIGATOIRE", "dpm");
        builder.HasKey(e => e.IdPieceObligatoire);
        builder.Property(e => e.IdPieceObligatoire).HasColumnName("IdPieceObligatoire").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_CasDossier).HasColumnName("FK_CasDossier");
        builder.Property(e => e.CodeTypePiece).HasColumnName("CodeTypePiece").HasMaxLength(60).IsUnicode(false).IsRequired();
        builder.Property(e => e.Libelle).HasColumnName("Libelle").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Ordre).HasColumnName("Ordre");
        builder.Property(e => e.Actif).HasColumnName("Actif");
        builder.Property(e => e.Obligatoire).HasColumnName("Obligatoire");
        builder.HasIndex(e => new { e.FK_CasDossier, e.CodeTypePiece }).IsUnique().HasDatabaseName("UX_DPM_CAS_PIECE_Cas_Code");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_DPM_CAS_PIECE_Obligatoire_Implique_Actif",
                "Obligatoire = 0 OR Actif = 1");
        });
        builder.HasOne(e => e.CasDossier).WithMany(c => c.PiecesObligatoires).HasForeignKey(e => e.FK_CasDossier).HasConstraintName("FK_DPM_CAS_PIECE_CAS_DOSSIER");
    }
}
