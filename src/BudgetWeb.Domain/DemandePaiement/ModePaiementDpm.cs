using BudgetWeb.Domain.Security;

namespace BudgetWeb.Domain.DemandePaiement;

public static class ModePaiementDpm
{
    public const string Caisse = "CAISSE";
    public const string Banque = "BANQUE";

    public static readonly IReadOnlyList<string> Valeurs = [Caisse, Banque];

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? value)
        => Valeurs.Contains(Normaliser(value), StringComparer.Ordinal);
}

public static class TypeInstrumentPaiement
{
    public const string PieceCaisse = "PIECE_CAISSE";
    public const string BonProvisoire = "BON_PROVISOIRE";
    public const string MinuteCheque = "MINUTE_CHEQUE";

    public static readonly IReadOnlyList<string> Valeurs = [PieceCaisse, BonProvisoire, MinuteCheque];

    public static string Normaliser(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? value)
        => Valeurs.Contains(Normaliser(value), StringComparer.Ordinal);

    public static void ExigerCoherence(string modePaiement, string instrument)
    {
        var mode = ModePaiementDpm.Normaliser(modePaiement);
        var inst = Normaliser(instrument);
        if (!ModePaiementDpm.IsValid(mode))
            throw new InvalidOperationException("Le mode de paiement doit être CAISSE ou BANQUE.");
        if (!IsValid(inst))
            throw new InvalidOperationException("Instrument de paiement invalide.");

        if (mode == ModePaiementDpm.Caisse
            && inst is not (PieceCaisse or BonProvisoire))
            throw new InvalidOperationException("En CAISSE, l'instrument doit être PIECE_CAISSE ou BON_PROVISOIRE.");

        if (mode == ModePaiementDpm.Banque && inst != MinuteCheque)
            throw new InvalidOperationException("En BANQUE, l'instrument doit être MINUTE_CHEQUE.");
    }
}

public static class ProfilUtilisateurCodes
{
    /// <summary>Profil historique — conservé pour compatibilité.</summary>
    public const string Demandeur = "DEMANDEUR";
    /// <summary>Profil métier cible (équivalent fonctionnel de <see cref="Demandeur"/>).</summary>
    public const string ServiceDemandeur = "SERVICE_DEMANDEUR";
    /// <summary>Profil cible — responsable du service demandeur.</summary>
    public const string ResponsableServiceDemandeur = "RESPONSABLE_SERVICE_DEMANDEUR";
    /// <summary>Profil cible — responsable d'entité initiatrice.</summary>
    public const string ResponsableEntiteInitiatrice = "RESPONSABLE_ENTITE_INITIATRICE";
    /// <summary>Profil historique — conservé pour compatibilité.</summary>
    public const string ChargeDpm = "CHARGE_DPM";
    /// <summary>Profil métier cible (équivalent fonctionnel de <see cref="ChargeDpm"/>).</summary>
    public const string ChargeDp = "CHARGE_DP";
    public const string GestionnaireJuniorDc = "GESTIONNAIRE_JUNIOR_DC";
    public const string GestionnaireJuniorAe = "GESTIONNAIRE_JUNIOR_AE";
    public const string GestionnaireJuniorBi = "GESTIONNAIRE_JUNIOR_BI";
    /// <summary>Profil métier cible — filière via permissions imputer_dc/ae/bi.</summary>
    public const string GestionnaireJunior = "GESTIONNAIRE_JUNIOR";
    /// <summary>Profil historique — contrôle + visa (à scinder).</summary>
    public const string ControleBudget = "CONTROLE_BUDGET";
    /// <summary>Profil cible — contrôle budgétaire (sans visa).</summary>
    public const string GestionnaireSenior = "GESTIONNAIRE_SENIOR";
    /// <summary>Profil cible — visa / niveau supérieur.</summary>
    public const string ChefDivision = "CHEF_DIVISION";
    /// <summary>Profil cible — Direction des Budgets (autorité, sans admin technique).</summary>
    public const string DirecteurBudgets = "DIRECTEUR_BUDGETS";
    public const string Admin = "ADMIN";

