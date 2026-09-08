using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Application.Services;
using BudgetWeb.Application.DTOs.Rapports;
using Xunit;

namespace BudgetWeb.UnitTests.Rapports;

public class RapportPrevisionBiRulesTests
{
    private static RapportBiItemCatalogueRow Item(
        long id, string code, string lib, long? parent, int niveau, string? cat = null)
        => new(id, code, lib, parent, niveau, cat, true);

    private static RapportBiPrevisionRow Prev(
        long id, long ub, long item, string detail, decimal annuel, string mode,
        string code = "ITEM 1", string lib = "Lib", long? parent = null, int niv = 1, string? cat = null)
        => new(id, ub, item, code, lib, parent, niv, cat, detail, annuel, mode);

    private static (Dictionary<long, RapportBiItemCatalogueRow> Cat,
        Dictionary<long, List<RapportBiItemCatalogueRow>> Children) CatalogueStandard()
    {
        var items = new List<RapportBiItemCatalogueRow>
        {
            Item(1, "I.A", "Investissements Strategiques", null, 0, RapportPrevisionBiService.CatFondsPropres),
            Item(2, "I.B", "Investissement d'appui", null, 0, RapportPrevisionBiService.CatFondsPropres),
            Item(3, "II", "Projets Ext", null, 0, RapportPrevisionBiService.CatExterieurs),
            Item(4, "ITEM 1", "Matériel outillage", 1, 1),
            Item(5, "ITEM 2", "Installations", 1, 1),
            Item(7, "ITEM 4", "Mobilier", 2, 1),
        };
        var cat = items.ToDictionary(i => i.IdItemBI);
        var children = items.Where(i => i.ParentId is not null)
            .GroupBy(i => i.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CodeItem).ToList());
        return (cat, children);
    }

    [Fact]
    public void Detail_Vers_Item()
    {
        var (cat, children) = CatalogueStandard();
        var prevs = new List<RapportBiPrevisionRow>
        {
            Prev(1, 10, 4, "Detail A", 10_000, "MENSUEL", "ITEM 1", "Matériel outillage", 1),
            Prev(2, 10, 4, "Detail B", 20_000, "MENSUEL", "ITEM 1", "Matériel outillage", 1),
            Prev(3, 10, 4, "Detail C", 30_000, "MENSUEL", "ITEM 1", "Matériel outillage", 1),
        };
        var mois = new Dictionary<long, decimal[]>
        {
            [1] = Enumerable.Repeat(10_000m / 12, 12).ToArray(),
            [2] = Enumerable.Repeat(20_000m / 12, 12).ToArray(),
            [3] = Enumerable.Repeat(30_000m / 12, 12).ToArray(),
        };
        // Note: using explicit month values for clarity in mensuel tests elsewhere; here cumul is MontantAnnuel
        var lignes = RapportPrevisionBiService.ConstruireLignesUb(prevs, mois, cat, children);
        var item = lignes.Single(l => l.TypeLigne == "ITEM" && l.CodeItem == "ITEM 1");
        Assert.Equal(60_000m, item.MontantCumul);
        Assert.Equal(3, lignes.Count(l => l.TypeLigne == "DETAIL"));
    }

    [Fact]
    public void Item_Vers_Parent()
    {
        var (cat, children) = CatalogueStandard();
        var prevs = new List<RapportBiPrevisionRow>
        {
            Prev(1, 10, 4, "A", 10_000, "ANNUEL", "ITEM 1", "M", 1),
            Prev(2, 10, 5, "B", 20_000, "ANNUEL", "ITEM 2", "I", 1),
        };
        var lignes = RapportPrevisionBiService.ConstruireLignesUb(prevs, new Dictionary<long, decimal[]>(), cat, children);
        var parent = lignes.Single(l => l.TypeLigne == "ITEM" && l.CodeItem == "I.A");
        Assert.Equal(30_000m, parent.MontantCumul);
        Assert.Null(parent.M01);
    }

    [Fact]
    public void Parent_Vers_Categorie()
    {
        var (cat, children) = CatalogueStandard();
        var prevs = new List<RapportBiPrevisionRow>
        {
            Prev(1, 10, 4, "A", 10_000, "ANNUEL", "ITEM 1", "M", 1),
            Prev(2, 10, 7, "B", 5_000, "ANNUEL", "ITEM 4", "Mob", 2),
        };
        var lignes = RapportPrevisionBiService.ConstruireLignesUb(prevs, new Dictionary<long, decimal[]>(), cat, children);
        var catI = lignes.Single(l => l.TypeLigne == "CATEGORIE" && l.CodeAffichage == "I.");
        Assert.Equal(15_000m, catI.MontantCumul);
    }

    [Fact]
    public void Total_Ub()
    {
        var (cat, children) = CatalogueStandard();
        var prevs = new List<RapportBiPrevisionRow>
        {
            Prev(1, 10, 4, "A", 10_000, "ANNUEL", "ITEM 1", "M", 1),
            Prev(2, 10, 3, "Ext", 7_000, "ANNUEL", "II", "P", null, 0, RapportPrevisionBiService.CatExterieurs),
        };
        var lignes = RapportPrevisionBiService.ConstruireLignesUb(prevs, new Dictionary<long, decimal[]>(), cat, children);
        Assert.Equal(17_000m, lignes.Single(l => l.TypeLigne == "TOTAL_UB").MontantCumul);
    }

    [Fact]
    public void Annuel_Mois_Null()
    {
        var n = RapportPrevisionBiService.NormaliserPrevision(
            Prev(1, 1, 4, "x", 12_000, "ANNUEL"), new Dictionary<long, decimal[]>());
        Assert.Equal(12_000m, n.Cumul);
        Assert.All(n.Mois, m => Assert.Null(m));
        Assert.False(n.HasMensuel);
    }

    [Fact]
    public void Mensuel_Mois_Reels()
    {
        var mois = new Dictionary<long, decimal[]> { [1] = [1000, 2000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] };
        var n = RapportPrevisionBiService.NormaliserPrevision(Prev(1, 1, 4, "x", 3000, "MENSUEL"), mois);
        Assert.Equal(3000m, n.Cumul);
        Assert.Equal(1000m, n.Mois[0]);
        Assert.Equal(2000m, n.Mois[1]);
        Assert.True(n.HasMensuel);
    }

    [Fact]
    public void Mixte_Annuel_Plus_Mensuel_Sur_Parent()
    {
        var parts = new[]
        {
            RapportPrevisionBiService.NormaliserPrevision(Prev(1, 1, 4, "A", 12_000, "ANNUEL"), new Dictionary<long, decimal[]>()),
            RapportPrevisionBiService.NormaliserPrevision(
                Prev(2, 1, 4, "B", 24_000, "MENSUEL"),
                new Dictionary<long, decimal[]> { [2] = Enumerable.Repeat(2_000m, 12).ToArray() }),
        };
        var agg = RapportPrevisionBiService.Agreger(parts);
        Assert.Equal(36_000m, agg.Cumul);
        Assert.Equal(2_000m, agg.Mois[0]);
        Assert.NotNull(agg.Mois[0]);
    }

    [Fact]
    public void Mixte_SousArbre_100_Pourcent_Annuel_Mois_Null()
    {
        var parts = new[]
        {
            RapportPrevisionBiService.NormaliserPrevision(Prev(1, 1, 4, "A", 10, "ANNUEL"), new Dictionary<long, decimal[]>()),
            RapportPrevisionBiService.NormaliserPrevision(Prev(2, 1, 4, "B", 20, "ANNUEL"), new Dictionary<long, decimal[]>()),
        };
        var agg = RapportPrevisionBiService.Agreger(parts);
        Assert.Equal(30m, agg.Cumul);
        Assert.All(agg.Mois, Assert.Null);
    }

    [Fact]
    public void Prevision_Directe_Sur_Parent()
    {
        var (cat, children) = CatalogueStandard();
        var prevs = new List<RapportBiPrevisionRow>
        {
            Prev(1, 10, 1, "Projet X", 5_000, "ANNUEL", "I.A", "Investissements Strategiques", null, 0, RapportPrevisionBiService.CatFondsPropres),
            Prev(2, 10, 4, "Detail", 10_000, "ANNUEL", "ITEM 1", "M", 1),
        };
        var lignes = RapportPrevisionBiService.ConstruireLignesUb(prevs, new Dictionary<long, decimal[]>(), cat, children);
        var ia = lignes.Single(l => l.TypeLigne == "ITEM" && l.CodeItem == "I.A");
        Assert.Equal(15_000m, ia.MontantCumul);
        Assert.Contains(lignes, l => l.TypeLigne == "DETAIL" && l.DetailBI == "Projet X" && l.IdItemBI == 1);
        Assert.Contains(lignes, l => l.TypeLigne == "DETAIL" && l.DetailBI == "Detail");
    }

    [Fact]
    public void Parent_Sans_Prevision_Directe()
    {
        var (cat, children) = CatalogueStandard();
        var prevs = new List<RapportBiPrevisionRow>
        {
            Prev(1, 10, 4, "Only", 8_000, "ANNUEL", "ITEM 1", "M", 1),
        };
        var lignes = RapportPrevisionBiService.ConstruireLignesUb(prevs, new Dictionary<long, decimal[]>(), cat, children);
        Assert.DoesNotContain(lignes, l => l.TypeLigne == "DETAIL" && l.IdItemBI == 1);
        Assert.Equal(8_000m, lignes.Single(l => l.CodeItem == "I.A" && l.TypeLigne == "ITEM").MontantCumul);
    }

    [Fact]
    public void Categorie_Absente()
    {
        Assert.Equal(RapportPrevisionBiService.SansCategorie, RapportPrevisionBiService.NormaliserCategorie(null));
        Assert.Equal(RapportPrevisionBiService.SansCategorie, RapportPrevisionBiService.NormaliserCategorie("AUTRE"));
        var (code, lib) = RapportPrevisionBiService.LibelleCategorie(RapportPrevisionBiService.SansCategorie);
        Assert.Null(code);
        Assert.Equal(RapportPrevisionBiService.SansCategorie, lib);
    }

    [Fact]
    public void Mode_Affichage_A_M()
    {
        Assert.Equal("A", RapportModeAffichage.Abbreviate("ANNUEL"));
        Assert.Equal("M", RapportModeAffichage.Abbreviate("MENSUEL"));
    }

    [Fact]
    public void Plusieurs_DetailBI()
    {
        var (cat, children) = CatalogueStandard();
        var prevs = new List<RapportBiPrevisionRow>
        {
            Prev(1, 10, 4, "Zebra", 1, "ANNUEL", "ITEM 1", "M", 1),
            Prev(2, 10, 4, "Alpha", 2, "ANNUEL", "ITEM 1", "M", 1),
        };
        var lignes = RapportPrevisionBiService.ConstruireLignesUb(prevs, new Dictionary<long, decimal[]>(), cat, children);
        var details = lignes.Where(l => l.TypeLigne == "DETAIL").Select(l => l.DetailBI).ToList();
        Assert.Equal(["Alpha", "Zebra"], details);
    }

    [Fact]
    public void Layout_Mixte()
    {
        var rows = new List<RapportBiPrevisionRow>
        {
            Prev(1, 1, 4, "a", 1, "ANNUEL"),
            Prev(2, 1, 4, "b", 1, "MENSUEL"),
        };
        Assert.Equal(RapportDcLayoutColonnes.Mixte, RapportPrevisionBiService.DetermineLayout(rows));
    }

    [Fact]
    public void Synthese_Entite_Multi_Departement_Et_Coherence()
    {
        var (cat, children) = CatalogueStandard();
        var mois = new Dictionary<long, decimal[]>();
        var prevByUb = new Dictionary<long, IReadOnlyList<RapportBiPrevisionRow>>
        {
            [10] = [Prev(1, 10, 4, "A", 10_000, "ANNUEL", "ITEM 1", "M", 1)],
            [20] = [Prev(2, 20, 4, "B", 5_000, "ANNUEL", "ITEM 1", "M", 1)],
        };

        var ubA = new RapportBiUbBlocDto(
            new RapportBiUbIdentiteDto(80, "AC", "AC", 91, "D1", "Dept1", "D1", "Dept1", null, null, null,
                10, "U1", "UB1", "BI", "VALIDEE", "USD", 2026, 1, null),
            RapportDcLayoutColonnes.Annuel,
            RapportPrevisionBiService.ConstruireLignesUb(prevByUb[10], mois, cat, children),
            10_000);
        var ubB = new RapportBiUbBlocDto(
            new RapportBiUbIdentiteDto(80, "AC", "AC", 92, "D2", "Dept2", "D2", "Dept2", null, null, null,
                20, "U2", "UB2", "BI", "VALIDEE", "USD", 2026, 1, null),
            RapportDcLayoutColonnes.Annuel,
            RapportPrevisionBiService.ConstruireLignesUb(prevByUb[20], mois, cat, children),
            5_000);

        var depts = new List<RapportBiDepartementBlocDto>
        {
            new(91, "D1", "Dept1", 0, [ubA], 10_000),
            new(92, "D2", "Dept2", 1, [ubB], 5_000),
        };

        var syn = RapportPrevisionBiService.ConstruireSyntheseEntite(
            2026, depts, prevByUb, mois, cat, children, new Dictionary<long, RapportUbOrgResolution>());

        Assert.Equal(2, syn.ColonnesDepartement.Count);
        Assert.DoesNotContain(syn.Lignes, l => l.TypeLigne == "DETAIL");
        var total = syn.Lignes.Single(l => l.TypeLigne == "TOTAL");
        Assert.Equal(15_000m, total.TotalLigne);
        Assert.Equal(15_000m, syn.TotalGeneral);
        Assert.Equal([10_000m, 5_000m], total.MontantsParDepartement);

        var item1 = syn.Lignes.Single(l => l.TypeLigne == "ITEM" && l.IdItemBI == 4);
        Assert.Equal([10_000m, 5_000m], item1.MontantsParDepartement);
    }

    [Fact]
    public void Categorie_I_II_Pas_Des_ItemBI()
    {
        Assert.Equal(RapportPrevisionBiService.CatFondsPropres,
            RapportPrevisionBiService.NormaliserCategorie("financements sur fonds propres"));
        var (c1, _) = RapportPrevisionBiService.LibelleCategorie(RapportPrevisionBiService.CatFondsPropres);
        var (c2, _) = RapportPrevisionBiService.LibelleCategorie(RapportPrevisionBiService.CatExterieurs);
        Assert.Equal("I.", c1);
        Assert.Equal("II.", c2);
    }

    [Fact]
    public void Total_Departement_Somme_Des_Totaux_Ub()
    {
        var (cat, children) = CatalogueStandard();
        var mois = new Dictionary<long, decimal[]>();
        var prevA = new List<RapportBiPrevisionRow>
        {
            Prev(1, 10, 4, "A", 4_000, "ANNUEL", "ITEM 1", "M", 1),
        };
        var prevB = new List<RapportBiPrevisionRow>
        {
            Prev(2, 11, 4, "B", 6_000, "ANNUEL", "ITEM 1", "M", 1),
        };
        var ubA = new RapportBiUbBlocDto(
            new RapportBiUbIdentiteDto(80, "AC", "AC", 91, "D1", "Dept1", "D1", "Dept1", null, null, null,
                10, "U1", "UB1", "BI", "VALIDEE", "USD", 2026, 1, null),
            RapportDcLayoutColonnes.Annuel,
            RapportPrevisionBiService.ConstruireLignesUb(prevA, mois, cat, children),
            4_000);
        var ubB = new RapportBiUbBlocDto(
            new RapportBiUbIdentiteDto(80, "AC", "AC", 91, "D1", "Dept1", "D1", "Dept1", null, null, null,
                11, "U2", "UB2", "BI", "VALIDEE", "USD", 2026, 1, null),
            RapportDcLayoutColonnes.Annuel,
            RapportPrevisionBiService.ConstruireLignesUb(prevB, mois, cat, children),
            6_000);
        var dept = new RapportBiDepartementBlocDto(91, "D1", "Dept1", 0, [ubA, ubB], ubA.TotalUb + ubB.TotalUb);
        Assert.Equal(10_000m, dept.TotalDepartement);
        Assert.Equal(2, dept.Ubs.Count);
    }

    [Fact]
    public void Aucun_Montant_Bi_HasMontant_False()
    {
        var mois = new Dictionary<long, decimal[]>();
        Assert.False(RapportPrevisionBiService.HasMontant(
            Prev(1, 10, 4, "Z", 0, "ANNUEL"), mois));
        Assert.False(RapportPrevisionBiService.HasMontant(
            Prev(2, 10, 4, "Z", 0, "MENSUEL"), mois));
    }

    [Fact]
    public void Tri_Departement_Puis_Sans_Departement_En_Fin()
    {
        var ubs = new List<RapportUbOrgRow>
        {
            Ub(1, "ZB"),
            Ub(2, "AA"),
            Ub(3, "MM"),
        };
        var resolutions = new Dictionary<long, RapportUbOrgResolution>
        {
            [1] = Res(1, 92, "ZZ"),
            [2] = Res(2, 91, "AA"),
            [3] = Res(3, null, null),
        };
        var groups = RapportPrevisionBiService.GroupByDepartement(ubs, resolutions, RapportBudgetaireNiveau.Entite);
        Assert.Equal(["AA", "ZZ", "SANS DÉPARTEMENT"], groups.Select(g => g.Code).ToList());
    }

    [Fact]
    public void Tri_Ub_Ordinal_IgnoreCase()
    {
        var ubs = new List<RapportUbOrgRow> { Ub(1, "b2"), Ub(2, "A1"), Ub(3, "a0") };
        var resolutions = new Dictionary<long, RapportUbOrgResolution>
        {
            [1] = Res(1, 91, "D1"),
            [2] = Res(2, 91, "D1"),
            [3] = Res(3, 91, "D1"),
        };
        var groups = RapportPrevisionBiService.GroupByDepartement(ubs, resolutions, RapportBudgetaireNiveau.Departement);
        Assert.Single(groups);
        Assert.Equal(["a0", "A1", "b2"], groups[0].Ubs.Select(u => u.CodeUB).ToList());
    }

    [Fact]
    public void Plusieurs_Ubs_Meme_Departement()
    {
        var ubs = new List<RapportUbOrgRow> { Ub(10, "U1"), Ub(20, "U2") };
        var resolutions = new Dictionary<long, RapportUbOrgResolution>
        {
            [10] = Res(10, 91, "D1"),
            [20] = Res(20, 91, "D1"),
        };
        var groups = RapportPrevisionBiService.GroupByDepartement(ubs, resolutions, RapportBudgetaireNiveau.Entite);
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Ubs.Count);
    }

    [Fact]
    public void Plusieurs_Departements_Dans_Entite()
    {
        var ubs = new List<RapportUbOrgRow> { Ub(10, "U1"), Ub(20, "U2") };
        var resolutions = new Dictionary<long, RapportUbOrgResolution>
        {
            [10] = Res(10, 91, "D1"),
            [20] = Res(20, 92, "D2"),
        };
        var groups = RapportPrevisionBiService.GroupByDepartement(ubs, resolutions, RapportBudgetaireNiveau.Entite);
        Assert.Equal(2, groups.Count);
        Assert.Equal(["D1", "D2"], groups.Select(g => g.Code).ToList());
    }

    private static RapportUbOrgRow Ub(long id, string code)
        => new(id, code, code, 1, "DT", "DT", id * 100, "UB", code, code);

    private static RapportUbOrgResolution Res(long idUb, long? idDept, string? codeDept)
        => new(idUb, 80, "AC", "AC", idDept, codeDept, codeDept, null, null, null, true);
}
