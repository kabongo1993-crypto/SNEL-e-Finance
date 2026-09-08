namespace BudgetWeb.Domain.Security;

/// <summary>
/// Rôles / profils applicatifs Budget Web.
/// BD_SNEL n'a pas de table ROLE : les profils vivent dans dpm.PROFIL_UTILISATEUR ;
/// les matricules conventionnels (<see cref="MatriculeAdminFull"/>, <see cref="MatriculeDg"/>)
/// restent supportés pour la rétrocompatibilité.
/// </summary>
public static class AppRoles
{
    public const string UserAdminFull = "User Admin Full";
    public const string UserDg = "DG";

    // --- Profils historiques (conservés) ---
    public const string Demandeur = "DEMANDEUR";
    public const string ChargeDpm = "CHARGE_DPM";
    public const string GestionnaireJuniorDc = "GESTIONNAIRE_JUNIOR_DC";
    public const string GestionnaireJuniorAe = "GESTIONNAIRE_JUNIOR_AE";
    public const string GestionnaireJuniorBi = "GESTIONNAIRE_JUNIOR_BI";
    public const string ControleBudget = "CONTROLE_BUDGET";
    public const string Admin = "ADMIN";

    // --- Profils métier cibles (ajoutés, coexistent avec l'historique) ---
    public const string ServiceDemandeur = "SERVICE_DEMANDEUR";
    public const string ResponsableServiceDemandeur = "RESPONSABLE_SERVICE_DEMANDEUR";
    public const string ResponsableEntiteInitiatrice = "RESPONSABLE_ENTITE_INITIATRICE";
    public const string ChargeDp = "CHARGE_DP";
    public const string GestionnaireJunior = "GESTIONNAIRE_JUNIOR";
    public const string GestionnaireSenior = "GESTIONNAIRE_SENIOR";
    public const string ChefDivision = "CHEF_DIVISION";
    public const string DirecteurBudgets = "DIRECTEUR_BUDGETS";
    public const string AdministrateurSysteme = "ADMINISTRATEUR_SYSTEME";

    /// <summary>Valeur stockée dans UTILISATEUR.Matricule pour identifier l'admin full.</summary>
    public const string MatriculeAdminFull = "ADMIN-FULL";

    /// <summary>Valeur stockée dans UTILISATEUR.Matricule pour identifier la Direction Générale.</summary>
    public const string MatriculeDg = "DG";

    /// <summary>Tous les codes profil acceptés (historiques + cibles).</summary>
    public static readonly IReadOnlyList<string> TousLesProfils =
    [
        Demandeur,
        ChargeDpm,
        GestionnaireJuniorDc,
        GestionnaireJuniorAe,
        GestionnaireJuniorBi,
        ControleBudget,
        Admin,
        ServiceDemandeur,
        ResponsableServiceDemandeur,
        ResponsableEntiteInitiatrice,
        ChargeDp,
        GestionnaireJunior,
        GestionnaireSenior,
        ChefDivision,
        DirecteurBudgets,
        AdministrateurSysteme
    ];
}

/// <summary>
/// Permissions applicatives (claims) — pas de table PERMISSION en base.
/// Convention : module.action
/// </summary>
public static class AppPermissions
{
    public const string AdminAll = "admin.all";
    public const string AdminUtilisateurs = "admin.utilisateurs";
    public const string AdminProfils = "admin.profils";

