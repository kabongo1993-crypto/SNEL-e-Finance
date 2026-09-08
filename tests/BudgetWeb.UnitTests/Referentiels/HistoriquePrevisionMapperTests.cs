using BudgetWeb.Application.Services;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class HistoriquePrevisionMapperTests
{
    [Fact]
    public void Test1_BrouillonVersSoumise_UnEvenementSoumission()
    {
        var events = HistoriquePrevisionMapper.ExpandJournalRow(
            1,
            new DateTime(2026, 8, 20, 10, 0, 0),
            "SOUMETTRE_UB",
            4,
            """{"statut":"BROUILLON"}""",
            """{"idVersion":1,"idUB":100,"idDepartement":10,"portee":"UB","statutAvant":"BROUILLON","statutApres":"SOUMISE"}""");

        Assert.Single(events);
        Assert.Equal("SOUMISSION", events[0].Action);
        Assert.Equal("UB", events[0].Portee);
        Assert.Equal("BROUILLON", events[0].AncienStatut);
        Assert.Equal("SOUMISE", events[0].NouveauStatut);
    }

    [Fact]
    public void Test2_SoumiseVersRejetee_ConserveSoumissionEtRejet()
    {
        var s1 = HistoriquePrevisionMapper.ExpandJournalRow(
            1, DateTime.UtcNow, "SOUMETTRE_UB", 4,
            """{"statut":"BROUILLON"}""",
            """{"idVersion":1,"idUB":100,"portee":"UB","statutAvant":"BROUILLON","statutApres":"SOUMISE"}""");
        var s2 = HistoriquePrevisionMapper.ExpandJournalRow(
            2, DateTime.UtcNow, "REJET_UB", 5,
            """{"statut":"SOUMISE"}""",
            """{"idVersion":1,"idUB":100,"portee":"UB","statutAvant":"SOUMISE","statutApres":"REJETEE","extra":{"motif":"Montant à justifier"}}""");

        var all = s1.Concat(s2).ToList();
        Assert.Equal(2, all.Count);
        Assert.Equal("SOUMISSION", all[0].Action);
        Assert.Equal("REJET", all[1].Action);
        Assert.Equal("Montant à justifier", all[1].Motif);
    }

    [Fact]
    public void Test3_RejeteeVersBrouillon_ConserveLeRejet()
    {
        var all = new[]
        {
            Expand("SOUMETTRE_UB", "BROUILLON", "SOUMISE"),
            Expand("REJET_UB", "SOUMISE", "REJETEE", motif: "x"),
            Expand("REOUVRIR_UB", "REJETEE", "BROUILLON"),
        }.SelectMany(x => x).ToList();

        Assert.Equal(3, all.Count);
        Assert.Contains(all, e => e.Action == "REJET" && e.NouveauStatut == "REJETEE");
        Assert.Contains(all, e => e.Action == "REOUVERTURE" && e.NouveauStatut == "BROUILLON");
    }

    [Fact]
    public void Test4_ChaineComplete_ToutesLesEtapes()
    {
        var all = new[]
        {
            Expand("SOUMETTRE_UB", "BROUILLON", "SOUMISE"),
            Expand("CONTROLER_UB", "SOUMISE", "CONTROLEE"),
            Expand("VALIDER_UB", "CONTROLEE", "VALIDEE"),
        }.SelectMany(x => x).ToList();

        Assert.Equal(new[] { "SOUMISSION", "CONTROLE", "VALIDATION" }, all.Select(e => e.Action));
        Assert.Equal("VALIDEE", all[^1].NouveauStatut);
    }

    [Fact]
    public void Test5_RejetDepartement_ExpandParUb()
    {
        var events = HistoriquePrevisionMapper.ExpandJournalRow(
            7,
            new DateTime(2026, 8, 21, 9, 0, 0),
            "REJET_DEPARTEMENT",
            4,
            null,
            """
            {
              "idVersion":1,
              "idDepartement":10,
              "portee":"DEPARTEMENT",
              "motif":"CORRECTION ATTENDU",
              "traitees":2,
              "ub":[
                {"idUB":1,"codeUB":"A001","statutAvant":"SOUMISE"},
                {"idUB":2,"codeUB":"A002","statutAvant":"SOUMISE"}
              ]
            }
            """);

        Assert.Equal(2, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal("DEPARTEMENT", e.Portee);
            Assert.Equal("REJET", e.Action);
            Assert.Equal("REJETEE", e.NouveauStatut);
            Assert.Equal("CORRECTION ATTENDU", e.Motif);
        });
        Assert.Equal(new[] { 1L, 2L }, events.Select(e => e.IdUB).OrderBy(x => x));
    }

    [Fact]
    public void Test6_RejetDepartementA_NeCreePasEvenementPourUbDepartementB()
    {
        var events = HistoriquePrevisionMapper.ExpandJournalRow(
            8, DateTime.UtcNow, "REJET_DEPARTEMENT", 4, null,
            """
            {
              "idVersion":1,"idDepartement":10,"portee":"DEPARTEMENT","motif":"x",
              "ub":[{"idUB":1,"codeUB":"A001","statutAvant":"SOUMISE"}]
            }
            """);

        Assert.DoesNotContain(events, e => e.IdUB == 99);
        Assert.Single(events);
        Assert.Equal(1, events[0].IdUB);
        Assert.Equal(10, events[0].IdDepartement);
    }

    [Fact]
    public void Test7_ActionLibelle_PorteeUbVsDepartement()
    {
        Assert.Equal("REJET — PORTÉE UB", HistoriquePrevisionMapper.MapActionLibelle("REJET", "UB"));
        Assert.Equal(
            "REJET — PORTÉE DÉPARTEMENT",
            HistoriquePrevisionMapper.MapActionLibelle("REJET", "DEPARTEMENT"));
    }

    [Fact]
    public void Test8_FiltreAction_MappeOperations()
    {
        var ops = HistoriquePrevisionMapper.OperationsForActionFilter("VALIDATION");
        Assert.Contains("VALIDER_UB", ops);
        Assert.Contains("VALIDATION_DEPARTEMENT", ops);
        Assert.DoesNotContain("REJET_UB", ops);
    }

    [Fact]
    public void ScenarioRejetPuisRevalidation_ConserveTouteLaChaine()
    {
        var all = new[]
        {
            Expand("SOUMETTRE_UB", "BROUILLON", "SOUMISE"),
            Expand("REJET_UB", "SOUMISE", "REJETEE", motif: "err"),
            Expand("REOUVRIR_UB", "REJETEE", "BROUILLON"),
            Expand("SOUMETTRE_UB", "BROUILLON", "SOUMISE"),
            Expand("CONTROLER_UB", "SOUMISE", "CONTROLEE"),
            Expand("VALIDER_UB", "CONTROLEE", "VALIDEE"),
        }.SelectMany(x => x).ToList();

        Assert.Equal(6, all.Count);
        Assert.Equal("VALIDEE", all[^1].NouveauStatut);
        Assert.Contains(all, e => e.Action == "REJET");
        Assert.Equal(2, all.Count(e => e.Action == "SOUMISSION"));
    }

    [Fact]
    public void AgregatVersion_Ignore()
    {
        var events = HistoriquePrevisionMapper.ExpandJournalRow(
            9, DateTime.UtcNow, "AGREGAT_STATUT_VERSION", 4,
            """{"statut":"SOUMISE"}""",
            """{"statut":"REJETEE"}""");
        Assert.Empty(events);
    }

    private static IReadOnlyList<HistoriqueAuditExpanded> Expand(
        string op,
        string avant,
        string apres,
        string? motif = null)
    {
        var nouvelles = motif is null
            ? $"{{\"idVersion\":1,\"idUB\":100,\"idDepartement\":10,\"portee\":\"UB\",\"statutAvant\":\"{avant}\",\"statutApres\":\"{apres}\"}}"
            : $"{{\"idVersion\":1,\"idUB\":100,\"idDepartement\":10,\"portee\":\"UB\",\"statutAvant\":\"{avant}\",\"statutApres\":\"{apres}\",\"extra\":{{\"motif\":\"{motif}\"}}}}";
        return HistoriquePrevisionMapper.ExpandJournalRow(
            Random.Shared.NextInt64(1, 1_000_000),
            DateTime.UtcNow,
            op,
            4,
            $"{{\"statut\":\"{avant}\"}}",
            nouvelles);
    }
}
