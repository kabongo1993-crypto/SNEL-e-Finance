namespace BudgetWeb.Application.DTOs.Referentiels;



public record TauxChangeDto(

    long IdTauxChange,

    string DeviseBase,

    string DeviseQuote,

    decimal TauxReference,

    DateOnly DateEffet,

    string Statut,

    DateTime DateCreation,

    long IdUtilisateurCreation,

    string? LibelleUtilisateurCreation,

    DateTime? DateModification,

    long? IdUtilisateurModification,

    string? LibelleUtilisateurModification,

    bool EstModifiable);



public record TauxChangeApplicableDto(

    long? IdTauxChange,

    string DeviseSource,

    string DeviseCible,

    /// <summary>Taux directionnel pour la paire demandée (inverse calculé si besoin).</summary>

    decimal Taux,

    /// <summary>Taux de référence canonique stocké (ex. 1 USD = TauxReference CDF).</summary>

    decimal TauxReference,

    DateOnly DateEffet,

    string Statut,

    bool EstIdentite,

    bool EstInverseCalcule);



public record ConversionResultDto(

    decimal MontantSource,

    string DeviseSource,

    decimal MontantCible,

    string DeviseCible,

    decimal TauxApplique,

    decimal TauxReference,

    long? IdTauxChange,

    bool EstIdentite);



public record ConversionUsdResultDto(

    decimal MontantBrut,

    string DeviseSource,

    decimal TauxConversion,

    decimal TauxReference,

    decimal MontantUsd,

    long? IdTauxChange,

    bool EstIdentite);



public record TauxChangeListQuery(

    string? DeviseBase = null,

    string? DeviseQuote = null,

    string? Statut = null,

    DateOnly? DateEffetMin = null,

    DateOnly? DateEffetMax = null);



public record CreateTauxChangeRequest(

    string DeviseBase,

    string DeviseQuote,

    decimal TauxReference,

    DateOnly DateEffet,

    bool ConfirmerRemplacement = false);



/// <summary>409 — un taux ACTIF existe déjà ; confirmation requise avant écrasement ou clôture.</summary>
public record TauxChangeRemplacementProposeDto(

    string Code,

    string Message,

    long IdTauxExistant,

    string DeviseBase,

    string DeviseQuote,

    decimal TauxReferenceExistant,

    DateOnly DateEffetExistante,

    bool EstUtilise,

    /// <summary>ECRASER ou CLOTURER_ET_CREER</summary>
    string ModePropose);



public record InactivateTauxChangeRequest(string? Motif = null);



public record UpdateTauxChangeRequest(

    decimal TauxReference,

    DateOnly DateEffet);



public record ConvertirTauxChangeRequest(

    decimal MontantSource,

    string DeviseSource,

    string DeviseCible,

    DateOnly DateReference);



public record PaireTauxChangeDto(string DeviseBase, string DeviseQuote, string Libelle);