    public const string PrevisionsEcrire = "previsions.ecrire";
    public const string PrevisionsSoumettre = "previsions.soumettre";
    public const string ReferentielsEcrire = "referentiels.ecrire";
    /// <summary>
    /// Créer / modifier le référentiel Demandeur (DPM) — distinct de <see cref="ReferentielsEcrire"/>
    /// (structures, UB, RB, exercices…).
    /// </summary>
    public const string DemandeursEcrire = "demandeurs.ecrire";
    public const string VersionsEcrire = "versions.ecrire";
    public const string VersionsControler = "versions.controler";
    public const string VersionsValider = "versions.valider";
    public const string VersionsRejeter = "versions.rejeter";
    public const string AjustementsLire = "ajustements.lire";
    public const string AjustementsEcrire = "ajustements.ecrire";
    public const string AjustementsValider = "ajustements.valider";
    public const string PaiementsLire = "paiements.lire";
    public const string PaiementsEcrire = "paiements.ecrire";
    public const string PaiementsSoumettre = "paiements.soumettre";
    /// <summary>Envoyer une DPM en validation entité (N1).</summary>
    public const string PaiementsEnvoyerValidation = "paiements.envoyer_validation";
    /// <summary>Valider électroniquement au niveau 1 (Responsable service).</summary>
    public const string PaiementsValiderN1 = "paiements.valider_n1";
    /// <summary>Valider électroniquement au niveau 2 (Responsable entité).</summary>
    public const string PaiementsValiderN2 = "paiements.valider_n2";
    /// <summary>Déclarer une validation physique (agent).</summary>
    public const string PaiementsDeclarerValidationPhysique = "paiements.declarer_validation_physique";
    /// <summary>Rejeter / retourner une DPM en validation entité.</summary>
    public const string PaiementsRejeterValidationEntite = "paiements.rejeter_validation_entite";
    /// <summary>Imprimer / générer le document DPM.</summary>
    public const string PaiementsImprimer = "paiements.imprimer";
    /// <summary>Joindre le document DPM signé physiquement.</summary>
    public const string PaiementsJoindreDocumentSigne = "paiements.joindre_document_signe";
    public const string PaiementsChargeDpm = "paiements.charge_dpm";
    /// <summary>Alias historique de <see cref="PaiementsChargeDpm"/> (réception budget).</summary>
    public const string PaiementsReceptionBudget = "paiements.reception_budget";
    /// <summary>
    /// Permission du Chargé DP — inchangée pendant la migration de profil
    /// <c>CHARGE_DPM</c> → <c>CHARGE_DP</c> (les deux profils la reçoivent).
    /// </summary>
    public const string PaiementsChargeDp = PaiementsChargeDpm;
    public const string PaiementsImputerDc = "paiements.imputer_dc";
    public const string PaiementsImputerAe = "paiements.imputer_ae";
    public const string PaiementsImputerBi = "paiements.imputer_bi";
    public const string PaiementsControlerBudget = "paiements.controler_budget";
    public const string PaiementsViserBudget = "paiements.viser_budget";
    /// <summary>Permission complémentaire : reprise DP entité initiatrice (non auto pour CHARGE_DP).</summary>
    public const string PaiementsReprendreEntite = "paiements.reprendre_entite";

    public static readonly IReadOnlyList<string> Demandeur =
    [
        PaiementsLire,
        PaiementsEcrire,
        PaiementsEnvoyerValidation,
        PaiementsDeclarerValidationPhysique,
        PaiementsImprimer,
        PaiementsJoindreDocumentSigne,
        PaiementsSoumettre,
        DemandeursEcrire
    ];

    /// <summary>
    /// Service demandeur (cible) — saisie, envoi validation, déclaration physique, soumission Budget.
    /// </summary>
    public static readonly IReadOnlyList<string> ServiceDemandeur = Demandeur;

    /// <summary>Responsable du service demandeur — validation N1 électronique.</summary>
    public static readonly IReadOnlyList<string> ResponsableServiceDemandeur =
    [
        PaiementsLire,
        PaiementsValiderN1,
        PaiementsRejeterValidationEntite,
        PaiementsImprimer
    ];

    /// <summary>Responsable d'entité initiatrice — validation N2 électronique.</summary>
    public static readonly IReadOnlyList<string> ResponsableEntiteInitiatrice =
    [
        PaiementsLire,
        PaiementsValiderN2,
        PaiementsRejeterValidationEntite,
        PaiementsImprimer
    ];

    /// <summary>Alias historique de <see cref="ResponsableEntiteInitiatrice"/>.</summary>
    public static readonly IReadOnlyList<string> ResponsableEntite = ResponsableEntiteInitiatrice;

    /// <summary>
    /// Permissions que les rôles Entité Initiatrice ne doivent pas hériter du profil.
    /// </summary>
    public static readonly IReadOnlyList<string> EntiteInitiatriceExclusions =
    [
        AdminAll,
        AdminUtilisateurs,
        AdminProfils,
        ReferentielsEcrire,
        // DemandeursEcrire volontairement absent : les profils entité initiatrice l'héritent.
        VersionsEcrire,
        VersionsControler,
        VersionsValider,
        VersionsRejeter,
        PrevisionsEcrire,
        PrevisionsSoumettre,
        PaiementsChargeDpm,
        PaiementsReceptionBudget,
        PaiementsImputerDc,
        PaiementsImputerAe,
        PaiementsImputerBi,
        PaiementsControlerBudget,
        PaiementsViserBudget,
        PaiementsReprendreEntite,
        AjustementsEcrire,
        AjustementsValider
    ];

