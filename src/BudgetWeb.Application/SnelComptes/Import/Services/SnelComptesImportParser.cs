using BudgetWeb.Application.SnelComptes.Import.DTOs;

namespace BudgetWeb.Application.SnelComptes.Import.Services;

public sealed class SnelComptesParsedRb
{
    public int NumeroLigne { get; init; }
    public string CodeExcel { get; init; } = string.Empty;
    public string CodeImport { get; init; } = string.Empty;
    public string Libelle { get; init; } = string.Empty;
    public string? CodeParent { get; init; }
    public int Niveau { get; init; }
}

public sealed class SnelComptesParsedItemBi
{
    public int NumeroLigne { get; init; }
    public string CodeExcel { get; init; } = string.Empty;
    public string CodeImport { get; init; } = string.Empty;
    public string Libelle { get; init; } = string.Empty;
    public string? CodeParent { get; init; }
    public int Niveau { get; init; }
    public string? Categorie { get; init; }
}

public sealed class SnelComptesParseResult
{
    public List<SnelComptesParsedRb> Rubriques { get; } = [];
    public List<SnelComptesParsedItemBi> ItemsBI { get; } = [];
    public List<SnelComptesLigneIgnoreeDto> LignesIgnorees { get; } = [];
    public List<SnelComptesImportAnomalieDto> Anomalies { get; } = [];
}

public static class SnelComptesImportParser
{
    public const string MotifProduitsEnAttente = "IGNORÉS — EN ATTENTE DE DÉCISION MÉTIER";
    public const int LongueurMaxCode = 30;
    public const int LongueurMaxLibelle = 300;
    public const int LongueurMaxCategorie = 200;

    private static readonly HashSet<string> CodesProduits = new(StringComparer.OrdinalIgnoreCase)
    {
        "7021", "7022", "71", "72", "75", "77", "79"
    };

    private static readonly HashSet<string> ParentsProduits = new(StringComparer.OrdinalIgnoreCase)
    {
        "7000", "7100", "7200", "7500", "7700", "7900"
    };

    public static SnelComptesParseResult Parse(IReadOnlyList<SnelComptesExcelRow> rows)
    {
        var result = new SnelComptesParseResult();
        var headerIndex = TrouverIndexEnTete(rows);
        if (headerIndex < 0)
        {
            result.Anomalies.Add(Erreur(
                "ENTETE_INTROUVABLE",
                "L'en-tête Section / Code / Libellé est introuvable sur la feuille Comptes SYSCOHADA.",
                null,
                null));
            return result;
        }

        for (var i = headerIndex + 1; i < rows.Count; i++)
        {
            TraiterLigne(rows[i], result);
        }

        Dedoublonner(result);
        ValiderHierarchieRb(result);
        ValiderHierarchieItems(result);
        ValiderLongueurs(result);

        return result;
    }

