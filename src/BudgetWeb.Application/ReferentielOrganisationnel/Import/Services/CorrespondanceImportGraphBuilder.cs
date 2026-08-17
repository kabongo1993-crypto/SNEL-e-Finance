using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Enums;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Models;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.ReferentielOrganisationnel.Import.Services;

public static class CorrespondanceImportGraphBuilder
{
    public static ImportGraphModel Construire(CorrespondanceWorkbookData workbook)
    {
        var graph = new ImportGraphModel();

        foreach (var entite in workbook.Entites)
        {
            graph.Entites[entite.Sigle] = (entite.Sigle, entite.Libelle);
            EnsureStructure(graph, null, TypeStructureOrganisationnelle.Entite, entite.Sigle, entite.Libelle, 0);
        }

        foreach (var dept in workbook.Departements)
        {
            graph.Departements[dept.Sigle] = (dept.Sigle, dept.Libelle);
        }

        foreach (var relation in workbook.RelationsEntiteDepartement)
        {
            graph.RelationsEntiteDepartement.Add((relation.SigleEntite, relation.SigleDepartement));

            if (!graph.Departements.ContainsKey(relation.SigleDepartement))
            {
                graph.Departements[relation.SigleDepartement] = (relation.SigleDepartement, relation.LibelleDepartement);
            }

            var parentEntite = BuildCle(null, TypeStructureOrganisationnelle.Entite, relation.SigleEntite);
            EnsureStructure(
                graph,
                parentEntite,
                TypeStructureOrganisationnelle.Departement,
                relation.SigleDepartement,
                relation.LibelleDepartement,
                0);
        }

        var codeUbParGroupe = workbook.ArbreSource
            .Where(r => !string.IsNullOrWhiteSpace(r.CodeUB))
            .GroupBy(r => r.IdtGroupeSource)
            .ToDictionary(g => g.Key, g => g.First().CodeUB!.Trim(), comparer: EqualityComparer<int>.Default);

        graph.LignesSource = workbook.ArbreSource.Count;

        foreach (var row in workbook.ArbreSource.OrderBy(r => r.LigneSource))
        {
            if (string.IsNullOrWhiteSpace(row.SigleEntite) || string.IsNullOrWhiteSpace(row.SigleDepartement))
            {
                graph.Anomalies.Add(Anomaly(row, ImportIssueSeverity.Error, ImportIssueCode.EntiteManquante,
                    "Entité ou département manquant dans l'arbre source."));
                continue;
            }

            var codeUb = !string.IsNullOrWhiteSpace(row.CodeUB)
                ? row.CodeUB.Trim()
                : codeUbParGroupe.GetValueOrDefault(row.IdtGroupeSource);
            var sansCodeUb = string.IsNullOrWhiteSpace(codeUb);

            if (row.TypeLigne.Equals("STRUCTURE_SANS_CODE_UB", StringComparison.OrdinalIgnoreCase))
            {
                graph.LignesSansCodeUB++;
            }

            var parentCle = BuildCle(null, TypeStructureOrganisationnelle.Entite, row.SigleEntite);
            parentCle = EnsureStructure(graph, parentCle, TypeStructureOrganisationnelle.Departement, row.SigleDepartement,
                row.LibelleDepartement, row.LigneSource);

            var mappedType = MapTypeElement(row.TypeElementSource);
            string? finestCle = parentCle;

            if (!mappedType.Equals(TypeStructureOrganisationnelle.Departement, StringComparison.OrdinalIgnoreCase))
            {
                var code = ResolveStructureCode(row);
                var libelle = ResolveStructureLibelle(row);
                if (string.IsNullOrWhiteSpace(code))
                {
                    if (sansCodeUb)
                    {
                        graph.Anomalies.Add(Anomaly(row, ImportIssueSeverity.Error, ImportIssueCode.LigneSansCodeUbNonIdentifiable,
                            "Structure non identifiable pour une ligne sans Code_UB."));
                    }
                    else
                    {
                        graph.Anomalies.Add(Anomaly(row, ImportIssueSeverity.Warning, ImportIssueCode.CodeStructureManquant,
                            "Code structure manquant, ligne ignorée pour la structure profonde."));
                    }
                }
                else
                {
                    finestCle = EnsureStructure(graph, parentCle, mappedType, code, libelle, row.LigneSource);
                }
            }

            if (sansCodeUb || row.TypeLigne.Equals("STRUCTURE_SANS_CODE_UB", StringComparison.OrdinalIgnoreCase))
            {
                if (finestCle is not null)
                {
                    graph.Structures[finestCle].SansUbRattachee = true;
                }
                continue;
            }

            if (finestCle is null)
            {
                graph.Anomalies.Add(Anomaly(row, ImportIssueSeverity.Error, ImportIssueCode.StructureIncomplete,
                    "Structure incomplète pour enregistrer l'UB."));
                continue;
            }

            RegisterUb(graph, codeUb!, row.LibelleUB, row.SigleDepartement, finestCle, row.LigneSource);
            graph.Structures[finestCle].SansUbRattachee = false;
        }

        ValiderContreFeuilles(graph, workbook);
        return graph;
    }

