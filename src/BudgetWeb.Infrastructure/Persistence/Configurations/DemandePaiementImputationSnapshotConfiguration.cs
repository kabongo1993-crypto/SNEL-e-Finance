using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetWeb.Infrastructure.Persistence.Configurations;

public class DemandePaiementImputationSnapshotConfiguration : IEntityTypeConfiguration<DemandePaiementImputationSnapshot>
{
    public void Configure(EntityTypeBuilder<DemandePaiementImputationSnapshot> builder)
    {
        builder.ToTable("DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT", "dpm");
        builder.HasKey(e => e.IdSnapshot);
        builder.Property(e => e.IdSnapshot).HasColumnName("IdSnapshot").ValueGeneratedOnAdd();
        builder.Property(e => e.FK_Imputation).HasColumnName("FK_Imputation");
        builder.Property(e => e.FK_DemandePaiement).HasColumnName("FK_DemandePaiement");
        builder.Property(e => e.DateSnapshot).HasColumnName("DateSnapshot");
        builder.Property(e => e.BudgetMensuel).HasColumnName("BudgetMensuel").HasColumnType("decimal(19,4)");
        builder.Property(e => e.CreditEngageMensuel).HasColumnName("CreditEngageMensuel").HasColumnType("decimal(19,4)");
        builder.Property(e => e.CreditDisponibleMensuelAvantVisa).HasColumnName("CreditDisponibleMensuelAvantVisa").HasColumnType("decimal(19,4)");
        builder.Property(e => e.BudgetAnnuel).HasColumnName("BudgetAnnuel").HasColumnType("decimal(19,4)");
        builder.Property(e => e.CreditEngageAnnuel).HasColumnName("CreditEngageAnnuel").HasColumnType("decimal(19,4)");
        builder.Property(e => e.CreditDisponibleAnnuelAvantVisa).HasColumnName("CreditDisponibleAnnuelAvantVisa").HasColumnType("decimal(19,4)");
        builder.Property(e => e.MontantPrevision).HasColumnName("MontantPrevision").HasColumnType("decimal(19,4)");
        builder.Property(e => e.EcartPrevisionImputation).HasColumnName("EcartPrevisionImputation").HasColumnType("decimal(19,4)");
        builder.Property(e => e.MontantBrut).HasColumnName("MontantBrut").HasColumnType("decimal(19,4)");
        builder.Property(e => e.Devise).HasColumnName("Devise").HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.TauxConversion).HasColumnName("TauxConversion").HasColumnType("decimal(19,8)");
        builder.Property(e => e.MontantUsd).HasColumnName("MontantUsd").HasColumnType("decimal(19,4)");
        builder.Property(e => e.FK_BudgetLigne).HasColumnName("FK_BudgetLigne");

        builder.HasIndex(e => new { e.FK_Imputation, e.DateSnapshot }).HasDatabaseName("IX_DPM_SNAP_IMPUTATION");
        builder.HasIndex(e => new { e.FK_DemandePaiement, e.DateSnapshot }).HasDatabaseName("IX_DPM_SNAP_DEMANDE");

        builder.HasOne(e => e.Imputation).WithMany(i => i.Snapshots).HasForeignKey(e => e.FK_Imputation).HasConstraintName("FK_DPM_SNAP_IMPUTATION");
        builder.HasOne(e => e.DemandePaiement).WithMany().HasForeignKey(e => e.FK_DemandePaiement).HasConstraintName("FK_DPM_SNAP_DEMANDE");
        builder.HasOne(e => e.PrevisionBudgetaire).WithMany().HasForeignKey(e => e.FK_BudgetLigne).HasConstraintName("FK_DPM_SNAP_BUDGET_LIGNE");
    }
}
