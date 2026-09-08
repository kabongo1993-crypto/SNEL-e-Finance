namespace BudgetWeb.Application.Interfaces;

/// <summary>
/// Règles d'accès UB/Département pour Prévisions (et helpers partagés).
/// </summary>
public interface IPerimetreAccesService
{
    /// <summary>Contrôleurs / validateurs / admin — voient toutes les UB en suivi.</summary>
    bool PeutVoirToutesUbPrevisions();

    /// <summary>
    /// Saisie grille : périmètre configuré → UB autorisée ; sinon historique (toute UB).
    /// </summary>
    Task GarantirAccesUbSaisiePrevisionsAsync(long idUB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lecture détail / grille : bypass contrôleurs ; périmètre ; sinon créateur sur l'UB.
    /// </summary>
    Task GarantirAccesUbLecturePrevisionsAsync(long idUB, CancellationToken cancellationToken = default);

    Task<bool> UtilisateurPeutAccederUbLecturePrevisionsAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default);

    Task<bool> PeutAccederUbLectureCourantAsync(long idUB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dropdown saisie : null = toutes les UB actives ; sinon liste filtrée par périmètre.
    /// </summary>
    Task<IReadOnlyList<long>?> GetIdsUbAutoriseesSaisieAsync(CancellationToken cancellationToken = default);

    /// <summary>File Budgets DPM — voient toutes les UB / demandeurs sans filtre périmètre.</summary>
    bool PeutVoirToutesUbDpm();

    /// <summary>
    /// Création DPM / demandeur : périmètre configuré → UB autorisée ; sinon proxy prévision.
    /// </summary>
    Task GarantirAccesUbDpmAsync(long idUB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Filtre demandeurs / accès UB DPM : bypass ; périmètre ; sinon proxy prévision.
    /// </summary>
    Task<bool> PeutAccederUbDpmAsync(long idUB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dropdown demandeur / DPM : null = toutes les UB ; sinon liste filtrée (périmètre ou proxy).
    /// </summary>
    Task<IReadOnlyList<long>?> GetIdsUbAutoriseesDpmAsync(CancellationToken cancellationToken = default);
}