    public static readonly IReadOnlyList<string> ChargeDpm =
    [
        PaiementsLire,
        PaiementsEcrire,
        PaiementsChargeDpm,
        PaiementsReceptionBudget
    ];

    public static readonly IReadOnlyList<string> JuniorDc =
    [
        PaiementsLire,
        PaiementsImputerDc,
        PaiementsControlerBudget
    ];

    public static readonly IReadOnlyList<string> JuniorAe =
    [
        PaiementsLire,
        PaiementsImputerAe,
        PaiementsControlerBudget
    ];

    public static readonly IReadOnlyList<string> JuniorBi =
    [
        PaiementsLire,
        PaiementsImputerBi,
        PaiementsControlerBudget
    ];

    /// <summary>Junior unifié : filière via permissions individuelles imputer_*.</summary>
    public static readonly IReadOnlyList<string> Junior =
    [
        PaiementsLire,
        PaiementsControlerBudget
    ];

    public static readonly IReadOnlyList<string> ControleBudget =
    [
        PaiementsLire,
        PaiementsControlerBudget,
        PaiementsViserBudget
    ];

    /// <summary>Préparation — contrôle budgétaire sans visa.</summary>
    public static readonly IReadOnlyList<string> GestionnaireSenior =
    [
        PaiementsLire,
        PaiementsControlerBudget
    ];

    /// <summary>Préparation — visa budgétaire / niveau supérieur.</summary>
    public static readonly IReadOnlyList<string> ChefDivision =
    [
        PaiementsLire,
        PaiementsViserBudget
    ];

    /// <summary>
    /// Équivalence CONTROLE_BUDGET après scission :
    /// Senior (contrôle) + Chef Division (visa) = lire + controler + viser.
    /// </summary>
    public static IReadOnlyList<string> PermissionsEffectivesApresScissionControleBudget()
        => EffectivePermissions.FromProfils([AppRoles.GestionnaireSenior, AppRoles.ChefDivision]);

    /// <summary>
    /// Directeur des Budgets — autorité métier Direction Budgets.
    /// JAMAIS <c>admin.all</c> / admin utilisateurs / référentiels techniques
    /// (réservés à <see cref="AppRoles.AdministrateurSysteme"/>).
    /// Distinct de :
    /// - <see cref="GestionnaireSenior"/> (contrôle opérationnel),
    /// - <see cref="ChefDivision"/> (visa seul — le Directeur a aussi le visa + versions),
    /// - matricule DG (ajustements écrire/valider — non repris ici).
    /// </summary>
    public static readonly IReadOnlyList<string> DirecteurBudgets =
    [
        PaiementsLire,
        PaiementsViserBudget,
        VersionsControler,
        VersionsValider,
        VersionsRejeter,
        AjustementsLire
    ];

    /// <summary>
    /// Garde-fous : le Directeur des Budgets ne doit jamais recevoir ces permissions via le profil.
    /// </summary>
    public static readonly IReadOnlyList<string> DirecteurBudgetsExclusions =
    [
        AdminAll,
        AdminUtilisateurs,
        AdminProfils,
        ReferentielsEcrire,
        DemandeursEcrire,
        VersionsEcrire,
        PrevisionsEcrire,
        PrevisionsSoumettre,
        PaiementsEcrire,
        PaiementsSoumettre,
        PaiementsChargeDpm,
        PaiementsReceptionBudget,
        PaiementsImputerDc,
        PaiementsImputerAe,
        PaiementsImputerBi,
        PaiementsControlerBudget,
        PaiementsReprendreEntite,
        AjustementsEcrire,
        AjustementsValider
    ];

    public static readonly IReadOnlyList<string> Dg =
    [
        AjustementsLire,
        AjustementsEcrire,
        AjustementsValider
    ];

