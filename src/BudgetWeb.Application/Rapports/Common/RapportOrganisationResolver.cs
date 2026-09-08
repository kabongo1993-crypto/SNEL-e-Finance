using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Rapports.Common;

public static class RapportBudgetaireNiveau
{
    public const string Entite = "ENTITE";
    public const string Departement = "DEPARTEMENT";
    public const string Ub = "UB";

    public static bool IsValid(string? value)
    {
        var n = Normaliser(value);
        return n is Entite or Departement or Ub;
    }

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}

public static class RapportDcLayoutColonnes
{
    public const string Annuel = "ANNUEL";
    public const string Mensuel = "MENSUEL";
    public const string Mixte = "MIXTE";
}

public record RapportStructureNode(
    long IdStructure,
    long? ParentId,
    string TypeStructure,
    string Code,
    string Libelle);

public record RapportUbOrgRow(
    long IdUB,
    string CodeUB,
    string LibelleUB,
    long IdDepartementTable,
    string CodeDepartementTable,
    string LibelleDepartementTable,
    long IdStructure,
    string TypeStructure,
    string CodeStructure,
    string LibelleStructure);

public record RapportUbOrgResolution(
    long IdUB,
    long? IdEntite,
    string? CodeEntite,
    string? LibelleEntite,
    long? IdStructureDepartement,
    string? CodeStructureDepartement,
    string? LibelleStructureDepartement,
    long? IdDivision,
    string? CodeDivision,
    string? LibelleDivision,
    bool EstSansDivision);

/// <summary>Résolution Entité / Département structure / Division — réutilisable DC / BI / AE.</summary>
public interface IRapportOrganisationResolver
{
    RapportUbOrgResolution Resolve(
        long idUB,
        long idStructure,
        IReadOnlyDictionary<long, RapportStructureNode> structuresById);

    IReadOnlyDictionary<long, RapportUbOrgResolution> ResolveAll(
        IReadOnlyList<RapportUbOrgRow> ubs,
        IReadOnlyDictionary<long, RapportStructureNode> structuresById);

    /// <summary>True si idStructure (ou un ancêtre) correspond au nœud cible.</summary>
    bool IsUnderNode(long idStructure, long idNode, IReadOnlyDictionary<long, RapportStructureNode> structuresById);
}

public sealed class RapportOrganisationResolver : IRapportOrganisationResolver
{
    public RapportUbOrgResolution Resolve(
        long idUB,
        long idStructure,
        IReadOnlyDictionary<long, RapportStructureNode> structuresById)
    {
        long? idEntite = null;
        string? codeEntite = null, libEntite = null;
        long? idDept = null;
        string? codeDept = null, libDept = null;
        long? idDiv = null;
        string? codeDiv = null, libDiv = null;

        var currentId = (long?)idStructure;
        var guard = 0;
        while (currentId is long id && guard++ < 30)
        {
            if (!structuresById.TryGetValue(id, out var node)) break;

            var type = node.TypeStructure.Trim().ToUpperInvariant();
            if (type == TypeStructureOrganisationnelle.Division && idDiv is null)
            {
                idDiv = node.IdStructure;
                codeDiv = node.Code;
                libDiv = node.Libelle;
            }
            else if (type == TypeStructureOrganisationnelle.Departement && idDept is null)
            {
                idDept = node.IdStructure;
                codeDept = node.Code;
                libDept = node.Libelle;
            }
            else if (type == TypeStructureOrganisationnelle.Entite && idEntite is null)
            {
                idEntite = node.IdStructure;
                codeEntite = node.Code;
                libEntite = node.Libelle;
            }

            currentId = node.ParentId;
        }

        return new RapportUbOrgResolution(
            idUB,
            idEntite, codeEntite, libEntite,
            idDept, codeDept, libDept,
            idDiv, codeDiv, libDiv,
            idDiv is null);
    }

    public IReadOnlyDictionary<long, RapportUbOrgResolution> ResolveAll(
        IReadOnlyList<RapportUbOrgRow> ubs,
        IReadOnlyDictionary<long, RapportStructureNode> structuresById)
    {
        var map = new Dictionary<long, RapportUbOrgResolution>();
        foreach (var ub in ubs)
        {
            map[ub.IdUB] = Resolve(ub.IdUB, ub.IdStructure, structuresById);
        }

        return map;
    }

    public bool IsUnderNode(long idStructure, long idNode, IReadOnlyDictionary<long, RapportStructureNode> structuresById)
    {
        var currentId = (long?)idStructure;
        var guard = 0;
        while (currentId is long id && guard++ < 30)
        {
            if (id == idNode) return true;
            if (!structuresById.TryGetValue(id, out var node)) break;
            currentId = node.ParentId;
        }

        return false;
    }
}