    public static readonly IReadOnlyList<string> Tous =
    [
        Demandeur,
        ServiceDemandeur,
        ResponsableServiceDemandeur,
        ResponsableEntiteInitiatrice,
        ChargeDpm,
        ChargeDp,
        GestionnaireJuniorDc,
        GestionnaireJuniorAe,
        GestionnaireJuniorBi,
        GestionnaireJunior,
        ControleBudget,
        GestionnaireSenior,
        ChefDivision,
        DirecteurBudgets,
        Admin
    ];

    /// <summary>True si profil Service demandeur (cible ou historique DEMANDEUR).</summary>
    public static bool EstServiceDemandeur(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        return c is ServiceDemandeur or Demandeur;
    }

    /// <summary>True si l'un des trois rôles Entité Initiatrice (ou DEMANDEUR historique).</summary>
    public static bool EstEntiteInitiatrice(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        return c is Demandeur
            or ServiceDemandeur
            or ResponsableServiceDemandeur
            or ResponsableEntiteInitiatrice;
    }

    /// <summary>True si responsable service ou responsable entité (pas le simple service demandeur).</summary>
    public static bool EstResponsableEntiteInitiatrice(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        return c is ResponsableServiceDemandeur or ResponsableEntiteInitiatrice;
    }

    /// <summary>True si le code est un profil Chargé DP (cible ou historique).</summary>
    public static bool EstChargeDp(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        return c is ChargeDp or ChargeDpm;
    }

    /// <summary>True si profil Gestionnaire Junior (unifié ou historique DC/AE/BI).</summary>
    public static bool EstGestionnaireJunior(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        return c is GestionnaireJunior
            or GestionnaireJuniorDc
            or GestionnaireJuniorAe
            or GestionnaireJuniorBi;
    }

    /// <summary>
    /// True si le profil participe au contrôle/visa budgétaire
    /// (historique CONTROLE_BUDGET, Senior, Chef de division ou Directeur des Budgets).
    /// </summary>
    public static bool EstControleOuVisaBudget(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        return c is ControleBudget or GestionnaireSenior or ChefDivision or DirecteurBudgets;
    }

    /// <summary>True si profil Directeur des Budgets.</summary>
    public static bool EstDirecteurBudgets(string? code)
        => string.Equals(
            (code ?? string.Empty).Trim(),
            DirecteurBudgets,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True si le profil appartient à la Direction des Budgets
    /// (cible ou historique encore reconnu) — candidat au seed périmètre Tous*.
    /// </summary>
    public static bool EstDirectionBudgets(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        return c is ChargeDp
            or ChargeDpm
            or GestionnaireJunior
            or GestionnaireJuniorDc
            or GestionnaireJuniorAe
            or GestionnaireJuniorBi
            or GestionnaireSenior
            or ChefDivision
            or ControleBudget
            or DirecteurBudgets;
    }

    /// <summary>
    /// Snapshot périmètre recommandé pour un utilisateur Direction des Budgets :
    /// tous départements + toutes UB (équivalent runtime de PeutVoirToutesUb).
    /// </summary>
    public static PerimetreUtilisateurSnapshot PerimetreSeedDirectionBudgets()
        => new(TousDepartements: true, ToutesUnitesBudgetaires: true, [], []);

    /// <summary>
    /// Permission d'imputation à reporter en permission individuelle
    /// lors de la migration d'un profil junior historique vers <see cref="GestionnaireJunior"/>.
    /// </summary>
    public static string? PermissionImputerPourProfilJuniorHistorique(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            GestionnaireJuniorDc => AppPermissions.PaiementsImputerDc,
            GestionnaireJuniorAe => AppPermissions.PaiementsImputerAe,
            GestionnaireJuniorBi => AppPermissions.PaiementsImputerBi,
            _ => null
        };

    public static bool IsValid(string? code)
        => Tous.Contains((code ?? string.Empty).Trim().ToUpperInvariant(), StringComparer.Ordinal)
           || AppRoles.TousLesProfils.Contains(
               (code ?? string.Empty).Trim().ToUpperInvariant(),
               StringComparer.OrdinalIgnoreCase);
}
