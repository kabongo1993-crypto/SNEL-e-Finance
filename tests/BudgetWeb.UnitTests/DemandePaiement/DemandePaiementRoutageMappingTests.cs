using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Mapping EF Lot 3.1 — routage nominatif DPM (sans effet workflow).</summary>
public class DemandePaiementRoutageMappingTests
{
    [Fact]
    public void DemandePaiement_FK_UtilisateurAssigne_Est_Nullable()
    {
        var entity = BuildModel().FindEntityType(typeof(DemandePaiementEntity))!;
        var property = entity.FindProperty(nameof(DemandePaiementEntity.FK_UtilisateurAssigne))!;

        Assert.True(property.IsNullable);
        Assert.Equal("FK_UtilisateurAssigne", property.GetColumnName());
    }

    [Fact]
    public void DemandePaiementRoutage_Map_Table_Et_Cle()
    {
        var entity = BuildModel().FindEntityType(typeof(DemandePaiementRoutage))!;

        Assert.Equal("DEMANDE_PAIEMENT_ROUTAGE", entity.GetTableName());
        Assert.Equal("dpm", entity.GetSchema());
        Assert.Equal(nameof(DemandePaiementRoutage.IdRoutage), entity.FindPrimaryKey()!.Properties[0].Name);
    }

    [Fact]
    public void DemandePaiementRoutage_Map_Longueurs_Statut_Et_Action()
    {
        var entity = BuildModel().FindEntityType(typeof(DemandePaiementRoutage))!;

        Assert.Equal(40, entity.FindProperty(nameof(DemandePaiementRoutage.StatutSource))!.GetMaxLength());
        Assert.Equal(40, entity.FindProperty(nameof(DemandePaiementRoutage.StatutCible))!.GetMaxLength());
        Assert.Equal(40, entity.FindProperty(nameof(DemandePaiementRoutage.Action))!.GetMaxLength());
        Assert.Equal(500, entity.FindProperty(nameof(DemandePaiementRoutage.Motif))!.GetMaxLength());
    }

    [Fact]
    public void DemandePaiementRoutage_Relations_Utilisateur_Source_Et_Cible()
    {
        var entity = BuildModel().FindEntityType(typeof(DemandePaiementRoutage))!;

        var fkSource = entity.GetForeignKeys()
            .Single(fk => fk.Properties[0].Name == nameof(DemandePaiementRoutage.FK_UtilisateurSource));
        var fkCible = entity.GetForeignKeys()
            .Single(fk => fk.Properties[0].Name == nameof(DemandePaiementRoutage.FK_UtilisateurCible));
        var fkDemande = entity.GetForeignKeys()
            .Single(fk => fk.Properties[0].Name == nameof(DemandePaiementRoutage.FK_DemandePaiement));

        Assert.Equal("FK_DPM_ROUTAGE_USER_SOURCE", fkSource.GetConstraintName());
        Assert.Equal("FK_DPM_ROUTAGE_USER_CIBLE", fkCible.GetConstraintName());
        Assert.Equal("FK_DPM_ROUTAGE_DEMANDE", fkDemande.GetConstraintName());
        Assert.Equal(DeleteBehavior.Restrict, fkSource.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, fkCible.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, fkDemande.DeleteBehavior);
    }

    [Fact]
    public void DemandePaiementRoutage_Index_Actif_Filtre()
    {
        var entity = BuildModel().FindEntityType(typeof(DemandePaiementRoutage))!;
        var index = entity.GetIndexes()
            .Single(i => i.GetDatabaseName() == "IX_DPM_ROUTAGE_Demande_Actif");

        Assert.Equal("[EstActif] = 1", index.GetFilter());
        Assert.Collection(
            index.Properties.Select(p => p.Name),
            first => Assert.Equal(nameof(DemandePaiementRoutage.FK_DemandePaiement), first),
            second => Assert.Equal(nameof(DemandePaiementRoutage.EstActif), second));
    }

    [Fact]
    public void DemandePaiement_UtilisateurAssigne_FK_Convention()
    {
        var entity = BuildModel().FindEntityType(typeof(DemandePaiementEntity))!;
        var fk = entity.GetForeignKeys()
            .Single(fk => fk.Properties[0].Name == nameof(DemandePaiementEntity.FK_UtilisateurAssigne));

        Assert.Equal("FK_DPM_DP_USER_ASSIGNE", fk.GetConstraintName());
    }

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new BudgetDbContext(options);
        return context.Model;
    }
}
