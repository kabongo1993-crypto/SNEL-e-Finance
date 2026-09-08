using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Évaluation pure d'accès DPM — sans I/O (Lot 3.2).</summary>
public static class DemandePaiementAccesRules
{
    public sealed record Demande(
        string Statut,
        long FK_UtilisateurCreation,
        long? FK_UtilisateurAssigne,
        string? CodeTypeBudget,
        long? FK_UtilisateurRetour = null);

    public sealed record Utilisateur(
        long UserId,
        IReadOnlySet<string> Permissions);

    public static bool EstAccesTransversalAdmin(IReadOnlySet<string> permissions)
        => permissions.Contains(AppPermissions.AdminAll);

    public static bool EstActeurMetierDpm(IReadOnlySet<string> permissions)
        => permissions.Contains(AppPermissions.AdminAll)
           || permissions.Contains(AppPermissions.PaiementsChargeDpm)
           || permissions.Contains(AppPermissions.PaiementsReceptionBudget)
           || permissions.Contains(AppPermissions.PaiementsImputerDc)
           || permissions.Contains(AppPermissions.PaiementsImputerAe)
           || permissions.Contains(AppPermissions.PaiementsImputerBi)
           || permissions.Contains(AppPermissions.PaiementsControlerBudget)
           || permissions.Contains(AppPermissions.PaiementsViserBudget)
           || permissions.Contains(AppPermissions.PaiementsValiderN1)
           || permissions.Contains(AppPermissions.PaiementsValiderN2);

    public static bool EstDemandeurPur(IReadOnlySet<string> permissions)
        => permissions.Contains(AppPermissions.PaiementsLire)
           && !EstActeurMetierDpm(permissions);

    public static bool PeutAcceder(
        Demande demande,
        Utilisateur utilisateur,
        DemandePaiementAccesAction action,
        bool ubAccessible)
    {
        if (!PossedePermissionPourAction(utilisateur.Permissions, action))
            return false;

        if (EstAccesTransversalAdmin(utilisateur.Permissions))
            return ActionCompatibleStatut(action, demande.Statut);

        var statut = StatutDemandePaiement.Normaliser(demande.Statut);
        var userId = utilisateur.UserId;
        var estCreateur = demande.FK_UtilisateurCreation == userId;
        // Après retour A_CORRIGER, l'assigne peut rester sur le créateur en N1/N2 :
        // traiter comme pool (comme un premier envoi) pour les validateurs.
        var assigneEffectif = ResoudreAssigneEffectif(demande.FK_UtilisateurAssigne, demande.FK_UtilisateurCreation, statut);

        if (action == DemandePaiementAccesAction.Lire && estCreateur)
            return true;

        if (assigneEffectif is long assigne && assigne != userId)
        {
            // Visible ≠ Actionnable : la lecture périmètre n'est pas bloquée par l'assignation nominative.
            // Les actions restent soumises à Contournable / pool / ActionCompatibleStatut.
            if (action == DemandePaiementAccesAction.Lire
                && EstVisibleLecturePerimetre(
                    statut,
                    utilisateur.Permissions,
                    demande.CodeTypeBudget,
                    userId,
                    demande.FK_UtilisateurRetour))
            {
                // lecture autorisée si UB ok plus bas
            }
            else if (action == DemandePaiementAccesAction.JoindreDocumentSigne
                     && (estCreateur && ActionCreateurAutorisee(action, statut, utilisateur.Permissions)
                         || EstDansPoolMetier(statut, utilisateur.Permissions, demande.CodeTypeBudget)))
            {
                // Le créateur ou le pool N2 peut joindre/remplacer le document signé
                // sans être le détenteur nominatif de l'assignation.
            }
            else if (!EstAccesAssignationContournable(action, statut, utilisateur.Permissions, demande.CodeTypeBudget))
                return false;
        }
        else if (assigneEffectif is long detenteur && detenteur == userId)
        {
            // Détenteur nominatif — pas de pool métier requis (Lot 3.4).
        }
        else if (assigneEffectif is null && EstDemandeurPur(utilisateur.Permissions))
        {
            return estCreateur && ActionCompatibleStatut(action, demande.Statut);
        }
        else if (action == DemandePaiementAccesAction.Lire)
        {
            // Visible ≠ Actionnable : lecture élargie au périmètre métier, sans élargir les actions.
            if (!EstVisibleLecturePerimetre(
                    statut,
                    utilisateur.Permissions,
                    demande.CodeTypeBudget,
                    userId,
                    demande.FK_UtilisateurRetour))
                return false;
        }
        else if (!EstDansPoolMetier(statut, utilisateur.Permissions, demande.CodeTypeBudget))
        {
            return false;
        }

        if (!ubAccessible && !EstAccesTransversalAdmin(utilisateur.Permissions))
            return false;

        if (estCreateur && action != DemandePaiementAccesAction.Lire
            && ActionCreateurAutorisee(action, statut, utilisateur.Permissions))
            return true;

        if (estCreateur && action != DemandePaiementAccesAction.Lire && EstDemandeurPur(utilisateur.Permissions))
            return ActionCreateurAutorisee(action, statut, utilisateur.Permissions);

        return ActionCompatibleStatut(action, statut);
    }