    public static readonly IReadOnlyList<string> AdminFull =
    [
        AdminAll,
        AdminUtilisateurs,
        AdminProfils,
        PrevisionsEcrire,
        PrevisionsSoumettre,
        ReferentielsEcrire,
        DemandeursEcrire,
        VersionsEcrire,
        VersionsControler,
        VersionsValider,
        VersionsRejeter,
        AjustementsLire,
        AjustementsEcrire,
        AjustementsValider,
        PaiementsLire,
        PaiementsEcrire,
        PaiementsSoumettre,
        PaiementsEnvoyerValidation,
        PaiementsValiderN1,
        PaiementsValiderN2,
        PaiementsDeclarerValidationPhysique,
        PaiementsRejeterValidationEntite,
        PaiementsImprimer,
        PaiementsJoindreDocumentSigne,
        PaiementsChargeDpm,
        PaiementsReceptionBudget,
        PaiementsImputerDc,
        PaiementsImputerAe,
        PaiementsImputerBi,
        PaiementsControlerBudget,
        PaiementsViserBudget,
        PaiementsReprendreEntite
    ];

    /// <summary>
    /// Permissions pouvant être accordées individuellement (hors héritage profil).
    /// </summary>
    public static readonly IReadOnlyList<string> PermissionsComplementaires =
    [
        PaiementsReprendreEntite,
        PaiementsImputerDc,
        PaiementsImputerAe,
        PaiementsImputerBi,
        PaiementsControlerBudget,
        PaiementsViserBudget,
        PaiementsChargeDpm,
        PaiementsReceptionBudget,
        PaiementsLire,
        PaiementsEcrire,
        PaiementsSoumettre,
        PaiementsEnvoyerValidation,
        PaiementsValiderN1,
        PaiementsValiderN2,
        PaiementsDeclarerValidationPhysique,
        PaiementsRejeterValidationEntite,
        PaiementsImprimer,
        PaiementsJoindreDocumentSigne,
        DemandeursEcrire,
        PrevisionsEcrire,
        PrevisionsSoumettre,
        VersionsControler,
        VersionsValider,
        VersionsRejeter,
        AjustementsLire,
        AjustementsEcrire,
        AjustementsValider,
        ReferentielsEcrire,
        AdminUtilisateurs,
        AdminProfils
    ];

    /// <summary>Catalogue complet des permissions connues.</summary>
    public static readonly IReadOnlyList<string> Toutes =
    [
        AdminAll,
        AdminUtilisateurs,
        AdminProfils,
        PrevisionsEcrire,
        PrevisionsSoumettre,
        ReferentielsEcrire,
        DemandeursEcrire,
        VersionsEcrire,
        VersionsControler,
        VersionsValider,
        VersionsRejeter,
        AjustementsLire,
        AjustementsEcrire,
        AjustementsValider,
        PaiementsLire,
        PaiementsEcrire,
        PaiementsSoumettre,
        PaiementsEnvoyerValidation,
        PaiementsValiderN1,
        PaiementsValiderN2,
        PaiementsDeclarerValidationPhysique,
        PaiementsRejeterValidationEntite,
        PaiementsImprimer,
        PaiementsJoindreDocumentSigne,
        PaiementsChargeDpm,
        PaiementsReceptionBudget,
        PaiementsImputerDc,
        PaiementsImputerAe,
        PaiementsImputerBi,
        PaiementsControlerBudget,
        PaiementsViserBudget,
        PaiementsReprendreEntite
    ];

    public static IReadOnlyList<string> PermissionsPourProfil(string codeProfil)
        => codeProfil.Trim().ToUpperInvariant() switch
        {
            "DEMANDEUR" => Demandeur,
            "SERVICE_DEMANDEUR" => ServiceDemandeur,
            "RESPONSABLE_SERVICE_DEMANDEUR" => ResponsableServiceDemandeur,
            "RESPONSABLE_ENTITE_INITIATRICE" => ResponsableEntiteInitiatrice,
            "CHARGE_DPM" => ChargeDpm,
            "CHARGE_DP" => ChargeDpm,
            "GESTIONNAIRE_JUNIOR_DC" => JuniorDc,
            "GESTIONNAIRE_JUNIOR_AE" => JuniorAe,
            "GESTIONNAIRE_JUNIOR_BI" => JuniorBi,
            "GESTIONNAIRE_JUNIOR" => Junior,
            "CONTROLE_BUDGET" => ControleBudget,
            "GESTIONNAIRE_SENIOR" => GestionnaireSenior,
            "CHEF_DIVISION" => ChefDivision,
            "DIRECTEUR_BUDGETS" => DirecteurBudgets,
            "ADMIN" => AdminFull,
            "ADMINISTRATEUR_SYSTEME" => AdminFull,
            _ => []
        };

