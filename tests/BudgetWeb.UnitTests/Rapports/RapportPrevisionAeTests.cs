using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Application.Services;
using BudgetWeb.Application.DTOs.Rapports;
using Xunit;

namespace BudgetWeb.UnitTests.Rapports;

public class RapportPrevisionAeRulesTests
{
    private static RapportAePrevisionRow Row(
        long id, long ub, string item, decimal annuel, string mode,
        long idRb, string codeRb, long? idGroupeRb = 1, string? codeG = "00", string? libG = "G", int? ordre = 1)
        => new(id, ub, item, annuel, mode, null, null, idRb, codeRb, $"Lib {codeRb}",
            idGroupeRb, codeG, libG, ordre);

    private static List<ClassementAeLigneDto> Classement(params (string type, string? libelle, long? idG, int ordre)[] lines)
    {
        long id = 1;
        return lines.Select(l => new ClassementAeLigneDto(
            id++, l.type, l.idG, l.type == "GROUPE" ? l.libelle : null,
            l.type == "ITEM" ? l.libelle : null, null, null, l.ordre)).ToList();
    }

    [Fact]
    public void Mois_Obligatoire()
    {
        Assert.Throws<ArgumentException>(() => RapportAeMoisCodes.Parse(null));
        Assert.Throws<ArgumentException>(() => RapportAeMoisCodes.Parse(""));
        Assert.Throws<ArgumentException>(() => RapportAeMoisCodes.Parse("13"));
        Assert.Equal(1, RapportAeMoisCodes.Parse("1"));
        Assert.Null(RapportAeMoisCodes.Parse("tous"));
    }