    /// <summary>
    /// Assignation « collée » au créateur en validation entité = pool (pas un détenteur nominatif).
    /// </summary>
    public static long? ResoudreAssigneEffectif(
        long? fkUtilisateurAssigne,
        long fkUtilisateurCreation,
        string statut)
    {
        var s = StatutDemandePaiement.Normaliser(statut);
        if (fkUtilisateurAssigne is long assigne
            && assigne == fkUtilisateurCreation
            && s is StatutDemandePaiement.EnValidationN1 or StatutDemandePaiement.EnValidationN2)
        {
            return null;
        }

        return fkUtilisateurAssigne;
    }

    private static bool ActionCreateurAutorisee(
        DemandePaiementAccesAction action,
        string statut,
        IReadOnlySet<string> permissions)
    {
        return action switch
        {
            DemandePaiementAccesAction.Modifier => statut is StatutDemandePaiement.Brouillon
                or StatutDemandePaiement.ACorriger,
            DemandePaiementAccesAction.Lire => true,
            DemandePaiementAccesAction.Soumettre
                => statut == StatutDemandePaiement.ValideeEntite,
            DemandePaiementAccesAction.EnvoyerValidation
                => statut == StatutDemandePaiement.Brouillon,
            // Agent demandeur : déclaration physique / pièce signée sur son propre dossier
            // (y compris si FK_UtilisateurAssigne = créateur après un cycle A_CORRIGER).
            DemandePaiementAccesAction.DeclarerValidationPhysique
                => statut is StatutDemandePaiement.EnValidationN1
                    or StatutDemandePaiement.EnValidationN2,
            DemandePaiementAccesAction.JoindreDocumentSigne
                => statut is StatutDemandePaiement.EnValidationN2
                    or StatutDemandePaiement.ValideeEntite
                    or StatutDemandePaiement.Brouillon
                    or StatutDemandePaiement.ACorriger,
            _ when permissions.Contains(AppPermissions.PaiementsSoumettre)
                   && statut == StatutDemandePaiement.ValideeEntite
                   && action == DemandePaiementAccesAction.Lire => true,
            _ => false,
        };
    }

