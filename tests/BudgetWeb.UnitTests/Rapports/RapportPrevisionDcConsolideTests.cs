using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Documents;
using Xunit;

namespace BudgetWeb.UnitTests.Rapports;

public class RapportPrevisionDcConsolideTests
{
    [Fact]
    public void Consolider_Somme_Plusieurs_UB_Meme_RB()
    {
        var rows = new List<RapportDcPrevisionRow>
        {
            new(1, 10, 100_000, "ANNUEL", 100, "00100", "Achats énergie", 1, "00", "ACHATS", 1),
            new(2, 11, 250_000, "ANNUEL", 100, "00100", "Achats énergie", 1, "00", "ACHATS", 1),
            new(3, 12, 150_000, "ANNUEL", 100, "00100", "Achats énergie", 1, "00", "ACHATS", 1),
        };
        var groupes = RapportPrevisionDcConsolideService.Consolider(rows, new Dictionary<long, decimal[]>());
        Assert.Single(groupes);
        Assert.Single(groupes[0].Lignes);
        Assert.Equal(500_000m, groupes[0].Lignes[0].MontantCumul);
        Assert.Equal(500_000m, groupes[0].SousTotalDc);
        Assert.Null(groupes[0].Lignes[0].M01);
    }

    [Fact]
    public void Consolider_Mois_Additionnes_Pas_Moyenne()
    {
        var rows = new List<RapportDcPrevisionRow>
        {
            new(1, 10, 30_000, "MENSUEL", 100, "00100", "X", 1, "00", "G", 1),
            new(2, 11, 40_000, "MENSUEL", 100, "00100", "X", 1, "00", "G", 1),
        };
        var mois = new Dictionary<long, decimal[]>
        {
            [1] = [10_000, 20_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [2] = [15_000, 25_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        };
        var g = RapportPrevisionDcConsolideService.Consolider(rows, mois);
        var l = Assert.Single(Assert.Single(g).Lignes);
        Assert.Equal(25_000m, l.M01);
        Assert.Equal(45_000m, l.M02);
        Assert.Equal(70_000m, l.MontantCumul);
    }

    [Fact]
    public void Consolider_Mixte_Deux_Lignes_Sans_Ventilation_Artificielle()
    {
        var rows = new List<RapportDcPrevisionRow>
        {
            new(1, 10, 30_000, "MENSUEL", 100, "00100", "X", 1, "00", "G", 1),
            new(2, 11, 120_000, "ANNUEL", 100, "00100", "X", 1, "00", "G", 1),
        };
        var mois = new Dictionary<long, decimal[]>
        {
            [1] = [10_000, 20_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        };
        var lignes = Assert.Single(RapportPrevisionDcConsolideService.Consolider(rows, mois)).Lignes;
        Assert.Equal(2, lignes.Count);
        var mensuel = lignes.Single(l => l.CodeMode == "MENSUEL");
        var annuel = lignes.Single(l => l.CodeMode == "ANNUEL");
        Assert.Equal(10_000m, mensuel.M01);
        Assert.Null(annuel.M01);
        Assert.Equal(120_000m, annuel.MontantCumul);
        Assert.Equal(150_000m, mensuel.MontantCumul + annuel.MontantCumul);
    }

    [Fact]
    public void Consolider_Deux_Groupes_02_Distincts_Par_Id()
    {
        var rows = new List<RapportDcPrevisionRow>
        {
            new(1, 1, 10, "ANNUEL", 1, "02100", "A", 10, "02", "Groupe A", 1),
            new(2, 1, 20, "ANNUEL", 2, "02200", "B", 11, "02", "Groupe B", 2),
        };
        var g = RapportPrevisionDcConsolideService.Consolider(rows, new Dictionary<long, decimal[]>());
        Assert.Equal(2, g.Count);
        Assert.All(g, x => Assert.Equal("02", x.CodeGroupe));
        Assert.Equal(10, g.Sum(x => x.SousTotalDc) - 20); // 30 total
        Assert.Equal(30m, g.Sum(x => x.SousTotalDc));
    }

    [Fact]
    public void Consolider_SansGroupe_Et_Zero_Exclu()
    {
        var rows = new List<RapportDcPrevisionRow>
        {
            new(1, 1, 50, "ANNUEL", 1, "00100", "A", null, null, null, null),
            new(2, 1, 0, "ANNUEL", 2, "00111", "B", 1, "00", "G", 1),
        };
        var g = RapportPrevisionDcConsolideService.Consolider(rows, new Dictionary<long, decimal[]>());
        Assert.Single(g);
        Assert.Equal("SANS GROUPE N1", g[0].Libelle);
        Assert.Equal(50m, g[0].SousTotalDc);
    }

    [Fact]
    public void Usd_Format()
    {
        Assert.EndsWith("USD", SnelUsdFormat.WithCurrency(12_500_000m));
    }

    [Fact]
    public void Mode_Affichage_Annuel_Mensuel()
    {
        Assert.Equal("A", RapportModeAffichage.Abbreviate("ANNUEL"));
        Assert.Equal("M", RapportModeAffichage.Abbreviate("MENSUEL"));
        Assert.Equal("A", RapportModeAffichage.Abbreviate("annuel"));
        Assert.Equal("M", RapportModeAffichage.Abbreviate("mensuel"));
        // DTO métier inchangé
        var rows = new List<RapportDcPrevisionRow>
        {
            new(1, 10, 100_000, "ANNUEL", 100, "00100", "X", 1, "00", "G", 1),
            new(2, 11, 50_000, "MENSUEL", 100, "00100", "X", 1, "00", "G", 1),
        };
        var groupes = RapportPrevisionDcConsolideService.Consolider(rows, new Dictionary<long, decimal[]>
        {
            [2] = Enumerable.Repeat(5_000m, 12).ToArray(),
        });
        var lignes = Assert.Single(groupes).Lignes;
        Assert.Contains(lignes, l => l.CodeMode == "ANNUEL");
        Assert.Contains(lignes, l => l.CodeMode == "MENSUEL");
        Assert.Equal("A", RapportModeAffichage.Abbreviate(lignes.Single(l => l.CodeMode == "ANNUEL").CodeMode));
        Assert.Equal("M", RapportModeAffichage.Abbreviate(lignes.Single(l => l.CodeMode == "MENSUEL").CodeMode));
    }
}