    private static void ValiderContreFeuilles(ImportGraphModel graph, CorrespondanceWorkbookData workbook)
    {
        if (graph.Entites.Count != workbook.Entites.Count)
        {
            graph.Anomalies.Add(new ImportValidationIssueDto(ImportIssueSeverity.Warning, "ECART_ENTITES",
                $"Entités construites ({graph.Entites.Count}) vs feuille 01 ({workbook.Entites.Count}).",
                null, "01_ENTITES", null, null));
        }

        if (graph.Departements.Count != workbook.Departements.Count)
        {
            graph.Anomalies.Add(new ImportValidationIssueDto(ImportIssueSeverity.Warning, "ECART_DEPARTEMENTS",
                $"Départements construits ({graph.Departements.Count}) vs feuille 02 ({workbook.Departements.Count}).",
                null, "02_DEPARTEMENTS", null, null));
        }

        if (graph.RelationsEntiteDepartement.Count != workbook.RelationsEntiteDepartement.Count)
        {
            graph.Anomalies.Add(new ImportValidationIssueDto(ImportIssueSeverity.Warning, "ECART_RELATIONS",
                $"Relations construites ({graph.RelationsEntiteDepartement.Count}) vs feuille 03 ({workbook.RelationsEntiteDepartement.Count}).",
                null, "03_ENTITE_DEPARTEMENT", null, null));
        }

        var ubFeuille04 = workbook.UbAImporter.Select(u => u.CodeUB).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (graph.UnitesBudgetaires.Count != ubFeuille04)
        {
            graph.Anomalies.Add(new ImportValidationIssueDto(ImportIssueSeverity.Error, "ECART_UB",
                $"UB construites ({graph.UnitesBudgetaires.Count}) vs feuille 04 ({ubFeuille04}).",
                null, "04_UB_A_IMPORTER", null, null));
        }

        if (graph.LignesSansCodeUB != workbook.SansCodeUb.Count)
        {
            graph.Anomalies.Add(new ImportValidationIssueDto(ImportIssueSeverity.Warning, "ECART_SANS_CODE_UB",
                $"Lignes sans Code_UB comptées ({graph.LignesSansCodeUB}) vs feuille 06 ({workbook.SansCodeUb.Count}).",
                null, "06_SANS_CODE_UB", null, null));
        }

        foreach (var ub in workbook.UbAImporter)
        {
            if (!graph.UnitesBudgetaires.ContainsKey(ub.CodeUB))
            {
                graph.Anomalies.Add(new ImportValidationIssueDto(ImportIssueSeverity.Error, "UB_MANQUANTE",
                    $"UB {ub.CodeUB} présente en feuille 04 mais absente du graphe construit.",
                    ub.LigneSource, "04_UB_A_IMPORTER", ub.CodeUB, ub.SigleEntite));
            }
        }
    }

    private static string MapTypeElement(string typeElement)
        => typeElement.ToUpperInvariant() switch
        {
            "DEPARTEMENT" => TypeStructureOrganisationnelle.Departement,
            "DIRECTION" => TypeStructureOrganisationnelle.Direction,
            "DIVISION" => TypeStructureOrganisationnelle.Division,
            "SERVICE" => TypeStructureOrganisationnelle.Service,
            "BUREAU" => TypeStructureOrganisationnelle.Section,
            _ => TypeStructureOrganisationnelle.Autre
        };