    public static bool PossedePermissionPourAction(
        IReadOnlySet<string> permissions,
        DemandePaiementAccesAction action)
    {
        if (permissions.Contains(AppPermissions.AdminAll))
            return true;

        return action switch
        {
            DemandePaiementAccesAction.Lire => permissions.Contains(AppPermissions.PaiementsLire),
            DemandePaiementAccesAction.Modifier => permissions.Contains(AppPermissions.PaiementsEcrire),
            DemandePaiementAccesAction.Traiter or DemandePaiementAccesAction.Receptionner
                or DemandePaiementAccesAction.Orienter
                => EstChargeDpm(permissions),
            DemandePaiementAccesAction.ValiderN1
                => permissions.Contains(AppPermissions.PaiementsValiderN1),
            DemandePaiementAccesAction.ValiderN2
                => permissions.Contains(AppPermissions.PaiementsValiderN2),
            DemandePaiementAccesAction.ControlerBudget or DemandePaiementAccesAction.PrendreEnCharge
                => permissions.Contains(AppPermissions.PaiementsControlerBudget),
            DemandePaiementAccesAction.ViserBudget
                => permissions.Contains(AppPermissions.PaiementsViserBudget),
            DemandePaiementAccesAction.Retourner
                => EstChargeDpm(permissions)
                   || permissions.Contains(AppPermissions.PaiementsControlerBudget)
                   || permissions.Contains(AppPermissions.PaiementsViserBudget),
            DemandePaiementAccesAction.Imputer => PossedeImputation(permissions),
            DemandePaiementAccesAction.Soumettre
                => permissions.Contains(AppPermissions.PaiementsSoumettre),
            DemandePaiementAccesAction.EnvoyerValidation
                => permissions.Contains(AppPermissions.PaiementsEnvoyerValidation),
            DemandePaiementAccesAction.DeclarerValidationPhysique
                => permissions.Contains(AppPermissions.PaiementsDeclarerValidationPhysique),
            DemandePaiementAccesAction.JoindreDocumentSigne
                => permissions.Contains(AppPermissions.PaiementsJoindreDocumentSigne),
            _ => false,
        };
    }

    public static bool ActionCompatibleStatut(DemandePaiementAccesAction action, string? statut)
    {
        var s = StatutDemandePaiement.Normaliser(statut);
        return action switch
        {
            DemandePaiementAccesAction.Lire => true,
            DemandePaiementAccesAction.Modifier => s is StatutDemandePaiement.Brouillon
                or StatutDemandePaiement.ACorriger,
            DemandePaiementAccesAction.Traiter => s == StatutDemandePaiement.EnTraitementDpm,
            DemandePaiementAccesAction.ValiderN1 => s == StatutDemandePaiement.EnValidationN1,
            DemandePaiementAccesAction.ValiderN2 => s == StatutDemandePaiement.EnValidationN2,
            DemandePaiementAccesAction.Receptionner => s == StatutDemandePaiement.Soumise,
            DemandePaiementAccesAction.Orienter => s == StatutDemandePaiement.EnTraitementDpm,
            DemandePaiementAccesAction.ControlerBudget or DemandePaiementAccesAction.ViserBudget
                => s == StatutDemandePaiement.EnControleBudgetaire,
            DemandePaiementAccesAction.PrendreEnCharge
                => s is StatutDemandePaiement.EnTraitementDpm or StatutDemandePaiement.EnControleBudgetaire,
            DemandePaiementAccesAction.Retourner
                => s is StatutDemandePaiement.EnTraitementDpm or StatutDemandePaiement.EnControleBudgetaire,
            DemandePaiementAccesAction.Imputer => s == StatutDemandePaiement.EnControleBudgetaire,
            DemandePaiementAccesAction.Soumettre => s == StatutDemandePaiement.ValideeEntite,
            DemandePaiementAccesAction.EnvoyerValidation => s == StatutDemandePaiement.Brouillon,
            DemandePaiementAccesAction.DeclarerValidationPhysique
                => s is StatutDemandePaiement.EnValidationN1 or StatutDemandePaiement.EnValidationN2,
            DemandePaiementAccesAction.JoindreDocumentSigne
                => s is StatutDemandePaiement.EnValidationN2
                    or StatutDemandePaiement.ValideeEntite
                    or StatutDemandePaiement.Brouillon
                    or StatutDemandePaiement.ACorriger,
            _ => false,
        };
    }

    private static bool EstDansPoolMetier(
        string statut,
        IReadOnlySet<string> permissions,
        string? codeTypeBudget)
    {
        if (statut == StatutDemandePaiement.EnValidationN1
            && (permissions.Contains(AppPermissions.PaiementsValiderN1)
                || permissions.Contains(AppPermissions.PaiementsDeclarerValidationPhysique)))
            return true;

        if (statut == StatutDemandePaiement.EnValidationN2
            && (permissions.Contains(AppPermissions.PaiementsValiderN2)
                || permissions.Contains(AppPermissions.PaiementsDeclarerValidationPhysique)
                || permissions.Contains(AppPermissions.PaiementsJoindreDocumentSigne)))
            return true;

        if (EstChargeDpm(permissions)
            && statut is StatutDemandePaiement.Soumise or StatutDemandePaiement.EnTraitementDpm)
            return true;

        if (statut == StatutDemandePaiement.EnControleBudgetaire
            && (PossedeImputation(permissions)
                || permissions.Contains(AppPermissions.PaiementsControlerBudget)
                || permissions.Contains(AppPermissions.PaiementsViserBudget)))
        {
            return FiliereCompatible(codeTypeBudget, permissions);
        }

        return false;
    }