    [Fact]
    public void Detail_Janvier_MultiRb_Et_Totals()
    {
        var rows = new List<RapportAePrevisionRow>
        {
            Row(1, 1, "Item A", 30_000, "MENSUEL", 100, "00100"),
            Row(2, 1, "Item A", 20_000, "MENSUEL", 112, "00112"),
            Row(3, 1, "Item B", 5_000, "MENSUEL", 100, "00100"),
        };
        var mois = new Dictionary<long, decimal[]>
        {
            [1] = [10_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [2] = [20_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [3] = [5_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        };
        var classement = Classement(
            ("ITEM", "Item A", null, 1),
            ("ITEM", "Item B", null, 2));

        var detail = RapportPrevisionAeService.ConstruireDetailMensuel(1, rows, mois, classement);
        Assert.Equal(2, detail.ColonnesRb.Count);
        Assert.Equal(["00100", "00112"], detail.ColonnesRb.Select(c => c.CodeRB));
        Assert.Equal(2, detail.Lignes.Count(l => l.TypeLigne == "ITEM"));

        var a = detail.Lignes.Single(l => l.LibelleItemAE == "Item A");
        Assert.Equal("01", a.Numero);
        Assert.Equal(10_000m, a.MontantsParRb[0]);
        Assert.Equal(20_000m, a.MontantsParRb[1]);
        Assert.Equal(30_000m, a.Total);

        var b = detail.Lignes.Single(l => l.LibelleItemAE == "Item B");
        Assert.Equal("02", b.Numero);
        Assert.Equal(5_000m, b.MontantsParRb[0]);
        Assert.Equal(0m, b.MontantsParRb[1]);
        Assert.Equal(5_000m, b.Total);

        Assert.Equal(15_000m, detail.TotauxParRb[0]);
        Assert.Equal(20_000m, detail.TotauxParRb[1]);
        Assert.Equal(35_000m, detail.TotalGeneral);
    }

    [Fact]
    public void Detail_Annuel_AfficheTiret_DansCellules()
    {
        var rows = new List<RapportAePrevisionRow>
        {
            Row(1, 1, "Item A", 12_000, "ANNUEL", 100, "00100"),
            Row(2, 1, "Item A", 30_000, "ANNUEL", 112, "00112"),
        };
        var classement = Classement(("ITEM", "Item A", null, 1));
        var detail = RapportPrevisionAeService.ConstruireDetailMensuel(1, rows, new Dictionary<long, decimal[]>(), classement);
        var item = Assert.Single(detail.Lignes.Where(l => l.TypeLigne == "ITEM"));
        Assert.Equal("01", item.Numero);
        Assert.Null(item.MontantsParRb[0]);
        Assert.Null(item.MontantsParRb[1]);
        Assert.Equal(0m, item.Total);
        Assert.Equal(0m, detail.TotalGeneral);
    }

    [Fact]
    public void Detail_Mixte_AnnuelTiret_MensuelValeur()
    {
        var rows = new List<RapportAePrevisionRow>
        {
            Row(1, 1, "Item A", 12_000, "ANNUEL", 100, "00100"),
            Row(2, 1, "Item A", 24_000, "MENSUEL", 112, "00112"),
        };
        var mois = new Dictionary<long, decimal[]>
        {
            [2] = [2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000],
        };
        var classement = Classement(("ITEM", "Item A", null, 1));
        var detail = RapportPrevisionAeService.ConstruireDetailMensuel(1, rows, mois, classement);
        var item = Assert.Single(detail.Lignes.Where(l => l.TypeLigne == "ITEM"));
        Assert.Null(item.MontantsParRb[0]); // ANNUEL → —
        Assert.Equal(2_000m, item.MontantsParRb[1]);
        Assert.Equal(2_000m, item.Total);
    }

    [Fact]
    public void GroupeAe_Et_Numero_Et_SansGroupe()
    {
        var rows = new List<RapportAePrevisionRow>
        {
            Row(1, 1, "Item A", 10, "MENSUEL", 100, "00100"),
            Row(2, 1, "Item B", 20, "MENSUEL", 100, "00100"),
            Row(3, 1, "Item C", 30, "MENSUEL", 100, "00100"),
        };
        var mois = new Dictionary<long, decimal[]>
        {
            [1] = [10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [2] = [20, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [3] = [30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        };
        var classement = Classement(
            ("GROUPE", "FORMATION", 1, 1),
            ("ITEM", "Item A", 1, 2),
            ("ITEM", "Item B", 1, 3),
            ("ITEM", "Item C", null, 4));

        var detail = RapportPrevisionAeService.ConstruireDetailMensuel(1, rows, mois, classement);
        Assert.Equal("GROUPE", detail.Lignes[0].TypeLigne);
        Assert.Equal("FORMATION", detail.Lignes[0].LibelleGroupe);
        Assert.Equal(["01", "02", "03"], detail.Lignes.Where(l => l.TypeLigne == "ITEM").Select(l => l.Numero));
    }

    [Fact]
    public void GroupeOrphelin_Absent()
    {
        var rows = new List<RapportAePrevisionRow>
        {
            Row(1, 1, "Item A", 10, "MENSUEL", 100, "00100"),
        };
        var mois = new Dictionary<long, decimal[]> { [1] = [10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] };
        var classement = Classement(
            ("GROUPE", "ORPHELIN", 9, 1),
            ("GROUPE", "OK", 1, 2),
            ("ITEM", "Item A", 1, 3));
        var detail = RapportPrevisionAeService.ConstruireDetailMensuel(1, rows, mois, classement);
        Assert.Single(detail.Lignes, l => l.TypeLigne == "GROUPE");
        Assert.Equal("OK", detail.Lignes[0].LibelleGroupe);
    }

    [Fact]
    public void OrdreClassement_Respecte()
    {
        var rows = new List<RapportAePrevisionRow>
        {
            Row(1, 1, "Item B", 10, "MENSUEL", 100, "00100"),
            Row(2, 1, "Item A", 20, "MENSUEL", 100, "00100"),
        };
        var mois = new Dictionary<long, decimal[]>
        {
            [1] = [10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [2] = [20, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        };
        var classement = Classement(("ITEM", "Item B", null, 1), ("ITEM", "Item A", null, 2));
        var detail = RapportPrevisionAeService.ConstruireDetailMensuel(1, rows, mois, classement);
        Assert.Equal(["Item B", "Item A"], detail.Lignes.Select(l => l.LibelleItemAE));
    }

    [Fact]
    public void ColonnesRb_OrdreGroupePuisCode()
    {
        var rows = new List<RapportAePrevisionRow>
        {
            Row(1, 1, "X", 1, "MENSUEL", 2, "02200", 11, "02", "B", 2),
            Row(2, 1, "X", 1, "MENSUEL", 1, "01100", 10, "01", "A", 1),
        };
        var cols = RapportPrevisionAeService.DeterminerColonnesRb(rows);
        Assert.Equal(["01100", "02200"], cols.Select(c => c.CodeRB));
    }

    [Fact]
    public void SyntheseRb_CommeDc_AnnuelEtMensuelSepares()
    {
        var rows = new List<RapportAePrevisionRow>
        {
            Row(1, 1, "Item A", 12_000, "ANNUEL", 100, "00100"),
            Row(2, 1, "Item A", 24_000, "MENSUEL", 100, "00100"),
        };
        var mois = new Dictionary<long, decimal[]>
        {
            [2] = [2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000, 2_000],
        };
        var syn = RapportPrevisionAeService.ConstruireSyntheseRb(2026, rows, mois);
        var lignes = Assert.Single(syn.Groupes).Lignes;
        Assert.Equal(2, lignes.Count);
        var annuel = lignes.Single(l => l.CodeMode == "ANNUEL");
        var mensuel = lignes.Single(l => l.CodeMode == "MENSUEL");
        Assert.Equal(12_000m, annuel.MontantCumul);
        Assert.Null(annuel.M01);
        Assert.Equal(24_000m, mensuel.MontantCumul);
        Assert.Equal(2_000m, mensuel.M01);
        Assert.Equal(36_000m, syn.TotalGeneral);
    }

    [Fact]
    public void SyntheseRb_DonneesLiveLike_Formation()
    {
        var rows = new List<RapportAePrevisionRow>
        {
            Row(4, 1, "FORMATION DES UTILISATEUR", 12_000, "ANNUEL", 14, "00100"),
            Row(5, 1, "FORMATION DES UTILISATEUR", 30_000, "ANNUEL", 17, "00112"),
        };
        var syn = RapportPrevisionAeService.ConstruireSyntheseRb(2026, rows, new Dictionary<long, decimal[]>());
        Assert.Equal(42_000m, syn.TotalGeneral);
        Assert.Equal(2, syn.Groupes.SelectMany(g => g.Lignes).Count());
        Assert.All(syn.Groupes.SelectMany(g => g.Lignes), l => Assert.Null(l.M01));
        Assert.Equal(
            "BUDGET DÉTAILLÉ DES ACTIONS D'EXPLOITATION PAR RUBRIQUE BUDGÉTAIRE — EXERCICE 2026 EN USD",
            syn.Titre);
        Assert.All(syn.Groupes.SelectMany(g => g.Lignes), l => Assert.Equal("ANNUEL", l.CodeMode));
        Assert.Equal("A", RapportModeAffichage.Abbreviate("ANNUEL"));
        Assert.Equal("M", RapportModeAffichage.Abbreviate("MENSUEL"));
    }

    [Fact]
    public void Coherence_Detail_SyntheseItem_SyntheseRb_Mensuel()
    {
        // Jeu §37
        var rows = new List<RapportAePrevisionRow>
        {
            Row(1, 1, "Item A", 30_000, "MENSUEL", 100, "00100"),
            Row(2, 1, "Item A", 20_000, "MENSUEL", 112, "00112"),
            Row(3, 1, "Item B", 5_000, "MENSUEL", 100, "00100"),
        };
        var mois = new Dictionary<long, decimal[]>
        {
            [1] = [10_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [2] = [20_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [3] = [5_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        };
        var classement = Classement(("ITEM", "Item A", null, 1), ("ITEM", "Item B", null, 2));
        var detail = RapportPrevisionAeService.ConstruireDetailMensuel(1, rows, mois, classement);

        var prevByUb = new Dictionary<long, IReadOnlyList<RapportAePrevisionRow>> { [1] = rows };
        var classements = new Dictionary<long, IReadOnlyList<ClassementAeLigneDto>> { [1] = classement };
        var identite = new RapportAeUbIdentiteDto(
            null, null, null, null, null, null, "D", "D", null, null, null,
            1, "U", "U", "AE", "VALIDEE", "USD", 2026, 1, null);
        var blocs = new List<RapportAeUbBlocDto> { new(identite, [detail]) };

        var synItem = RapportPrevisionAeService.ConstruireSyntheseItem(2026, blocs, prevByUb, mois, classements);
        var synRb = RapportPrevisionAeService.ConstruireSyntheseRb(2026, rows, mois);

        Assert.Equal(35_000m, detail.TotalGeneral);
        Assert.Equal(35_000m, synItem.Lignes.Where(l => l.TypeLigne == "ITEM").Sum(l => l.M01 ?? 0));
        Assert.Equal(35_000m, synRb.Groupes.SelectMany(g => g.Lignes)
            .Where(l => l.CodeMode == "MENSUEL").Sum(l => l.M01 ?? 0));
        Assert.Equal(55_000m, synItem.TotalGeneral); // Σ MontantAnnuel
        Assert.Equal(55_000m, synRb.TotalGeneral);
    }

    [Fact]
    public void Cellule_Annuel_Null()
    {
        var rows = new[] { Row(1, 1, "X", 100, "ANNUEL", 1, "00100") };
        var (v, contrib) = RapportPrevisionAeService.MontantCelluleMois(rows, 1, new Dictionary<long, decimal[]>());
        Assert.Null(v);
        Assert.False(contrib);
    }
}