    private static int TrouverIndexEnTete(IReadOnlyList<SnelComptesExcelRow> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Section.Equals("Section", StringComparison.OrdinalIgnoreCase)
                && row.Code.Equals("Code", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void TraiterLigne(SnelComptesExcelRow row, SnelComptesParseResult result)
    {
        var section = row.Section.Trim();
        var code = row.Code.Trim();
        var libelle = row.Libelle.Trim();
        var groupe = row.Groupe.Trim();

        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(libelle) && string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        if (EstProduit(code, section))
        {
            result.LignesIgnorees.Add(new SnelComptesLigneIgnoreeDto(
                row.NumeroLigne,
                code,
                libelle,
                MotifProduitsEnAttente));
            result.Anomalies.Add(Info(
                "PRODUIT_IGNORE",
                $"{MotifProduitsEnAttente} : code {code} (ligne {row.NumeroLigne}).",
                row.NumeroLigne,
                code));
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            result.Anomalies.Add(Erreur("CODE_VIDE", "Code vide.", row.NumeroLigne, null));
            return;
        }

        if (string.IsNullOrWhiteSpace(libelle))
        {
            result.Anomalies.Add(Erreur("LIBELLE_VIDE", $"Libellé vide pour le code {code}.", row.NumeroLigne, code));
            return;
        }

        if (EstItemBI(code, section, groupe))
        {
            var codeImport = NormaliserCode(code);
            var estRacine = string.IsNullOrWhiteSpace(section)
                || NormaliserCode(section) == codeImport;
            var categorie = estRacine && !string.IsNullOrWhiteSpace(groupe) ? groupe : null;

            if (!estRacine && string.IsNullOrWhiteSpace(section))
            {
                result.Anomalies.Add(Erreur(
                    "PARENT_INTROUVABLE",
                    $"Item BI {code} sans parent.",
                    row.NumeroLigne,
                    code));
            }

            result.ItemsBI.Add(new SnelComptesParsedItemBi
            {
                NumeroLigne = row.NumeroLigne,
                CodeExcel = code,
                CodeImport = codeImport,
                Libelle = libelle,
                CodeParent = estRacine ? null : NormaliserCode(section),
                Niveau = estRacine ? 0 : 1,
                Categorie = categorie
            });

            SignalerNormalisation(result, row.NumeroLigne, code, codeImport);
            return;
        }

        var codeRb = NormaliserCode(code);
        var estSection = string.IsNullOrWhiteSpace(section) || NormaliserCode(section) == codeRb;
        result.Rubriques.Add(new SnelComptesParsedRb
        {
            NumeroLigne = row.NumeroLigne,
            CodeExcel = code,
            CodeImport = codeRb,
            Libelle = libelle,
            CodeParent = estSection ? null : NormaliserCode(section),
            Niveau = estSection ? 0 : 1
        });
        SignalerNormalisation(result, row.NumeroLigne, code, codeRb);
    }

    private static void Dedoublonner(SnelComptesParseResult result)
    {
        DedoublonnerListe(
            result,
            result.Rubriques,
            item => item.CodeImport,
            (item, motif) => new SnelComptesLigneIgnoreeDto(item.NumeroLigne, item.CodeExcel, item.Libelle, motif),
            item => item.NumeroLigne,
            "RB");

        DedoublonnerListe(
            result,
            result.ItemsBI,
            item => item.CodeImport,
            (item, motif) => new SnelComptesLigneIgnoreeDto(item.NumeroLigne, item.CodeExcel, item.Libelle, motif),
            item => item.NumeroLigne,
            "ITEM_BI");
    }

    private static void DedoublonnerListe<T>(
        SnelComptesParseResult result,
        List<T> items,
        Func<T, string> codeSelector,
        Func<T, string, SnelComptesLigneIgnoreeDto> toIgnore,
        Func<T, int> ligneSelector,
        string referentiel)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<T>();
        foreach (var item in items)
        {
            var code = codeSelector(item);
            if (seen.Add(code))
            {
                kept.Add(item);
                continue;
            }

            var motif = code.Equals("08800", StringComparison.OrdinalIgnoreCase)
                ? "Doublon 08800 : occurrence conservée (première ligne), celle-ci ignorée."
                : $"Code dupliqué dans le fichier ({referentiel}).";
            result.LignesIgnorees.Add(toIgnore(item, motif));

            if (code.Equals("08800", StringComparison.OrdinalIgnoreCase))
            {
                result.Anomalies.Add(Avertissement(
                    "DOUBLON_08800",
                    $"Code 08800 présent plusieurs fois : une seule occurrence sera importée (ligne {ligneSelector(item)} ignorée).",
                    ligneSelector(item),
                    code));
            }
            else
            {
                result.Anomalies.Add(Erreur(
                    "CODE_DUPLIQUE",
                    $"Le code {code} est dupliqué dans le fichier ({referentiel}).",
                    ligneSelector(item),
                    code));
            }
        }

        items.Clear();
        items.AddRange(kept);
    }

    private static void ValiderHierarchieRb(SnelComptesParseResult result)
    {
        var codes = result.Rubriques
            .ToDictionary(r => r.CodeImport, StringComparer.OrdinalIgnoreCase);

        foreach (var rb in result.Rubriques)
        {
            if (rb.CodeParent is null)
            {
                if (rb.Niveau != 0)
                {
                    result.Anomalies.Add(Erreur(
                        "HIERARCHIE_INCOHERENTE",
                        $"La rubrique {rb.CodeImport} est racine mais Niveau={rb.Niveau}.",
                        rb.NumeroLigne,
                        rb.CodeImport));
                }

                continue;
            }

            if (rb.CodeParent.Equals(rb.CodeImport, StringComparison.OrdinalIgnoreCase))
            {
                result.Anomalies.Add(Erreur(
                    "HIERARCHIE_INCOHERENTE",
                    $"La rubrique {rb.CodeImport} se référence elle-même comme parent.",
                    rb.NumeroLigne,
                    rb.CodeImport));
                continue;
            }

            if (!codes.TryGetValue(rb.CodeParent, out var parent))
            {
                result.Anomalies.Add(Erreur(
                    "PARENT_INTROUVABLE",
                    $"Le parent {rb.CodeParent} de la rubrique {rb.CodeImport} est introuvable dans les 64 RB de charges.",
                    rb.NumeroLigne,
                    rb.CodeImport));
                continue;
            }

            if (parent.Niveau != 0 || parent.CodeParent is not null)
            {
                result.Anomalies.Add(Erreur(
                    "HIERARCHIE_INCOHERENTE",
                    $"Le parent {rb.CodeParent} de {rb.CodeImport} n'est pas une section racine.",
                    rb.NumeroLigne,
                    rb.CodeImport));
            }
        }
    }

    private static void ValiderHierarchieItems(SnelComptesParseResult result)
    {
        var codes = result.ItemsBI
            .ToDictionary(i => i.CodeImport, StringComparer.OrdinalIgnoreCase);

        foreach (var item in result.ItemsBI)
        {
            if (item.CodeParent is null)
            {
                continue;
            }

            if (!codes.TryGetValue(item.CodeParent, out var parent))
            {
                result.Anomalies.Add(Erreur(
                    "PARENT_INTROUVABLE",
                    $"Le parent {item.CodeParent} de l'item {item.CodeImport} est introuvable.",
                    item.NumeroLigne,
                    item.CodeImport));
                continue;
            }

            if (parent.Niveau != 0 || parent.CodeParent is not null)
            {
                result.Anomalies.Add(Erreur(
                    "HIERARCHIE_INCOHERENTE",
                    $"Le parent {item.CodeParent} de {item.CodeImport} n'est pas une racine BI.",
                    item.NumeroLigne,
                    item.CodeImport));
            }
        }
    }

    private static void ValiderLongueurs(SnelComptesParseResult result)
    {
        foreach (var rb in result.Rubriques)
        {
            if (rb.CodeImport.Length > LongueurMaxCode)
            {
                result.Anomalies.Add(Erreur(
                    "LONGUEUR_CODE",
                    $"Le code {rb.CodeImport} dépasse {LongueurMaxCode} caractères.",
                    rb.NumeroLigne,
                    rb.CodeImport));
            }

            if (rb.Libelle.Length > LongueurMaxLibelle)
            {
                result.Anomalies.Add(Erreur(
                    "LONGUEUR_LIBELLE",
                    $"Le libellé de {rb.CodeImport} dépasse {LongueurMaxLibelle} caractères.",
                    rb.NumeroLigne,
                    rb.CodeImport));
            }
        }

        foreach (var item in result.ItemsBI)
        {
            if (item.CodeImport.Length > LongueurMaxCode)
            {
                result.Anomalies.Add(Erreur(
                    "LONGUEUR_CODE",
                    $"Le code {item.CodeImport} dépasse {LongueurMaxCode} caractères.",
                    item.NumeroLigne,
                    item.CodeImport));
            }

            if (item.Libelle.Length > LongueurMaxLibelle)
            {
                result.Anomalies.Add(Erreur(
                    "LONGUEUR_LIBELLE",
                    $"Le libellé de {item.CodeImport} dépasse {LongueurMaxLibelle} caractères.",
                    item.NumeroLigne,
                    item.CodeImport));
            }

            if (item.Categorie is { Length: > LongueurMaxCategorie })
            {
                result.Anomalies.Add(Erreur(
                    "LONGUEUR_CATEGORIE",
                    $"La catégorie de {item.CodeImport} dépasse {LongueurMaxCategorie} caractères.",
                    item.NumeroLigne,
                    item.CodeImport));
            }
        }
    }

    private static void SignalerNormalisation(
        SnelComptesParseResult result,
        int ligne,
        string codeExcel,
        string codeImport)
    {
        if (!string.Equals(codeExcel, codeImport, StringComparison.Ordinal))
        {
            result.Anomalies.Add(Info(
                "NORMALISATION_CASSE",
                $"Le code Excel « {codeExcel} » sera stocké « {codeImport} » (Trim + majuscules, contrainte du modèle existant).",
                ligne,
                codeImport));
        }
    }

    public static string NormaliserCode(string code)
        => code.Trim().ToUpperInvariant();

    private static bool EstProduit(string code, string section)
        => CodesProduits.Contains(code) || ParentsProduits.Contains(section);

    private static bool EstItemBI(string code, string section, string groupe)
    {
        if (EstCodeItemBI(code) || EstCodeItemBI(section))
        {
            return true;
        }

        return groupe.Contains("FINANCEMENTS", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstCodeItemBI(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("I.A", StringComparison.OrdinalIgnoreCase)
            || value.Equals("I.B", StringComparison.OrdinalIgnoreCase)
            || value.Equals("II", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("ITEM ", StringComparison.OrdinalIgnoreCase);
    }

    private static SnelComptesImportAnomalieDto Erreur(string code, string message, int? ligne, string? element)
        => new("Error", code, message, ligne, element);

    private static SnelComptesImportAnomalieDto Avertissement(string code, string message, int? ligne, string? element)
        => new("Warning", code, message, ligne, element);

    private static SnelComptesImportAnomalieDto Info(string code, string message, int? ligne, string? element)
        => new("Info", code, message, ligne, element);
}