    /// <summary>
    /// Visibilité lecture périmètre (compteurs / listes) — plus large que le pool actionnable.
    /// Intervenants du circuit : tous les statuts sauf BROUILLON (réservé au saisisseur).
    /// Chargé DP : A_CORRIGER uniquement s'il est l'auteur du retour (pas le circuit N1/N2).
    /// </summary>
    private static bool EstVisibleLecturePerimetre(
        string statut,
        IReadOnlySet<string> permissions,
        string? codeTypeBudget,
        long userId,
        long? fkUtilisateurRetour)
    {
        if (EstDansPoolMetier(statut, permissions, codeTypeBudget))
            return true;

        if (statut == StatutDemandePaiement.Brouillon)
            return false;

        if (!EstActeurMetierDpm(permissions))
            return false;

        if (statut == StatutDemandePaiement.ACorriger && EstChargeDpm(permissions))
            return fkUtilisateurRetour == userId;

        if ((statut is StatutDemandePaiement.EnControleBudgetaire
                or StatutDemandePaiement.ViseeBudgetairement)
            && PossedeImputation(permissions)
            && !EstChargeDpm(permissions)
            && !permissions.Contains(AppPermissions.PaiementsControlerBudget)
            && !permissions.Contains(AppPermissions.PaiementsViserBudget))
        {
            return FiliereCompatible(codeTypeBudget, permissions);
        }

        return StatutDemandePaiement.IsValid(statut);
    }

    private static bool EstChargeDpm(IReadOnlySet<string> permissions)
        => permissions.Contains(AppPermissions.PaiementsChargeDpm)
           || permissions.Contains(AppPermissions.PaiementsReceptionBudget);

    private static bool PossedeImputation(IReadOnlySet<string> permissions)
        => permissions.Contains(AppPermissions.PaiementsImputerDc)
           || permissions.Contains(AppPermissions.PaiementsImputerAe)
           || permissions.Contains(AppPermissions.PaiementsImputerBi);

    private static bool EstAccesAssignationContournable(
        DemandePaiementAccesAction action,
        string statut,
        IReadOnlySet<string> permissions,
        string? codeTypeBudget)
    {
        if (action == DemandePaiementAccesAction.PrendreEnCharge
            && statut == StatutDemandePaiement.EnTraitementDpm
            && permissions.Contains(AppPermissions.PaiementsControlerBudget))
        {
            return FiliereCompatible(codeTypeBudget, permissions);
        }

        if (action == DemandePaiementAccesAction.Retourner
            && statut == StatutDemandePaiement.EnControleBudgetaire
            && permissions.Contains(AppPermissions.PaiementsViserBudget))
        {
            return true;
        }

        return false;
    }

    private static bool FiliereCompatible(string? codeType, IReadOnlySet<string> permissions)
    {
        if (PossedeImputation(permissions))
        {
            if (string.IsNullOrWhiteSpace(codeType))
                return false;

            var code = codeType.Trim().ToUpperInvariant();
            return code switch
            {
                TypeBudgetCode.DepensesCourantes => permissions.Contains(AppPermissions.PaiementsImputerDc),
                TypeBudgetCode.ActionsExploitation => permissions.Contains(AppPermissions.PaiementsImputerAe),
                TypeBudgetCode.BudgetInvestissement => permissions.Contains(AppPermissions.PaiementsImputerBi),
                _ => false,
            };
        }

        return permissions.Contains(AppPermissions.PaiementsControlerBudget)
               || permissions.Contains(AppPermissions.PaiementsViserBudget);
    }
}
