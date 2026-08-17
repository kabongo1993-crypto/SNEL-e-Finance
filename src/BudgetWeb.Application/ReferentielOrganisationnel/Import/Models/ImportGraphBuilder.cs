using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Enums;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.ReferentielOrganisationnel.Import.Models;

public sealed class StructureNodeModel
{
    public required string CleMetier { get; init; }
    public required string TypeStructure { get; init; }
    public required string Code { get; init; }
    public required string Libelle { get; init; }
    public string? CleMetierParent { get; init; }
    public int LignesSource { get; set; }
    public bool SansUbRattachee { get; set; }
}

public sealed class UniteBudgetaireModel
{
    public required string CodeUB { get; init; }
    public string Libelle { get; set; } = string.Empty;
    public required string DepartementSigle { get; set; }
    public required string CleMetierStructure { get; set; }
    public int LignesSource { get; set; }
}

public sealed class ImportGraphModel
{
    public Dictionary<string, (string Code, string Libelle)> Entites { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, (string Sigle, string Libelle)> Departements { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<(string EntiteCode, string DepartementSigle)> RelationsEntiteDepartement { get; } = [];
    public Dictionary<string, StructureNodeModel> Structures { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, UniteBudgetaireModel> UnitesBudgetaires { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ImportValidationIssueDto> Anomalies { get; } = [];
    public int LignesSansCodeUB { get; set; }
    public int LignesSource { get; set; }

    public ImportCompteursDto ToCompteurs()
    {
        var anomaliesCritiques = Anomalies.Count(a => a.Severite >= ImportIssueSeverity.Error);
        return new ImportCompteursDto(
            Entites.Count,
            Departements.Count,
            RelationsEntiteDepartement.Count,
            Structures.Count,
            UnitesBudgetaires.Count,
            LignesSansCodeUB,
            LignesSource,
            Anomalies.Count,
            anomaliesCritiques);
    }
}

internal sealed class ImportContexteLigne
{
    public string? EntiteCode { get; set; }
    public string? EntiteLibelle { get; set; }
    public string? DepartementSigle { get; set; }
    public string? DepartementLibelle { get; set; }
    public string? DirectionCode { get; set; }
    public string? DirectionLibelle { get; set; }
    public string? DivisionCode { get; set; }
    public string? DivisionLibelle { get; set; }
    public string? ServiceCode { get; set; }
    public string? ServiceLibelle { get; set; }
    public string? SectionCode { get; set; }
    public string? SectionLibelle { get; set; }
    public string? CodeUB { get; set; }
    public string? LibelleUB { get; set; }

    public void Appliquer(LigneExcelNormaliseeDto ligne)
    {
        if (!string.IsNullOrWhiteSpace(ligne.EntiteCode)) EntiteCode = ligne.EntiteCode;
        if (!string.IsNullOrWhiteSpace(ligne.EntiteLibelle)) EntiteLibelle = ligne.EntiteLibelle;
        if (!string.IsNullOrWhiteSpace(ligne.DepartementSigle)) DepartementSigle = ligne.DepartementSigle;
        if (!string.IsNullOrWhiteSpace(ligne.DepartementLibelle)) DepartementLibelle = ligne.DepartementLibelle;
        if (!string.IsNullOrWhiteSpace(ligne.DirectionCode)) DirectionCode = ligne.DirectionCode;
        if (!string.IsNullOrWhiteSpace(ligne.DirectionLibelle)) DirectionLibelle = ligne.DirectionLibelle;
        if (!string.IsNullOrWhiteSpace(ligne.DivisionCode)) DivisionCode = ligne.DivisionCode;
        if (!string.IsNullOrWhiteSpace(ligne.DivisionLibelle)) DivisionLibelle = ligne.DivisionLibelle;
        if (!string.IsNullOrWhiteSpace(ligne.ServiceCode)) ServiceCode = ligne.ServiceCode;
        if (!string.IsNullOrWhiteSpace(ligne.ServiceLibelle)) ServiceLibelle = ligne.ServiceLibelle;
        if (!string.IsNullOrWhiteSpace(ligne.SectionCode)) SectionCode = ligne.SectionCode;
        if (!string.IsNullOrWhiteSpace(ligne.SectionLibelle)) SectionLibelle = ligne.SectionLibelle;
        if (!string.IsNullOrWhiteSpace(ligne.CodeUB)) CodeUB = ligne.CodeUB;
        if (!string.IsNullOrWhiteSpace(ligne.LibelleUB)) LibelleUB = ligne.LibelleUB;
    }

    public LigneExcelNormaliseeDto Materialiser(LigneExcelNormaliseeDto source)
        => source with
        {
            EntiteCode = source.EntiteCode ?? EntiteCode,
            EntiteLibelle = source.EntiteLibelle ?? EntiteLibelle,
            DepartementSigle = source.DepartementSigle ?? DepartementSigle,
            DepartementLibelle = source.DepartementLibelle ?? DepartementLibelle,
            DirectionCode = source.DirectionCode ?? DirectionCode,
            DirectionLibelle = source.DirectionLibelle ?? DirectionLibelle,
            DivisionCode = source.DivisionCode ?? DivisionCode,
            DivisionLibelle = source.DivisionLibelle ?? DivisionLibelle,
            ServiceCode = source.ServiceCode ?? ServiceCode,
            ServiceLibelle = source.ServiceLibelle ?? ServiceLibelle,
            SectionCode = source.SectionCode ?? SectionCode,
            SectionLibelle = source.SectionLibelle ?? SectionLibelle,
            CodeUB = source.CodeUB ?? CodeUB,
            LibelleUB = source.LibelleUB ?? LibelleUB
        };
}

internal readonly record struct StructureNiveau(string TypeStructure, string? Code, string? Libelle);

public static class ImportGraphBuilder
{
    public static ImportGraphModel Construire(IReadOnlyList<LigneExcelNormaliseeDto> lignes)
    {
        var graph = new ImportGraphModel { LignesSource = lignes.Count };
        var contexte = new ImportContexteLigne();

        foreach (var ligne in lignes.OrderBy(l => l.Feuille).ThenBy(l => l.NumeroLigne))
        {
            var effective = contexte.Materialiser(ligne);
            var sansCodeUb = string.IsNullOrWhiteSpace(effective.CodeUB);
            if (sansCodeUb)
            {
                graph.LignesSansCodeUB++;
            }

            if (string.IsNullOrWhiteSpace(effective.EntiteCode))
            {
                graph.Anomalies.Add(Issue(ligne, ImportIssueSeverity.Error, ImportIssueCode.EntiteManquante,
                    "Entité manquante ou non héritée."));
                continue;
            }

            var entiteLibelle = effective.EntiteLibelle ?? effective.EntiteCode;
            graph.Entites[effective.EntiteCode] = (effective.EntiteCode, entiteLibelle);

            if (string.IsNullOrWhiteSpace(effective.DepartementSigle))
            {
                if (sansCodeUb)
                {
                    graph.Anomalies.Add(Issue(ligne, ImportIssueSeverity.Error, ImportIssueCode.LigneSansCodeUbNonIdentifiable,
                        "Ligne sans Code_UB : département non identifiable."));
                }
                else
                {
                    graph.Anomalies.Add(Issue(ligne, ImportIssueSeverity.Error, ImportIssueCode.DepartementManquant,
                        "Département manquant ou non hérité."));
                }
                continue;
            }

            var deptLibelle = effective.DepartementLibelle ?? effective.DepartementSigle;
            if (string.IsNullOrWhiteSpace(deptLibelle))
            {
                graph.Anomalies.Add(Issue(ligne, ImportIssueSeverity.Warning, ImportIssueCode.LibelleDepartementManquant,
                    $"Libellé manquant pour le département {effective.DepartementSigle}, sigle utilisé."));
                deptLibelle = effective.DepartementSigle;
            }

            graph.Departements[effective.DepartementSigle] = (effective.DepartementSigle, deptLibelle);
            graph.RelationsEntiteDepartement.Add((effective.EntiteCode, effective.DepartementSigle));

            var niveaux = new List<StructureNiveau>
            {
                new(TypeStructureOrganisationnelle.Entite, effective.EntiteCode, entiteLibelle),
                new(TypeStructureOrganisationnelle.Departement, effective.DepartementSigle, deptLibelle)
            };

            AjouterNiveau(niveaux, TypeStructureOrganisationnelle.Direction, effective.DirectionCode, effective.DirectionLibelle);
            AjouterNiveau(niveaux, TypeStructureOrganisationnelle.Division, effective.DivisionCode, effective.DivisionLibelle);
            AjouterNiveau(niveaux, TypeStructureOrganisationnelle.Service, effective.ServiceCode, effective.ServiceLibelle);
            AjouterNiveau(niveaux, TypeStructureOrganisationnelle.Section, effective.SectionCode, effective.SectionLibelle);

            string? parentCle = null;
            string? finestCle = null;
            foreach (var niveau in niveaux)
            {
                var code = NormaliserCode(niveau.Code, niveau.Libelle, niveau.TypeStructure);
                var libelle = NormaliserLibelle(niveau.Libelle, code);
                var cle = BuildCle(parentCle, niveau.TypeStructure, code);
                if (!graph.Structures.TryGetValue(cle, out var node))
                {
                    node = new StructureNodeModel
                    {
                        CleMetier = cle,
                        TypeStructure = niveau.TypeStructure,
                        Code = code,
                        Libelle = libelle,
                        CleMetierParent = parentCle,
                        SansUbRattachee = sansCodeUb
                    };
                    graph.Structures[cle] = node;
                }

                node.LignesSource++;
                parentCle = cle;
                finestCle = cle;
            }

            if (sansCodeUb)
            {
                if (finestCle is null)
                {
                    graph.Anomalies.Add(Issue(ligne, ImportIssueSeverity.Error, ImportIssueCode.LigneSansCodeUbNonIdentifiable,
                        "Ligne sans Code_UB : structure non identifiable."));
                }
                else
                {
                    graph.Structures[finestCle].SansUbRattachee = true;
                }

                contexte.Appliquer(ligne);
                continue;
            }

            if (finestCle is null)
            {
                graph.Anomalies.Add(Issue(ligne, ImportIssueSeverity.Error, ImportIssueCode.StructureIncomplete,
                    "Structure organisationnelle incomplète pour la ligne."));
                contexte.Appliquer(ligne);
                continue;
            }

            if (graph.UnitesBudgetaires.TryGetValue(effective.CodeUB!, out var ubExistante))
            {
                if (!ubExistante.DepartementSigle.Equals(effective.DepartementSigle, StringComparison.OrdinalIgnoreCase))
                {
                    graph.Anomalies.Add(Issue(ligne, ImportIssueSeverity.Critical, ImportIssueCode.CodeUbDupliqueIncoherent,
                        $"Code_UB {effective.CodeUB} déjà défini avec un autre département ({ubExistante.DepartementSigle})."));
                }

                ubExistante.LignesSource++;
                if (!string.IsNullOrWhiteSpace(effective.LibelleUB))
                {
                    ubExistante.Libelle = effective.LibelleUB;
                }

                ubExistante.CleMetierStructure = finestCle;
            }
            else
            {
                graph.UnitesBudgetaires[effective.CodeUB!] = new UniteBudgetaireModel
                {
                    CodeUB = effective.CodeUB!,
                    Libelle = effective.LibelleUB ?? effective.CodeUB!,
                    DepartementSigle = effective.DepartementSigle,
                    CleMetierStructure = finestCle,
                    LignesSource = 1
                };
            }

            if (graph.Structures.TryGetValue(finestCle, out var structureFeuille))
            {
                structureFeuille.SansUbRattachee = false;
            }

            contexte.Appliquer(ligne);
        }

        return graph;
    }

    private static void AjouterNiveau(List<StructureNiveau> niveaux, string type, string? code, string? libelle)
    {
        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(libelle))
        {
            return;
        }

        niveaux.Add(new StructureNiveau(type, code, libelle));
    }

    private static string NormaliserCode(string? code, string? libelle, string typeStructure)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            return code.Trim();
        }

        if (!string.IsNullOrWhiteSpace(libelle))
        {
            return libelle.Trim();
        }

        throw new InvalidOperationException($"Code structure manquant pour {typeStructure}.");
    }

    private static string NormaliserLibelle(string? libelle, string code)
        => string.IsNullOrWhiteSpace(libelle) ? code : libelle.Trim();

    private static string BuildCle(string? parentCle, string typeStructure, string code)
        => string.IsNullOrWhiteSpace(parentCle)
            ? $"{typeStructure}|{code}"
            : $"{parentCle}>{typeStructure}|{code}";

    private static ImportValidationIssueDto Issue(
        LigneExcelNormaliseeDto ligne,
        ImportIssueSeverity severite,
        string code,
        string message)
        => new(severite, code, message, ligne.NumeroLigne, ligne.Feuille, ligne.CodeUB, ligne.EntiteCode);
}
