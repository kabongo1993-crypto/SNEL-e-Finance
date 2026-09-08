using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.UnitTests.Referentiels;

/// <summary>Périmètre ouvert — tests métier prévisions sans filtrage UB.</summary>
internal sealed class NoopPerimetreAccesService : IPerimetreAccesService
{
    public bool PeutVoirToutesUbPrevisions() => true;

    public Task GarantirAccesUbSaisiePrevisionsAsync(long idUB, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task GarantirAccesUbLecturePrevisionsAsync(long idUB, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> UtilisateurPeutAccederUbLecturePrevisionsAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> PeutAccederUbLectureCourantAsync(long idUB, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<IReadOnlyList<long>?> GetIdsUbAutoriseesSaisieAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<long>?>(null);

    public bool PeutVoirToutesUbDpm() => true;

    public Task GarantirAccesUbDpmAsync(long idUB, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> PeutAccederUbDpmAsync(long idUB, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<IReadOnlyList<long>?> GetIdsUbAutoriseesDpmAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<long>?>(null);
}

/// <summary>Aucun périmètre configuré ; pas de proxy créateur prévision.</summary>
internal sealed class EmptyFakePerimetreReader : IPerimetreUtilisateurReader
{
    public Task<PerimetreUtilisateurSnapshot?> GetAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
        => Task.FromResult<PerimetreUtilisateurSnapshot?>(null);

    public Task<long?> GetDepartementUbAsync(long idUB, CancellationToken cancellationToken = default)
        => Task.FromResult<long?>(1);

    public Task<bool> UtilisateurPeutAccederUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<IReadOnlyList<long>> ResoudreIdsUbPerimetreAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<long>>([]);

    public Task<bool> UtilisateurACreePrevisionSurUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<IReadOnlyList<long>> ResoudreIdsUbProxyPrevisionAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<long>>([]);
}

internal static class SuiviPrevisionServiceTestFactory
{
    public static SuiviPrevisionService Create(
        ISuiviPrevisionRepository repo,
        ICurrentUserService user,
        IPerimetreAccesService? acces = null,
        IPerimetreUtilisateurReader? perimetre = null)
    {
        perimetre ??= new EmptyFakePerimetreReader();
        acces ??= new PerimetreAccesService(perimetre, user);
        return new SuiviPrevisionService(repo, user, acces, perimetre);
    }
}