    public static string? LibelleProfil(string codeProfil)
        => codeProfil.Trim().ToUpperInvariant() switch
        {
            "DEMANDEUR" => "Demandeur (historique)",
            "SERVICE_DEMANDEUR" => "Service demandeur",
            "RESPONSABLE_SERVICE_DEMANDEUR" => "Responsable service demandeur",
            "RESPONSABLE_ENTITE_INITIATRICE" => "Responsable entité initiatrice",
            "CHARGE_DPM" => "Chargé DPM (historique)",
            "CHARGE_DP" => "Chargé DP",
            "GESTIONNAIRE_JUNIOR_DC" => "Gestionnaire junior DC (historique)",
            "GESTIONNAIRE_JUNIOR_AE" => "Gestionnaire junior AE (historique)",
            "GESTIONNAIRE_JUNIOR_BI" => "Gestionnaire junior BI (historique)",
            "GESTIONNAIRE_JUNIOR" => "Gestionnaire junior",
            "CONTROLE_BUDGET" => "Contrôle budget (historique)",
            "GESTIONNAIRE_SENIOR" => "Gestionnaire senior",
            "CHEF_DIVISION" => "Chef de division",
            "DIRECTEUR_BUDGETS" => "Directeur des Budgets",
            "ADMIN" => "Administrateur (historique)",
            "ADMINISTRATEUR_SYSTEME" => "Administrateur système",
            _ => null
        };

    public static string? DescriptionPermission(string code)
        => code.Trim().ToLowerInvariant() switch
        {
            "admin.all" => "Administration technique globale",
            "admin.utilisateurs" => "Gestion des utilisateurs",
            "admin.profils" => "Consultation des profils et permissions",
            "paiements.reprendre_entite" => "Reprendre une DP provenant d'une entité initiatrice",
            "paiements.charge_dpm" => "Traitement Chargé DP",
            "paiements.reception_budget" => "Réception budget (alias charge_dpm)",
            "paiements.imputer_dc" => "Imputer filière DC",
            "paiements.imputer_ae" => "Imputer filière AE",
            "paiements.imputer_bi" => "Imputer filière BI",
            "paiements.controler_budget" => "Contrôle budgétaire",
            "paiements.viser_budget" => "Visa budgétaire",
            "paiements.lire" => "Lire les demandes de paiement",
            "paiements.ecrire" => "Créer / modifier les demandes de paiement",
            "paiements.soumettre" => "Soumettre une demande de paiement au Budget",
            "paiements.envoyer_validation" => "Envoyer une DPM en validation entité (N1)",
            "paiements.valider_n1" => "Valider électroniquement une DPM (niveau 1)",
            "paiements.valider_n2" => "Valider électroniquement une DPM (niveau 2)",
            "paiements.declarer_validation_physique" => "Déclarer une validation physique",
            "paiements.rejeter_validation_entite" => "Rejeter une DPM en validation entité",
            "paiements.imprimer" => "Imprimer / générer le document DPM",
            "paiements.joindre_document_signe" => "Joindre le document DPM signé physiquement",
            "previsions.ecrire" => "Saisir les prévisions",
            "previsions.soumettre" => "Soumettre les prévisions",
            "versions.controler" => "Contrôler une version / soumission",
            "versions.valider" => "Valider une version",
            "versions.rejeter" => "Rejeter une version",
            "versions.ecrire" => "Gérer les versions budgétaires",
            "ajustements.lire" => "Lire les ajustements",
            "ajustements.ecrire" => "Créer / modifier les ajustements",
            "ajustements.valider" => "Valider les ajustements",
            "referentiels.ecrire" => "Écrire dans les référentiels (structures, UB, RB…)",
            "demandeurs.ecrire" => "Créer / modifier les demandeurs (DPM)",
            _ => null
        };

    public static bool EstPermissionComplementaire(string code)
        => PermissionsComplementaires.Contains(code.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool EstProfilConnu(string code)
        => AppRoles.TousLesProfils.Contains(code.Trim(), StringComparer.OrdinalIgnoreCase);
}
