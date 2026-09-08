using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Interfaces;

/// <summary>Lecture du périmètre de sécurité utilisateur (Département / UB).</summary>
public interface IPerimetreUtilisateurReader
{
    Task<PerimetreUtilisateurSnapshot?> GetAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default);

    /// <summary>Retourne le FK_Departement de l'UB, ou null si UB inexistante.</summary>
    Task<long?> GetDepartementUbAsync(
        long idUB,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DPM : périmètre configuré → <see cref="PerimetreAccess"/> ;
    /// sinon fallback « a créé une prévision sur cette UB ».
    /// </summary>
    Task<bool> UtilisateurPeutAccederUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// IDs des UB accessibles via périmètre configuré (vide si non configuré).
    /// </summary>
    Task<IReadOnlyList<long>> ResoudreIdsUbPerimetreAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default);

    Task<bool> UtilisateurACreePrevisionSurUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UB distinctes sur lesquelles l'utilisateur a créé au moins une prévision (proxy DPM).
    /// </summary>
    Task<IReadOnlyList<long>> ResoudreIdsUbProxyPrevisionAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default);
}
