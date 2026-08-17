using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Models;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.ReferentielOrganisationnel;

public class ImportGraphBuilderTests
{
    [Fact]
    public void Construire_RespecteUniciteCodeUB()
    {
        var lignes = new List<LigneExcelNormaliseeDto>
        {
            Ligne(1, "AC", "DDI", "DEPARTEMENT DE DISTRIBUTION (DDI)", "UB001", "UB Test 1"),
            Ligne(2, null, null, null, "UB001", null, secondaire: true, directionCode: "DIR1", directionLibelle: "Direction 1")
        };

        var graph = ImportGraphBuilder.Construire(lignes);

        Assert.Single(graph.UnitesBudgetaires);
        Assert.Equal(2, graph.UnitesBudgetaires["UB001"].LignesSource);
    }

    [Fact]
    public void Construire_CompteRelationsEntiteDepartement()
    {
        var lignes = new List<LigneExcelNormaliseeDto>
        {
            Ligne(1, "AC", "DDI", "DEPARTEMENT DE DISTRIBUTION (DDI)", "UB001", "UB 1"),
            Ligne(2, "DBD", "DDI", "DEPARTEMENT DE DISTRIBUTION (DDI)", "UB002", "UB 2")
        };

        var graph = ImportGraphBuilder.Construire(lignes);

        Assert.Equal(2, graph.Entites.Count);
        Assert.Single(graph.Departements);
        Assert.Equal(2, graph.RelationsEntiteDepartement.Count);
    }

    [Fact]
    public void Construire_LigneSansCodeUbIdentifiable_CreeStructureSansUb()
    {
        var lignes = new List<LigneExcelNormaliseeDto>
        {
            Ligne(1, "AC", "DDI", "DEPARTEMENT DE DISTRIBUTION (DDI)", null, null, sansCodeUb: true, directionCode: "DIRX", directionLibelle: "Direction X")
        };

        var graph = ImportGraphBuilder.Construire(lignes);

        Assert.Empty(graph.UnitesBudgetaires);
        Assert.Contains(graph.Structures.Values, s => s.SansUbRattachee);
        Assert.Equal(1, graph.LignesSansCodeUB);
    }

    private static LigneExcelNormaliseeDto Ligne(
        int numero,
        string? entite,
        string? deptSigle,
        string? deptLibelle,
        string? codeUb,
        string? libelleUb,
        bool secondaire = false,
        bool sansCodeUb = false,
        string? directionCode = null,
        string? directionLibelle = null)
        => new(
            numero,
            "Feuille1",
            entite,
            entite,
            deptSigle,
            deptLibelle,
            directionCode,
            directionLibelle,
            null,
            null,
            null,
            null,
            null,
            null,
            codeUb,
            libelleUb,
            secondaire,
            sansCodeUb || string.IsNullOrWhiteSpace(codeUb));
}
