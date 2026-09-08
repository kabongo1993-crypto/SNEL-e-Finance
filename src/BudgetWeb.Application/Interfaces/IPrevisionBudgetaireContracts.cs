using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IPrevisionBudgetaireRepository
{
    Task<IReadOnlyList<PrevisionBudgetaireDto>> GetByFiltresAsync(
        long? idVersion,
        long? idTypeBudget,
        long? idUB,
        long? idModePrevision,
        string? libelleItemAE,
        long? idItemBI,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chargement grille : projection SQL légère (montants + RB + répartitions optionnelles).
    /// Évite le graphe Include Version/Type/Mode/UB/Utilisateur.
    /// </summary>
    Task<IReadOnlyList<PrevisionGrilleSourceDto>> GetForGrilleAsync(
        long idVersion,
        long idTypeBudget,
        long idUB,
        string? libelleItemAE,
        long? idItemBI,
        bool includeRepartitions,
        CancellationToken cancellationToken = default);

    Task<PrevisionBudgetaireDto?> GetByIdAsync(long idPrevision, CancellationToken cancellationToken = default);

    Task<(string Statut, bool Exists)> GetVersionStatutAsync(long idVersion, CancellationToken cancellationToken = default);

    Task<(string CodeType, string Libelle, bool Exists, bool Actif)> GetTypeBudgetAsync(
        long idTypeBudget,
        CancellationToken cancellationToken = default);

    Task<(string CodeMode, string Libelle, bool Exists, bool Actif)> GetModePrevisionAsync(
        long idModePrevision,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsUBAsync(long idUB, CancellationToken cancellationToken = default);

    Task<bool> ExistsRBAsync(long idRB, CancellationToken cancellationToken = default);

    Task<bool> ExistsItemBIAsync(long idItemBI, CancellationToken cancellationToken = default);

    Task<bool> ExistsGroupeItemAEAsync(long idGroupeItemAE, CancellationToken cancellationToken = default);

    Task<bool> ExistsUtilisateurAsync(long idUtilisateur, CancellationToken cancellationToken = default);

    Task<(string CodeUB, string LibelleUB)?> GetUBInfoAsync(long idUB, CancellationToken cancellationToken = default);

    Task<(string? CodeItem, string? LibelleItem)?> GetItemBIInfoAsync(long idItemBI, CancellationToken cancellationToken = default);

    /// <summary>Retrouve l'id d'une prévision DC existante (Version+UB+RB), indépendamment du mode.</summary>
    Task<long?> FindIdDcAsync(long idVersion, long idUB, long idRB, CancellationToken cancellationToken = default);

    /// <summary>Retrouve l'id d'une prévision AE existante (Version+UB+RB+Action), indépendamment du mode.</summary>
    Task<long?> FindIdAeAsync(
        long idVersion,
        long idUB,
        long idRB,
        string libelleItemAE,
        CancellationToken cancellationToken = default);

    /// <summary>Retrouve l'id d'une prévision BI existante (Version+UB+Item+Detail), indépendamment du mode.</summary>
    Task<long?> FindIdBiAsync(
        long idVersion,
        long idUB,
        long idItemBI,
        string detailBI,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(
        long IdRB,
        string CodeRB,
        string Libelle,
        long? ParentId,
        int Niveau,
        bool Actif,
        long? IdGroupeRB,
        string? CodeGroupe,
        string? LibelleGroupe,
        int? OrdreAffichageGroupe)>> GetRubriquesActivesAsync(
        CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    Task<PrevisionBudgetaireDto> CreateAsync(
        long idVersion,
        long idTypeBudget,
        long idModePrevision,
        long idUB,
        long? idRB,
        long? idItemBI,
        long? idGroupeItemAE,
        string? libelleItemAE,
        string? detailBI,
        decimal montantAnnuel,
        long idUtilisateurCreation,
        IReadOnlyList<RepartitionMensuelleDto> repartitions,
        CancellationToken cancellationToken = default);

    Task<PrevisionBudgetaireDto?> UpdateAsync(
        long idPrevision,
        long? idGroupeItemAE,
        string? libelleItemAE,
        string? detailBI,
        decimal montantAnnuel,
        long idUtilisateurModification,
        IReadOnlyList<RepartitionMensuelleDto>? repartitions,
        bool remplacerRepartitions,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idPrevision, CancellationToken cancellationToken = default);

    Task RemplacerRepartitionsAsync(
        long idPrevision,
        IReadOnlyList<RepartitionMensuelleDto> repartitions,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrevisionResumeCategorieDto>> GetResumeParTypeAsync(
        long idVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Libellés d'actions d'exploitation déjà utilisés (distincts), pour combo de réutilisation multi-UB.
    /// Filtre par version et/ou exercice (au moins un des deux).
    /// </summary>
    Task<IReadOnlyList<string>> ListLibellesItemAEAsync(
        long? idVersion,
        long? idExercice,
        CancellationToken cancellationToken = default);
}

public interface IPrevisionBudgetaireService
{
    Task<IReadOnlyList<PrevisionBudgetaireDto>> GetByFiltresAsync(
        long? idVersion,
        long? idTypeBudget,
        long? idUB,
        long? idModePrevision,
        string? libelleItemAE,
        long? idItemBI,
        CancellationToken cancellationToken = default);

    Task<PrevisionBudgetaireDto?> GetByIdAsync(long idPrevision, CancellationToken cancellationToken = default);

    Task<PrevisionGrilleDto> GetGrilleAsync(
        long idVersion,
        long idTypeBudget,
        long idModePrevision,
        long idUB,
        string? libelleItemAE,
        long? idGroupeItemAE,
        long? idItemBI,
        CancellationToken cancellationToken = default);

    Task<PrevisionGrillePageDto> GetGrillePageAsync(
        long idVersion,
        long idTypeBudget,
        long idModePrevision,
        long idUB,
        string? libelleItemAE,
        long? idGroupeItemAE,
        long? idItemBI,
        int page,
        int pageSize,
        string? search,
        string? filtre,
        long? idGroupeRB = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrevisionResumeCategorieDto>> GetResumeAsync(
        long idVersion,
        CancellationToken cancellationToken = default);

    /// <summary>Actions d'exploitation existantes (distinctes) pour combo de saisie.</summary>
    Task<IReadOnlyList<string>> ListLibellesItemAEAsync(
        long? idVersion,
        long? idExercice,
        CancellationToken cancellationToken = default);

    Task<PrevisionBudgetaireDto> CreateAsync(
        CreatePrevisionBudgetaireRequest request,
        CancellationToken cancellationToken = default);

    Task<PrevisionBudgetaireDto?> UpdateAsync(
        long idPrevision,
        UpdatePrevisionBudgetaireRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idPrevision, CancellationToken cancellationToken = default);

    Task<SauvegarderGrillePrevisionResultDto> SauvegarderGrilleAsync(
        SauvegarderGrillePrevisionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IGroupeItemAERepository
{
    Task<IReadOnlyList<GroupeItemAEDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<GroupeItemAEDto?> GetByIdAsync(long idGroupeItemAE, CancellationToken cancellationToken = default);

    Task<GroupeItemAEDto> CreateAsync(string libelle, bool actif, CancellationToken cancellationToken = default);

    Task<GroupeItemAEDto?> UpdateAsync(long idGroupeItemAE, string libelle, bool actif, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idGroupeItemAE, CancellationToken cancellationToken = default);

    Task<int> CountPrevisionsAsync(long idGroupeItemAE, CancellationToken cancellationToken = default);
}

public interface IGroupeItemAEService
{
    Task<IReadOnlyList<GroupeItemAEDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<GroupeItemAEDto?> GetByIdAsync(long idGroupeItemAE, CancellationToken cancellationToken = default);

    Task<GroupeItemAEDto> CreateAsync(CreateGroupeItemAERequest request, CancellationToken cancellationToken = default);

    Task<GroupeItemAEDto?> UpdateAsync(long idGroupeItemAE, UpdateGroupeItemAERequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idGroupeItemAE, CancellationToken cancellationToken = default);
}
