using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;

namespace BudgetWeb.Application.Services;

public sealed class ClassementAeService : IClassementAeService
{
    private readonly IClassementAeRepository _repository;

    public ClassementAeService(IClassementAeRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ClassementAeLigneDto>> GetAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        ValiderScope(idVersion, idUB);
        return await _repository.GetByVersionUbAsync(idVersion, idUB, cancellationToken);
    }

    public async Task ReorderAsync(ReorderClassementAeRequest request, CancellationToken cancellationToken = default)
    {
        ValiderScope(request.IdVersion, request.IdUB);
        if (request.IdsClassementOrdonnes is null || request.IdsClassementOrdonnes.Count == 0)
        {
            throw new InvalidOperationException("La liste d'identifiants de classement est obligatoire.");
        }

        if (request.IdsClassementOrdonnes.Distinct().Count() != request.IdsClassementOrdonnes.Count)
        {
            throw new InvalidOperationException("La liste contient des identifiants en double.");
        }

        await _repository.ExecuteInTransactionAsync(
            ct => _repository.ReorderAsync(request.IdVersion, request.IdUB, request.IdsClassementOrdonnes, ct),
            cancellationToken);
    }

    public async Task<ClassementAeInitResultDto> InitialiserManquantsAsync(
        CancellationToken cancellationToken = default)
    {
        var actions = await _repository.GetActionsAePourInitAsync(cancellationToken);
        var couples = actions
            .Select(a => (a.IdVersion, a.IdUB))
            .Distinct()
            .ToList();

        var crees = 0;
        var deja = 0;
        var traites = 0;

        foreach (var (idVersion, idUB) in couples)
        {
            traites++;
            if (await _repository.ExistsAnyAsync(idVersion, idUB, cancellationToken))
            {
                deja++;
                continue;
            }

            await _repository.ExecuteInTransactionAsync(async ct =>
            {
                var duCouple = actions
                    .Where(a => a.IdVersion == idVersion && a.IdUB == idUB)
                    .ToList();

                // Groupes d'abord, puis items (ordre technique, non historique Excel).
                foreach (var idGroupe in duCouple.Where(a => a.IdGroupe is > 0).Select(a => a.IdGroupe!.Value).Distinct())
                {
                    await _repository.EnsureGroupeAsync(idVersion, idUB, idGroupe, ct);
                }

                foreach (var action in duCouple)
                {
                    await _repository.EnsureItemAsync(idVersion, idUB, action.LibelleItemAE, ct);
                }
            }, cancellationToken);

            crees += (await _repository.GetByVersionUbAsync(idVersion, idUB, cancellationToken)).Count;
        }

        return new ClassementAeInitResultDto(traites, crees, deja);
    }

    public async Task GarantirGroupeHomogeneAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        long? idGroupePropose,
        CancellationToken cancellationToken = default)
    {
        var libelle = NormaliserLibelle(libelleItemAE);
        var existants = await _repository.GetGroupesDistinctsActionAsync(idVersion, idUB, libelle, cancellationToken);
        if (existants.Count == 0)
        {
            return;
        }

        var propose = idGroupePropose is > 0 ? idGroupePropose : null;
        foreach (var g in existants)
        {
            var exist = g is > 0 ? g : null;
            if (exist != propose)
            {
                throw new InvalidOperationException(
                    $"L'action « {libelle} » a déjà un groupe différent sur d'autres rubriques. "
                    + "Toutes les prévisions AE d'une même action doivent partager le même groupe (ou aucune).");
            }
        }
    }

    public async Task ApresEcritureAeAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        long? idGroupeItemAE,
        CancellationToken cancellationToken = default)
    {
        var libelle = NormaliserLibelle(libelleItemAE);
        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            if (idGroupeItemAE is > 0)
            {
                await _repository.EnsureGroupeAsync(idVersion, idUB, idGroupeItemAE.Value, ct);
            }

            await _repository.EnsureItemAsync(idVersion, idUB, libelle, ct);
        }, cancellationToken);
    }

    public async Task ApresSuppressionAeAsync(
        long idVersion,
        long idUB,
        string? libelleItemAE,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(libelleItemAE))
        {
            return;
        }

        var libelle = NormaliserLibelle(libelleItemAE);
        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.RemoveItemIfUnusedAsync(idVersion, idUB, libelle, ct);
            await _repository.PurgeOrphanGroupesAsync(idVersion, idUB, ct);
            await _repository.ReindexAsync(idVersion, idUB, ct);
        }, cancellationToken);
    }

    public async Task ApresRenommageAeAsync(
        long idVersion,
        long idUB,
        string ancienLibelle,
        string nouveauLibelle,
        long? idGroupeItemAE,
        CancellationToken cancellationToken = default)
    {
        var ancien = NormaliserLibelle(ancienLibelle);
        var nouveau = NormaliserLibelle(nouveauLibelle);
        if (string.Equals(ancien, nouveau, StringComparison.Ordinal))
        {
            await ApresEcritureAeAsync(idVersion, idUB, nouveau, idGroupeItemAE, cancellationToken);
            return;
        }

        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            // Met à jour les autres RB encore sous l'ancien libellé (la ligne courante peut déjà être renommée).
            await _repository.RenamePrevisionsAeAsync(idVersion, idUB, ancien, nouveau, ct);
            await _repository.RenameItemAsync(idVersion, idUB, ancien, nouveau, ct);
            if (idGroupeItemAE is > 0)
            {
                await _repository.EnsureGroupeAsync(idVersion, idUB, idGroupeItemAE.Value, ct);
            }

            await _repository.EnsureItemAsync(idVersion, idUB, nouveau, ct);
        }, cancellationToken);
    }

    public async Task ApresChangementGroupeAeAsync(
        long idVersion,
        long idUB,
        string libelleItemAE,
        long? nouveauGroupe,
        CancellationToken cancellationToken = default)
    {
        var libelle = NormaliserLibelle(libelleItemAE);
        var groupe = nouveauGroupe is > 0 ? nouveauGroupe : null;

        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.UpdateGroupePrevisionsAeAsync(idVersion, idUB, libelle, groupe, ct);
            if (groupe is > 0)
            {
                await _repository.EnsureGroupeAsync(idVersion, idUB, groupe.Value, ct);
            }

            await _repository.EnsureItemAsync(idVersion, idUB, libelle, ct);
            await _repository.PurgeOrphanGroupesAsync(idVersion, idUB, ct);
            await _repository.ReindexAsync(idVersion, idUB, ct);
        }, cancellationToken);
    }

    private static void ValiderScope(long idVersion, long idUB)
    {
        if (idVersion <= 0)
        {
            throw new InvalidOperationException("IdVersion est obligatoire.");
        }

        if (idUB <= 0)
        {
            throw new InvalidOperationException("IdUB est obligatoire.");
        }
    }

    private static string NormaliserLibelle(string libelle)
    {
        var t = (libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(t))
        {
            throw new InvalidOperationException("Le libellé de l'action d'exploitation est obligatoire.");
        }

        return t;
    }
}
