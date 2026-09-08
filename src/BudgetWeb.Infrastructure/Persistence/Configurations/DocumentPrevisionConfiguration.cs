using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DocumentPrevisionConfiguration : IEntityTypeConfiguration<DocumentPrevision>
{
    public void Configure(EntityTypeBuilder<DocumentPrevision> builder)
    {
        builder.ToTable("DOCUMENT_PREVISION");
        builder.HasKey(e => e.IdDocument);
        builder.Property(e => e.IdDocument).HasColumnName("IdDocument").ValueGeneratedOnAdd();
        builder.Property(e => e.Reference).HasColumnName("Reference").HasMaxLength(120).IsRequired();
        builder.Property(e => e.TypeDocument).HasColumnName("TypeDocument").HasMaxLength(10).IsRequired();
        builder.Property(e => e.IdAudit).HasColumnName("IdAudit");
        builder.Property(e => e.IdVersion).HasColumnName("IdVersion");
        builder.Property(e => e.AnneeExercice).HasColumnName("AnneeExercice");
        builder.Property(e => e.NumeroVersion).HasColumnName("NumeroVersion");
        builder.Property(e => e.IdDepartement).HasColumnName("IdDepartement");
        builder.Property(e => e.CodeDepartement).HasColumnName("CodeDepartement").HasMaxLength(30);
        builder.Property(e => e.LibelleDepartement).HasColumnName("LibelleDepartement").HasMaxLength(200);
        builder.Property(e => e.IdUB).HasColumnName("IdUB");
        builder.Property(e => e.CodeUB).HasColumnName("CodeUB").HasMaxLength(50);
        builder.Property(e => e.LibelleUB).HasColumnName("LibelleUB").HasMaxLength(250);
        builder.Property(e => e.Portee).HasColumnName("Portee").HasMaxLength(20);
        builder.Property(e => e.NbUbConcernees).HasColumnName("NbUbConcernees");
        builder.Property(e => e.IdUtilisateurAuteur).HasColumnName("IdUtilisateurAuteur");
        builder.Property(e => e.NomUtilisateurAuteur).HasColumnName("NomUtilisateurAuteur").HasMaxLength(200);
        builder.Property(e => e.DateEvenement).HasColumnName("DateEvenement");
        builder.Property(e => e.StatutAvant).HasColumnName("StatutAvant").HasMaxLength(30);
        builder.Property(e => e.StatutApres).HasColumnName("StatutApres").HasMaxLength(30);
        builder.Property(e => e.Motif).HasColumnName("Motif").HasMaxLength(1000);
        builder.Property(e => e.MontantDC).HasColumnName("MontantDC").HasPrecision(18, 4);
        builder.Property(e => e.MontantAE).HasColumnName("MontantAE").HasPrecision(18, 4);
        builder.Property(e => e.MontantBI).HasColumnName("MontantBI").HasPrecision(18, 4);
        builder.Property(e => e.MontantTotal).HasColumnName("MontantTotal").HasPrecision(18, 4);
        builder.Property(e => e.PayloadJson).HasColumnName("PayloadJson");
        builder.Property(e => e.CheminFichier).HasColumnName("CheminFichier").HasMaxLength(500);
        builder.Property(e => e.HashSha256).HasColumnName("HashSha256").HasMaxLength(64);
        builder.Property(e => e.TailleOctets).HasColumnName("TailleOctets");
        builder.Property(e => e.DateGeneration).HasColumnName("DateGeneration");

        builder.HasIndex(e => e.Reference).IsUnique().HasDatabaseName("UX_DOCUMENT_PREVISION_REFERENCE");
        builder.HasIndex(e => e.IdAudit).HasDatabaseName("IX_DOCUMENT_PREVISION_AUDIT");
    }
}

public class DocumentPrevisionSequenceConfiguration : IEntityTypeConfiguration<DocumentPrevisionSequence>
{
    public void Configure(EntityTypeBuilder<DocumentPrevisionSequence> builder)
    {
        builder.ToTable("DOCUMENT_PREVISION_SEQUENCE");
        builder.HasKey(e => e.IdSequence);
        builder.Property(e => e.IdSequence).HasColumnName("IdSequence").ValueGeneratedOnAdd();
        builder.Property(e => e.Annee).HasColumnName("Annee");
        builder.Property(e => e.IdDepartement).HasColumnName("IdDepartement");
        builder.Property(e => e.NumeroVersion).HasColumnName("NumeroVersion");
        builder.Property(e => e.TypeDocument).HasColumnName("TypeDocument").HasMaxLength(10);
        builder.Property(e => e.DernierNumero).HasColumnName("DernierNumero");

        builder.HasIndex(e => new { e.Annee, e.IdDepartement, e.NumeroVersion, e.TypeDocument })
            .IsUnique()
            .HasDatabaseName("UX_DOCUMENT_PREVISION_SEQUENCE");
    }
}
