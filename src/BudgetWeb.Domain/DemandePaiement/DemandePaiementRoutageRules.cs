using BudgetWeb.Domain.Entities;

using BudgetWeb.Domain.Enums;



namespace BudgetWeb.Domain.DemandePaiement;



/// <summary>Règles pures de routage nominatif DPM (Lot 3.3).</summary>

public static class DemandePaiementRoutageRules

{

    public static long? ResoudreIdentiteValidateurN1(DemandePaiementValidation n1)

    {

        if (n1.FK_UtilisateurValidateur is long validateur)

            return validateur;



        if (n1.FK_UtilisateurDeclarant is long declarant)

            return declarant;



        return null;

    }



    public static DemandePaiementRoutage? TrouverDernierRoutageActif(

        IEnumerable<DemandePaiementRoutage> routages)

        => routages

            .Where(r => r.EstActif)

            .OrderByDescending(r => r.DateRoutage)

            .ThenByDescending(r => r.IdRoutage)

            .FirstOrDefault();



    public static DemandePaiementRoutage? TrouverDernierRoutageParAction(

        IEnumerable<DemandePaiementRoutage> routages,

        string action)

        => routages

            .Where(r => string.Equals(r.Action, action, StringComparison.OrdinalIgnoreCase))

            .OrderByDescending(r => r.DateRoutage)

            .ThenByDescending(r => r.IdRoutage)

            .FirstOrDefault();



    /// <summary>Z → Y1 : dernier transmetteur via ORIENTER ou CONTROLER.</summary>

    public static long? ResoudreRetourJuniorVersCharge(IEnumerable<DemandePaiementRoutage> routages)

    {

        var orienter = TrouverDernierRoutageParAction(routages, DemandePaiementRoutageAction.Orienter);

        if (orienter is not null)

            return orienter.FK_UtilisateurSource;



        var controler = TrouverDernierRoutageParAction(routages, DemandePaiementRoutageAction.Controler);

        if (controler is not null)

            return controler.FK_UtilisateurSource;



        var reception = TrouverDernierRoutageParAction(routages, DemandePaiementRoutageAction.Receptionner);

        if (reception is not null)

            return reception.FK_UtilisateurCible;



        var entrer = TrouverDernierRoutageParAction(routages, DemandePaiementRoutageAction.EntrerTraitement);

        if (entrer is not null)

            return entrer.FK_UtilisateurCible;



        return null;

    }



    /// <summary>V → Z : junior/contrôleur cible de la dernière transmission nominative.</summary>

    public static long? ResoudreRetourVisaVersControleur(

        IEnumerable<DemandePaiementRoutage> routages,

        long? utilisateurAssigneCourant)

    {

        var orienter = TrouverDernierRoutageParAction(routages, DemandePaiementRoutageAction.Orienter);

        if (orienter is not null)

            return orienter.FK_UtilisateurCible;



        var controler = TrouverDernierRoutageParAction(routages, DemandePaiementRoutageAction.Controler);

        if (controler is not null)

            return controler.FK_UtilisateurCible;



        return utilisateurAssigneCourant;

    }



    /// <summary>Source réelle pour une prise en charge directe (sans orientation préalable).</summary>

    public static long? ResoudreSourcePourPriseEnCharge(

        Entities.DemandePaiement demande,

        IEnumerable<DemandePaiementRoutage> routages)

    {

        var actif = TrouverDernierRoutageActif(routages);

        if (actif is not null)

            return actif.FK_UtilisateurCible;



        if (demande.FK_UtilisateurAssigne is long assigne)

            return assigne;



        if (demande.FK_UtilisateurReception is long reception)

            return reception;



        return demande.FK_UtilisateurCreation;

    }



    public static bool EstRetourVisa(

        Entities.DemandePaiement demande,

        long utilisateurCourant,

        IReadOnlySet<string> permissions)

    {

        if (demande.FK_UtilisateurAssigne is long assigne && assigne == utilisateurCourant)

            return false;



        return permissions.Contains(Security.AppPermissions.PaiementsViserBudget);

    }



    public static DemandePaiementRoutage NouvelleTransmission(

        long idDemande,

        long source,

        long cible,

        string statutSource,

        string statutCible,

        string action,

        DateTime dateRoutage,

        string? motif = null)

        => new()

        {

            FK_DemandePaiement = idDemande,

            FK_UtilisateurSource = source,

            FK_UtilisateurCible = cible,

            StatutSource = StatutDemandePaiement.Normaliser(statutSource),

            StatutCible = StatutDemandePaiement.Normaliser(statutCible),

            Action = action,

            DateRoutage = dateRoutage,

            EstActif = true,

            Motif = motif,

        };

}


