using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>
/// Validation du paramétrage comptable requis avant établissement d'un document instrument.
/// </summary>
public static class ParametreInstrumentPaiementRules
{
    /// <summary>True si la ligne est active et tous les champs obligatoires sont renseignés.</summary>
    public static bool EstComplet(ParametreInstrumentPaiement? parametre, string typeInstrument)
    {
        var type = TypeInstrumentPaiement.Normaliser(typeInstrument);
        if (parametre is null || !parametre.Actif)
            return false;

        return ListerChampsObligatoiresManquants(parametre, type).Count == 0;
    }

    /// <summary>
    /// Exige une ligne de paramétrage active et tous les champs obligatoires renseignés.
    /// </summary>
    public static ParametreInstrumentPaiement ExigerParametreComplet(
        ParametreInstrumentPaiement? parametre,
        string typeInstrument)
    {
        var type = TypeInstrumentPaiement.Normaliser(typeInstrument);

        if (parametre is null || !parametre.Actif)
        {
            throw new InvalidOperationException(MessageParametrageNonConfigure(type));
        }

        var manquants = ListerChampsObligatoiresManquants(parametre, type);
        if (manquants.Count > 0)
        {
            throw new InvalidOperationException(MessageParametrageNonConfigure(type));
        }

        return parametre;
    }

    private static string MessageParametrageNonConfigure(string type)
        => TypeInstrumentPaiement.Normaliser(type) switch
        {
            TypeInstrumentPaiement.PieceCaisse =>
                "Le paramétrage de la pièce de caisse n'est pas configuré.",
            TypeInstrumentPaiement.BonProvisoire =>
                "Le paramétrage du bon provisoire n'est pas configuré.",
            TypeInstrumentPaiement.MinuteCheque =>
                "Le paramétrage de la minute de chèque n'est pas configuré.",
            _ => "Le paramétrage de l'instrument de paiement n'est pas configuré.",
        };

    private static List<string> ListerChampsObligatoiresManquants(
        ParametreInstrumentPaiement parametre,
        string type)
    {
        var manquants = new List<string>();

        switch (type)
        {
            case TypeInstrumentPaiement.PieceCaisse:
                ExigerChamp(parametre.Sr, "SR", manquants);
                ExigerChamp(parametre.ComptabiliteGenerale, "Comptabilité générale", manquants);
                ExigerChamp(parametre.Cp, "CP", manquants);
                ExigerChamp(parametre.Cpa, "CPA", manquants);
                ExigerChamp(parametre.NumeroAppariement, "N° d'appariement", manquants);
                ExigerChamp(parametre.RecuInstitutionnel, "Reçu institutionnel", manquants);
                break;

            case TypeInstrumentPaiement.BonProvisoire:
                ExigerChamp(parametre.CompteGeneral, "Compte général", manquants);
                ExigerChamp(parametre.CompteParticulier, "Compte particulier", manquants);
                ExigerChamp(parametre.NumeroAppariement, "N° d'appariement", manquants);
                ExigerChamp(parametre.RecuInstitutionnel, "Reçu institutionnel", manquants);
                break;

            case TypeInstrumentPaiement.MinuteCheque:
                ExigerChamp(parametre.CompteGeneral, "Compte général", manquants);
                ExigerChamp(parametre.CpCa, "CP/CA", manquants);
                ExigerChamp(parametre.Ls, "L/S", manquants);
                ExigerChamp(parametre.SuiviExtraComptable, "Suivi extra-comptable", manquants);
                ExigerChamp(parametre.NumeroAppariement, "N° d'appariement", manquants);
                break;

            default:
                manquants.Add("Type d'instrument");
                break;
        }

        return manquants;
    }

    private static void ExigerChamp(string? value, string label, List<string> manquants)
    {
        if (string.IsNullOrWhiteSpace(value))
            manquants.Add(label);
    }
}
