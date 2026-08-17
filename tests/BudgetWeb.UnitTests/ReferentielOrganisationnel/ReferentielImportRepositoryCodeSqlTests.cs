using BudgetWeb.Infrastructure.Import.Repositories;
using Xunit;

namespace BudgetWeb.UnitTests.ReferentielOrganisationnel;

public class ReferentielImportRepositoryCodeSqlTests
{
    [Fact]
    public void ConstruireCodeSqlUnique_Entite_ConserveLeSigle()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Assert.Equal("AC", ReferentielImportRepository.ConstruireCodeSqlUnique("ENTITE|AC", used));
    }

    [Fact]
    public void ConstruireCodeSqlUnique_DepartementsHomonymes_SontDistincts()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ac = ReferentielImportRepository.ConstruireCodeSqlUnique("ENTITE|AC>DEPARTEMENT|DDI", used);
        var dbd = ReferentielImportRepository.ConstruireCodeSqlUnique("ENTITE|DBD>DEPARTEMENT|DDI", used);

        Assert.Equal("E.AC.DP.DDI", ac);
        Assert.Equal("E.DBD.DP.DDI", dbd);
        Assert.NotEqual(ac, dbd);
    }

    [Fact]
    public void ConstruireCodeSqlUnique_DivisionsCommerciales_RestentDistinctes()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codes = new[] { "GCC", "GCE", "GCN", "GCO", "GCS" }
            .Select(sigle => ReferentielImportRepository.ConstruireCodeSqlUnique(
                $"ENTITE|DDK>DEPARTEMENT|DEC>DIVISION|{sigle}", used))
            .ToList();

        Assert.Equal(
        [
            "E.DDK.DP.DEC.DV.GCC",
            "E.DDK.DP.DEC.DV.GCE",
            "E.DDK.DP.DEC.DV.GCN",
            "E.DDK.DP.DEC.DV.GCO",
            "E.DDK.DP.DEC.DV.GCS"
        ], codes);
        Assert.Equal(5, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(codes, c => Assert.True(c.Length <= 30));
    }
}
