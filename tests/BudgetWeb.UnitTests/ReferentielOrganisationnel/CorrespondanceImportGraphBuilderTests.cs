using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Services;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.ReferentielOrganisationnel;

public class CorrespondanceImportGraphBuilderTests
{
    [Fact]
    public void Construire_DivisionsCommercialesDdkDec_RestentDistinctesMalgreDivision100()
    {
        var workbook = new CorrespondanceWorkbookData(
            "test.xlsx",
            [new EntiteSheetRow("DDK", "DEPARTEMENT DE DISTRIBUTION DE KINSHASA", "ENTITE")],
            [new DepartementSheetRow("DEC", "DEPARTEMENT COMMERCIAL (DEC )")],
            [new EntiteDepartementSheetRow("DDK", "DEPARTEMENT DE DISTRIBUTION DE KINSHASA", "DEC", "DEPARTEMENT COMMERCIAL (DEC )")],
            [],
            [],
            [
                new SansCodeUbSheetRow("DDK", "DEC", "DIVISION GESTION COMMERCIALE CENTRE (GCC)", "100", null, "DIVISION", 710),
                new SansCodeUbSheetRow("DDK", "DEC", "DIVISION GESTION COMMERCIALE KINSHASA EST (GCE)", "100", null, "DIVISION", 754),
                new SansCodeUbSheetRow("DDK", "DEC", "DIVISION GESTION COMMERCIALE KINSHASA NORD  (GCN)", "100", null, "DIVISION", 813),
                new SansCodeUbSheetRow("DDK", "DEC", "DIVISION GESTION COMMERCIALE KINSHASA OUEST (GCO)", "100", null, "DIVISION", 849),
                new SansCodeUbSheetRow("DDK", "DEC", "DIVISION GESTION COMMERCIALE KINSHASA SUD (GCS)", "100", null, "DIVISION", 893)
            ],
            [
                Arbre(710, "DDK", "DEC", "DIVISION GESTION COMMERCIALE CENTRE (GCC)"),
                Arbre(754, "DDK", "DEC", "DIVISION GESTION COMMERCIALE KINSHASA EST (GCE)"),
                Arbre(813, "DDK", "DEC", "DIVISION GESTION COMMERCIALE KINSHASA NORD  (GCN)"),
                Arbre(849, "DDK", "DEC", "DIVISION GESTION COMMERCIALE KINSHASA OUEST (GCO)"),
                Arbre(893, "DDK", "DEC", "DIVISION GESTION COMMERCIALE KINSHASA SUD (GCS)")
            ]);

        var graph = CorrespondanceImportGraphBuilder.Construire(workbook);

        var divisions = graph.Structures.Values
            .Where(s => s.TypeStructure.Equals(TypeStructureOrganisationnelle.Division, StringComparison.OrdinalIgnoreCase)
                        && s.CleMetier.Contains("ENTITE|DDK>DEPARTEMENT|DEC", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Code)
            .OrderBy(c => c)
            .ToList();

        Assert.Equal(["GCC", "GCE", "GCN", "GCO", "GCS"], divisions);
        Assert.Equal(5, graph.LignesSansCodeUB);
        Assert.DoesNotContain(graph.Structures.Keys, k => k.EndsWith("DIVISION|100", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Construire_ConserveDepartementsDistinctsPourUbA006Serie()
    {
        var workbook = new CorrespondanceWorkbookData(
            "test.xlsx",
            [new EntiteSheetRow("AC", "ADMINISTRATION CENTRALE", "ENTITE")],
            [
                new DepartementSheetRow("DSG", "DEPARTEMENT DU SECRETARIAT GENERAL (DSG)"),
                new DepartementSheetRow("DCG", "DEPARTEMENT DU CONTROLE GENERAL (DCG)")
            ],
            [
                new EntiteDepartementSheetRow("AC", "ADMINISTRATION CENTRALE", "DSG", "DEPARTEMENT DU SECRETARIAT GENERAL (DSG)"),
                new EntiteDepartementSheetRow("AC", "ADMINISTRATION CENTRALE", "DCG", "DEPARTEMENT DU CONTROLE GENERAL (DCG)")
            ],
            [
                new UbImportSheetRow("AC", "ADMINISTRATION CENTRALE", "DSG", "DSG", "A00600", "DSG", 1),
                new UbImportSheetRow("AC", "ADMINISTRATION CENTRALE", "DSG", "DSG", "A00610", "Division Etudes", 2),
                new UbImportSheetRow("AC", "ADMINISTRATION CENTRALE", "DCG", "DCG", "A00620", "Division Securite", 3),
                new UbImportSheetRow("AC", "ADMINISTRATION CENTRALE", "DCG", "DCG", "A00630", "Division Inspection", 4)
            ],
            [],
            [],
            [
                UbArbre(1, "AC", "DSG", "A00600", "DSG", "DEPARTEMENT"),
                UbArbre(2, "AC", "DSG", "A00610", "Division Etudes", "DIVISION", division: "18"),
                UbArbre(3, "AC", "DCG", "A00620", "Division Securite", "DIVISION", division: "19"),
                UbArbre(4, "AC", "DCG", "A00630", "Division Inspection", "DIVISION", division: "20")
            ]);

        var graph = CorrespondanceImportGraphBuilder.Construire(workbook);

        Assert.Equal("DSG", graph.UnitesBudgetaires["A00600"].DepartementSigle);
        Assert.Equal("DSG", graph.UnitesBudgetaires["A00610"].DepartementSigle);
        Assert.Equal("DCG", graph.UnitesBudgetaires["A00620"].DepartementSigle);
        Assert.Equal("DCG", graph.UnitesBudgetaires["A00630"].DepartementSigle);
        Assert.Empty(graph.Anomalies);
    }

    private static ArbreSourceSheetRow Arbre(int ligne, string entite, string dept, string libelle)
        => new(
            ligne,
            entite,
            entite,
            dept,
            dept,
            "STRUCTURE_SANS_CODE_UB",
            "DIVISION",
            ligne,
            libelle,
            null,
            "100",
            null,
            null);

    private static ArbreSourceSheetRow UbArbre(
        int ligne,
        string entite,
        string dept,
        string codeUb,
        string libelle,
        string typeElement,
        string? division = null)
        => new(
            ligne,
            entite,
            entite,
            dept,
            dept,
            "UB_PRINCIPALE",
            typeElement,
            ligne,
            libelle,
            null,
            division,
            codeUb,
            null);
}
