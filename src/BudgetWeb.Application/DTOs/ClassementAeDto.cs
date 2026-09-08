namespace BudgetWeb.Application.DTOs;

public record ClassementAeLigneDto(
    long IdClassementAE,
    string TypeLigne,
    long? IdGroupeItemAE,
    string? LibelleGroupe,
    string? LibelleItemAE,
    /// <summary>Groupe déduit des prévisions pour les lignes ITEM ; null pour GROUPE ou sans groupe.</summary>
    long? IdGroupeDeduit,
    string? LibelleGroupeDeduit,
    int OrdreAffichage);

public record ReorderClassementAeRequest(
    long IdVersion,
    long IdUB,
    IReadOnlyList<long> IdsClassementOrdonnes);

public record ClassementAeInitResultDto(
    int CouplesTraites,
    int LignesCreees,
    int CouplesDejaInitialises);
