using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Documents;
using Xunit;

namespace BudgetWeb.UnitTests.Rapports;

public class RapportOrganisationResolverTests
{
    private readonly RapportOrganisationResolver _sut = new();

    private static Dictionary<long, RapportStructureNode> TreeEquateur() => new()
    {
        [86] = new(86, null, "ENTITE", "DPE", "EQUATEUR"),
        [117] = new(117, 86, "DEPARTEMENT", "E.DPE.DP.DDI", "DEPARTEMENT DE DISTRIBUTION"),
        [200] = new(200, 117, "DIVISION", "DV1", "Division 1"),
        [201] = new(201, 200, "SERVICE", "SV1", "Service"),
        [118] = new(118, 86, "DEPARTEMENT", "E.DPE.DP.DPR", "DEPARTEMENT DE PRODUCTION"),
        [300] = new(300, 118, "SERVICE", "SV2", "Service sans division"),
    };

    [Fact]
    public void Resolve_Trouve_Entite_Departement_Division()
    {
        var r = _sut.Resolve(1, 201, TreeEquateur());
        Assert.Equal(86, r.IdEntite);
        Assert.Equal("DPE", r.CodeEntite);
        Assert.Equal(117, r.IdStructureDepartement);
        Assert.Equal(200, r.IdDivision);
        Assert.False(r.EstSansDivision);
    }

    [Fact]
    public void Resolve_Sans_Division()
    {
        var r = _sut.Resolve(2, 300, TreeEquateur());
        Assert.Equal(86, r.IdEntite);
        Assert.Equal(118, r.IdStructureDepartement);
        Assert.True(r.EstSansDivision);
    }

    [Fact]
    public void IsUnderNode_Entite_Et_Departement()
    {
        var t = TreeEquateur();
        Assert.True(_sut.IsUnderNode(201, 86, t));
        Assert.True(_sut.IsUnderNode(201, 117, t));
        Assert.False(_sut.IsUnderNode(201, 118, t));
        Assert.False(_sut.IsUnderNode(300, 117, t));
    }
}

public class RapportPrevisionDcRulesTests
{
    [Fact]
    public void HasMontant_Annuel_Zero_Exclu()
    {
        var row = new RapportDcPrevisionRow(1, 1, 0, "ANNUEL", 1, "00100", "X", 1, "00", "G", 1);
        Assert.False(RapportPrevisionDcService.HasMontant(row, new Dictionary<long, decimal[]>()));
    }

    [Fact]
    public void HasMontant_Annuel_Non_Nul()
    {
        var row = new RapportDcPrevisionRow(1, 1, 100, "ANNUEL", 1, "00100", "X", 1, "00", "G", 1);
        Assert.True(RapportPrevisionDcService.HasMontant(row, new Dictionary<long, decimal[]>()));
    }

    [Fact]
    public void HasMontant_Mensuel_Avec_Mois()
    {
        var row = new RapportDcPrevisionRow(9, 1, 0, "MENSUEL", 1, "00100", "X", null, null, null, null);
        var mois = new Dictionary<long, decimal[]> { [9] = [0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0] };
        Assert.True(RapportPrevisionDcService.HasMontant(row, mois));
    }

    [Fact]
    public void ToLigne_Annuel_Sans_Mois()
    {
        var row = new RapportDcPrevisionRow(1, 1, 250_000, "ANNUEL", 1, "00200", "RB ANNUELLE", 1, "00", "G", 1);
        var ligne = RapportPrevisionDcService.ToLigne(row, new Dictionary<long, decimal[]>());
        Assert.Equal(250_000m, ligne.MontantAnnuel);
        Assert.Null(ligne.M01);
        Assert.Null(ligne.M06);
        Assert.Null(ligne.M12);
    }

    [Fact]
    public void ToLigne_Mensuel_Avec_12_Mois()
    {
        var row = new RapportDcPrevisionRow(2, 1, 120_000, "MENSUEL", 2, "00100", "RB MENSUELLE", 1, "00", "G", 1);
        var mois = new Dictionary<long, decimal[]>
        {
            [2] = Enumerable.Repeat(10_000m, 12).ToArray(),
        };
        var ligne = RapportPrevisionDcService.ToLigne(row, mois);
        Assert.Equal(10_000m, ligne.M01);
        Assert.Equal(10_000m, ligne.M12);
    }

    [Fact]
    public void DetermineLayout_Mixte()
    {
        var rows = new List<RapportDcPrevisionRow>
        {
            new(1, 1, 10, "ANNUEL", 1, "a", "a", 1, "00", "g", 1),
            new(2, 1, 10, "MENSUEL", 2, "b", "b", 1, "00", "g", 1),
        };
        Assert.Equal(RapportDcLayoutColonnes.Mixte, RapportPrevisionDcService.DetermineLayout(rows));
    }

    [Fact]
    public void BuildGroupes_SansGroupe_Et_Deux_Id_Meme_Code()
    {
        var rows = new List<RapportDcPrevisionRow>
        {
            new(1, 1, 50, "ANNUEL", 1, "00100", "A", null, null, null, null),
            new(2, 1, 20, "ANNUEL", 2, "02100", "B", 10, "02", "Groupe A", 1),
            new(3, 1, 30, "ANNUEL", 3, "02200", "C", 11, "02", "Groupe B", 2),
        };
        var groupes = RapportPrevisionDcService.BuildGroupes(rows, new Dictionary<long, decimal[]>());
        Assert.Equal(3, groupes.Count);
        Assert.Contains(groupes, g => g.Libelle == "SANS GROUPE N1");
        Assert.Equal(2, groupes.Count(g => g.CodeGroupe == "02"));
    }

    [Fact]
    public void Usd_Format()
    {
        var s = SnelUsdFormat.WithCurrency(12_500_000.5m);
        Assert.Contains("000,50", s);
        Assert.EndsWith("USD", s);
    }

    [Fact]
    public void Mode_Affichage_Annuel_Mensuel()
    {
        Assert.Equal("A", RapportModeAffichage.Abbreviate("ANNUEL"));
        Assert.Equal("M", RapportModeAffichage.Abbreviate("MENSUEL"));
        var row = new RapportDcPrevisionRow(1, 1, 100, "ANNUEL", 1, "00100", "X", 1, "00", "G", 1);
        var ligne = RapportPrevisionDcService.ToLigne(row, new Dictionary<long, decimal[]>());
        Assert.Equal("ANNUEL", ligne.CodeMode);
        Assert.Equal("A", RapportModeAffichage.Abbreviate(ligne.CodeMode));
    }

    [Fact]
    public void Niveaux_Valides()
    {
        Assert.True(RapportBudgetaireNiveau.IsValid("ENTITE"));
        Assert.True(RapportBudgetaireNiveau.IsValid("DEPARTEMENT"));
        Assert.True(RapportBudgetaireNiveau.IsValid("UB"));
        Assert.False(RapportBudgetaireNiveau.IsValid("DIVISION"));
        Assert.False(RapportBudgetaireNiveau.IsValid("GLOBAL"));
    }
}
