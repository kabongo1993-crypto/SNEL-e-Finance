using BudgetWeb.Domain.Entities;

namespace BudgetWeb.Application.Interfaces.Referentiels;

public interface IPaireTauxChangeRepository
{
    Task<IReadOnlyList<PaireTauxChange>> ListActivesAsync(CancellationToken cancellationToken = default);

    Task<PaireTauxChange?> FindCanoniqueAsync(
        string deviseBase,
        string deviseQuote,
        CancellationToken cancellationToken = default);

    /// <summary>Résout deux devises vers la ligne canonique enregistrée (quelle que soit l'orientation demandée).</summary>
    Task<PaireTauxChange?> ResolveForDevisesAsync(
        string deviseA,
        string deviseB,
        CancellationToken cancellationToken = default);

    Task<bool> DevisesActivesExistentAsync(
        string deviseBase,
        string deviseQuote,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retourne la paire existante (quelle que soit l'orientation demandée) ou crée l'entrée
    /// registre avec l'orientation demandée si le couple est nouveau.
    /// </summary>
    Task<PaireTauxChange> CreerOuObtenirPaireCanoniqueAsync(
        string deviseBase,
        string deviseQuote,
        CancellationToken cancellationToken = default);

    Task<PaireTauxChange> AddAsync(PaireTauxChange entity, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