    private static string? ResolveStructureCode(ArbreSourceSheetRow row)
    {
        // Cas DDK/DEC : plusieurs STRUCTURE_SANS_CODE_UB partagent Division=100
        // mais sont distinctes via le sigle entre parenthèses (GCC, GCE, GCN, GCO, GCS).
        // On n'applique ce discriminateur que si le sigle est compact ; sinon on conserve Division.
        if (row.TypeLigne.Equals("STRUCTURE_SANS_CODE_UB", StringComparison.OrdinalIgnoreCase))
        {
            var sigleMetier = ExtractSigleMetierCompact(row.LibelleUB);
            if (!string.IsNullOrWhiteSpace(sigleMetier))
            {
                return sigleMetier;
            }
        }

        if (!string.IsNullOrWhiteSpace(row.SigleUB))
        {
            return row.SigleUB.Trim();
        }

        if (!string.IsNullOrWhiteSpace(row.Division))
        {
            return row.Division.Trim();
        }

        return TruncateCode(row.LibelleUB);
    }

    private static string ResolveStructureLibelle(ArbreSourceSheetRow row)
        => !string.IsNullOrWhiteSpace(row.ContenuUB) ? row.ContenuUB.Trim()
            : !string.IsNullOrWhiteSpace(row.LibelleUB) ? row.LibelleUB.Trim()
            : ResolveStructureCode(row) ?? "Structure";

    /// <summary>
    /// Extrait un sigle métier compact entre parenthèses (ex. "... (GCC)" → "GCC").
    /// Ignore les parenthèses descriptives longues (ex. "PROV. DU SUD-UBANGI").
    /// </summary>
    private static string? ExtractSigleMetierCompact(string? libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle))
        {
            return null;
        }

        var text = libelle.Trim();
        var close = text.LastIndexOf(')');
        if (close < 0)
        {
            return null;
        }

        var open = text.LastIndexOf('(', close);
        if (open < 0 || close <= open + 1)
        {
            return null;
        }

        var sigle = text[(open + 1)..close].Trim();
        if (sigle.Length is < 2 or > 8)
        {
            return null;
        }

        for (var i = 0; i < sigle.Length; i++)
        {
            if (!char.IsLetterOrDigit(sigle[i]))
            {
                return null;
            }
        }

        return sigle.ToUpperInvariant();
    }

    private static string? TruncateCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed[..Math.Min(30, trimmed.Length)];
    }    private static string EnsureStructure(
        ImportGraphModel graph,
        string? parentCle,
        string typeStructure,
        string code,
        string libelle,
        int ligneSource)
    {
        var cle = BuildCle(parentCle, typeStructure, code);
        if (!graph.Structures.TryGetValue(cle, out var node))
        {
            node = new StructureNodeModel
            {
                CleMetier = cle,
                TypeStructure = typeStructure,
                Code = code,
                Libelle = libelle,
                CleMetierParent = parentCle,
                SansUbRattachee = false
            };
            graph.Structures[cle] = node;
        }

        node.LignesSource++;
        return cle;
    }

    private static void RegisterUb(
        ImportGraphModel graph,
        string codeUb,
        string libelle,
        string deptSigle,
        string structureCle,
        int ligneSource)
    {
        if (graph.UnitesBudgetaires.TryGetValue(codeUb, out var existing))
        {
            if (!existing.DepartementSigle.Equals(deptSigle, StringComparison.OrdinalIgnoreCase))
            {
                graph.Anomalies.Add(new ImportValidationIssueDto(
                    ImportIssueSeverity.Critical,
                    ImportIssueCode.CodeUbDupliqueIncoherent,
                    $"Code_UB {codeUb} associé à {existing.DepartementSigle} puis {deptSigle}.",
                    ligneSource, "07_ARBRE_SOURCE", codeUb, null));
            }

            existing.LignesSource++;
            existing.CleMetierStructure = structureCle;
            if (!string.IsNullOrWhiteSpace(libelle))
            {
                existing.Libelle = libelle;
            }
        }
        else
        {
            graph.UnitesBudgetaires[codeUb] = new UniteBudgetaireModel
            {
                CodeUB = codeUb,
                Libelle = string.IsNullOrWhiteSpace(libelle) ? codeUb : libelle,
                DepartementSigle = deptSigle,
                CleMetierStructure = structureCle,
                LignesSource = 1
            };
        }
    }

    private static string BuildCle(string? parentCle, string typeStructure, string code)
        => string.IsNullOrWhiteSpace(parentCle)
            ? $"{typeStructure}|{code}"
            : $"{parentCle}>{typeStructure}|{code}";

    private static ImportValidationIssueDto Anomaly(
        ArbreSourceSheetRow row,
        ImportIssueSeverity severite,
        string code,
        string message)
        => new(severite, code, message, row.LigneSource, "07_ARBRE_SOURCE", row.CodeUB, row.SigleEntite);
}
