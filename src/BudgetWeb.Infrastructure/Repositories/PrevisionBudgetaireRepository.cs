using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class PrevisionBudgetaireRepository : IPrevisionBudgetaireRepository
{
    private readonly BudgetDbContext _context;

    public PrevisionBudgetaireRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PrevisionBudgetaireDto>> GetByFiltresAsync(
        long? idVersion,
        long? idTypeBudget,
        long? idUB,
        long? idModePrevision,
        string? libelleItemAE,
        long? idItemBI,
        CancellationToken cancellationToken = default)
    {
        var query = Query();
        if (idVersion is > 0)
        {
            query = query.Where(p => p.FK_VersionBudgetaire == idVersion);
        }

        if (idTypeBudget is > 0)
        {
            query = query.Where(p => p.FK_TypeBudget == idTypeBudget);
        }

        if (idUB is > 0)
        {
            query = query.Where(p => p.FK_UniteBudgetaire == idUB);
        }

        if (idModePrevision is > 0)
        {
            query = query.Where(p => p.FK_ModePrevision == idModePrevision);
        }

        if (!string.IsNullOrWhiteSpace(libelleItemAE))
        {
            var action = libelleItemAE.Trim();
            query = query.Where(p => p.LibelleItemAE != null && p.LibelleItemAE == action);
        }

        if (idItemBI is > 0)
        {
            query = query.Where(p => p.FK_ItemBI == idItemBI);
        }

        var items = await query.ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<PrevisionGrilleSourceDto>> GetForGrilleAsync(
        long idVersion,
        long idTypeBudget,
        long idUB,
        string? libelleItemAE,
        long? idItemBI,
        bool includeRepartitions,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p =>
                p.FK_VersionBudgetaire == idVersion
                && p.FK_TypeBudget == idTypeBudget
                && p.FK_UniteBudgetaire == idUB);

        if (!string.IsNullOrWhiteSpace(libelleItemAE))
        {
            var action = libelleItemAE.Trim();
            query = query.Where(p => p.LibelleItemAE != null && p.LibelleItemAE == action);
        }

        if (idItemBI is > 0)
        {
            query = query.Where(p => p.FK_ItemBI == idItemBI);
        }

        if (!includeRepartitions)
        {
            var rows = await query
                .Select(p => new
                {
                    p.IdPrevision,
                    p.FK_RubriqueBudgetaire,
                    p.DetailBI,
                    p.LibelleItemAE,
                    p.MontantAnnuel,
                })
                .ToListAsync(cancellationToken);

            return rows
                .Select(p => new PrevisionGrilleSourceDto(
                    p.IdPrevision,
                    p.FK_RubriqueBudgetaire,
                    p.DetailBI,
                    p.LibelleItemAE,
                    p.MontantAnnuel,
                    Array.Empty<RepartitionMensuelleDto>()))
                .ToList();
        }

        // Deux requêtes légères plutôt qu'un Include multi-join (évite le graphe Version/UB/…).
        var headers = await query
            .Select(p => new
            {
                p.IdPrevision,
                p.FK_RubriqueBudgetaire,
                p.DetailBI,
                p.LibelleItemAE,
                p.MontantAnnuel,
            })
            .ToListAsync(cancellationToken);

        if (headers.Count == 0)
        {
            return [];
        }

        var ids = headers.Select(h => h.IdPrevision).ToList();
        var reps = await _context.RepartitionsMensuelles.AsNoTracking()
            .Where(r => ids.Contains(r.FK_PrevisionBudgetaire) && r.Montant != 0)
            .Select(r => new { r.FK_PrevisionBudgetaire, r.Mois, r.Montant })
            .ToListAsync(cancellationToken);

        var byPrev = reps
            .GroupBy(r => r.FK_PrevisionBudgetaire)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<RepartitionMensuelleDto>)g
                    .OrderBy(x => x.Mois)
                    .Select(x => new RepartitionMensuelleDto(x.Mois, x.Montant))
                    .ToList());

        return headers
            .Select(h => new PrevisionGrilleSourceDto(
                h.IdPrevision,
                h.FK_RubriqueBudgetaire,
                h.DetailBI,
                h.LibelleItemAE,
                h.MontantAnnuel,
                byPrev.GetValueOrDefault(h.IdPrevision, Array.Empty<RepartitionMensuelleDto>())))
            .ToList();
    }

    public async Task<PrevisionBudgetaireDto?> GetByIdAsync(long idPrevision, CancellationToken cancellationToken = default)
    {
        var entity = await Query().FirstOrDefaultAsync(p => p.IdPrevision == idPrevision, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<(string Statut, bool Exists)> GetVersionStatutAsync(long idVersion, CancellationToken cancellationToken = default)
    {
        var statut = await _context.VersionsBudgetaires.AsNoTracking()
            .Where(v => v.IdVersion == idVersion)
            .Select(v => v.Statut)
            .FirstOrDefaultAsync(cancellationToken);
        return statut is null ? (string.Empty, false) : (statut, true);
    }

    public async Task<(string CodeType, string Libelle, bool Exists, bool Actif)> GetTypeBudgetAsync(
        long idTypeBudget,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.TypesBudget.AsNoTracking()
            .Where(t => t.IdTypeBudget == idTypeBudget)
            .Select(t => new { t.CodeType, t.Libelle, t.Actif })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? (string.Empty, string.Empty, false, false)
            : (row.CodeType, row.Libelle, true, row.Actif);
    }

    public async Task<(string CodeMode, string Libelle, bool Exists, bool Actif)> GetModePrevisionAsync(
        long idModePrevision,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.ModesPrevision.AsNoTracking()
            .Where(m => m.IdModePrevision == idModePrevision)
            .Select(m => new { m.CodeMode, m.Libelle, m.Actif })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? (string.Empty, string.Empty, false, false)
            : (row.CodeMode, row.Libelle, true, row.Actif);
    }

    public Task<bool> ExistsUBAsync(long idUB, CancellationToken cancellationToken = default)
        => _context.UnitesBudgetaires.AsNoTracking().AnyAsync(u => u.IdUB == idUB, cancellationToken);

    public Task<bool> ExistsRBAsync(long idRB, CancellationToken cancellationToken = default)
        => _context.RubriquesBudgetaires.AsNoTracking().AnyAsync(r => r.IdRB == idRB, cancellationToken);

    public Task<bool> ExistsItemBIAsync(long idItemBI, CancellationToken cancellationToken = default)
        => _context.ItemsBI.AsNoTracking().AnyAsync(i => i.IdItemBI == idItemBI, cancellationToken);

    public Task<bool> ExistsGroupeItemAEAsync(long idGroupeItemAE, CancellationToken cancellationToken = default)
        => _context.GroupesItemsAE.AsNoTracking().AnyAsync(g => g.IdGroupeItemAE == idGroupeItemAE, cancellationToken);

    public Task<bool> ExistsUtilisateurAsync(long idUtilisateur, CancellationToken cancellationToken = default)
        => _context.Utilisateurs.AsNoTracking().AnyAsync(u => u.IdUtilisateur == idUtilisateur, cancellationToken);

    public async Task<(string CodeUB, string LibelleUB)?> GetUBInfoAsync(long idUB, CancellationToken cancellationToken = default)
    {
        var row = await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => u.IdUB == idUB)
            .Select(u => new { u.CodeUB, u.Libelle })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.CodeUB, row.Libelle);
    }

    public async Task<(string? CodeItem, string? LibelleItem)?> GetItemBIInfoAsync(long idItemBI, CancellationToken cancellationToken = default)
    {
        var row = await _context.ItemsBI.AsNoTracking()
            .Where(i => i.IdItemBI == idItemBI)
            .Select(i => new { i.CodeItem, i.Libelle })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.CodeItem, row.Libelle);
    }

    public Task<long?> FindIdDcAsync(long idVersion, long idUB, long idRB, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion
                && p.FK_UniteBudgetaire == idUB
                && p.FK_RubriqueBudgetaire == idRB)
            .Select(p => (long?)p.IdPrevision)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<long?> FindIdAeAsync(
        long idVersion,
        long idUB,
        long idRB,
        string libelleItemAE,
        CancellationToken cancellationToken = default)
    {
        var action = libelleItemAE.Trim();
        return _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion
                && p.FK_UniteBudgetaire == idUB
                && p.FK_RubriqueBudgetaire == idRB
                && p.LibelleItemAE == action)
            .Select(p => (long?)p.IdPrevision)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<long?> FindIdBiAsync(
        long idVersion,
        long idUB,
        long idItemBI,
        string detailBI,
        CancellationToken cancellationToken = default)
    {
        var detail = detailBI.Trim();
        return _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion
                && p.FK_UniteBudgetaire == idUB
                && p.FK_ItemBI == idItemBI
                && p.DetailBI == detail)
            .Select(p => (long?)p.IdPrevision)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(
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
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.RubriquesBudgetaires.AsNoTracking()
            .Where(r => r.Actif)
            .OrderBy(r => r.CodeRB)
            .Select(r => new
            {
                r.IdRB,
                r.CodeRB,
                r.Libelle,
                r.FK_RubriqueBudgetaireParent,
                r.Niveau,
                r.Actif,
                r.FK_GroupeRubriqueBudgetaire,
                CodeGroupe = r.GroupeRubriqueBudgetaire != null ? r.GroupeRubriqueBudgetaire.CodeGroupe : null,
                LibelleGroupe = r.GroupeRubriqueBudgetaire != null ? r.GroupeRubriqueBudgetaire.Libelle : null,
                Ordre = r.GroupeRubriqueBudgetaire != null
                    ? (int?)r.GroupeRubriqueBudgetaire.OrdreAffichage
                    : null
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => (
                r.IdRB,
                r.CodeRB,
                r.Libelle,
                r.FK_RubriqueBudgetaireParent,
                r.Niveau,
                r.Actif,
                r.FK_GroupeRubriqueBudgetaire,
                r.CodeGroupe,
                r.LibelleGroupe,
                r.Ordre))
            .ToList();
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PrevisionBudgetaireDto> CreateAsync(
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
        CancellationToken cancellationToken = default)
    {
        var entity = new PrevisionBudgetaire
        {
            FK_VersionBudgetaire = idVersion,
            FK_TypeBudget = idTypeBudget,
            FK_ModePrevision = idModePrevision,
            FK_UniteBudgetaire = idUB,
            FK_RubriqueBudgetaire = idRB,
            FK_ItemBI = idItemBI,
            FK_GroupeItemAE = idGroupeItemAE,
            LibelleItemAE = libelleItemAE,
            DetailBI = detailBI,
            MontantAnnuel = montantAnnuel,
            DateCreation = DateTime.Now,
            FK_UtilisateurCreation = idUtilisateurCreation,
        };

        foreach (var r in repartitions)
        {
            entity.RepartitionsMensuelles.Add(new RepartitionMensuelle
            {
                Mois = r.Mois,
                Montant = r.Montant,
            });
        }

        _context.PrevisionsBudgetaires.Add(entity);
        await SauvegarderAsync(cancellationToken);
        return (await GetByIdAsync(entity.IdPrevision, cancellationToken))!;
    }

    public async Task<PrevisionBudgetaireDto?> UpdateAsync(
        long idPrevision,
        long? idGroupeItemAE,
        string? libelleItemAE,
        string? detailBI,
        decimal montantAnnuel,
        long idUtilisateurModification,
        IReadOnlyList<RepartitionMensuelleDto>? repartitions,
        bool remplacerRepartitions,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.PrevisionsBudgetaires
            .Include(p => p.RepartitionsMensuelles)
            .FirstOrDefaultAsync(p => p.IdPrevision == idPrevision, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.FK_GroupeItemAE = idGroupeItemAE;
        entity.LibelleItemAE = libelleItemAE;
        entity.DetailBI = detailBI;
        entity.MontantAnnuel = montantAnnuel;
        entity.DateModification = DateTime.Now;
        entity.FK_UtilisateurModification = idUtilisateurModification;

        if (remplacerRepartitions && repartitions is not null)
        {
            _context.RepartitionsMensuelles.RemoveRange(entity.RepartitionsMensuelles);
            entity.RepartitionsMensuelles.Clear();
            foreach (var r in repartitions)
            {
                entity.RepartitionsMensuelles.Add(new RepartitionMensuelle
                {
                    FK_PrevisionBudgetaire = idPrevision,
                    Mois = r.Mois,
                    Montant = r.Montant,
                });
            }
        }

        await SauvegarderAsync(cancellationToken);
        return await GetByIdAsync(idPrevision, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idPrevision, CancellationToken cancellationToken = default)
    {
        var entity = await _context.PrevisionsBudgetaires
            .Include(p => p.RepartitionsMensuelles)
            .FirstOrDefaultAsync(p => p.IdPrevision == idPrevision, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.RepartitionsMensuelles.RemoveRange(entity.RepartitionsMensuelles);
        _context.PrevisionsBudgetaires.Remove(entity);
        await SauvegarderAsync(cancellationToken);
        return true;
    }

    public async Task RemplacerRepartitionsAsync(
        long idPrevision,
        IReadOnlyList<RepartitionMensuelleDto> repartitions,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.PrevisionsBudgetaires
            .Include(p => p.RepartitionsMensuelles)
            .FirstOrDefaultAsync(p => p.IdPrevision == idPrevision, cancellationToken)
            ?? throw new InvalidOperationException("Prévision introuvable.");

        _context.RepartitionsMensuelles.RemoveRange(entity.RepartitionsMensuelles);
        entity.RepartitionsMensuelles.Clear();
        foreach (var r in repartitions)
        {
            entity.RepartitionsMensuelles.Add(new RepartitionMensuelle
            {
                FK_PrevisionBudgetaire = idPrevision,
                Mois = r.Mois,
                Montant = r.Montant,
            });
        }

        await SauvegarderAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrevisionResumeCategorieDto>> GetResumeParTypeAsync(
        long idVersion,
        CancellationToken cancellationToken = default)
    {
        return await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion)
            .GroupBy(p => new { p.FK_TypeBudget, p.TypeBudget.CodeType, p.TypeBudget.Libelle, p.TypeBudget.OrdreAffichage })
            .OrderBy(g => g.Key.OrdreAffichage)
            .Select(g => new PrevisionResumeCategorieDto(
                g.Key.CodeType,
                g.Key.Libelle,
                g.Sum(x => x.MontantAnnuel),
                g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListLibellesItemAEAsync(
        long? idVersion,
        long? idExercice,
        CancellationToken cancellationToken = default)
    {
        if (idVersion is not > 0 && idExercice is not > 0)
        {
            return [];
        }

        var query = _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.LibelleItemAE != null && p.LibelleItemAE != "");

        if (idExercice is > 0)
        {
            query = query.Where(p => p.VersionBudgetaire.FK_ExerciceBudgetaire == idExercice);
        }
        else if (idVersion is > 0)
        {
            query = query.Where(p => p.FK_VersionBudgetaire == idVersion);
        }

        return await query
            .Select(p => p.LibelleItemAE!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<PrevisionBudgetaire> Query()
        => _context.PrevisionsBudgetaires
            .AsNoTracking()
            .Include(p => p.VersionBudgetaire).ThenInclude(v => v.ExerciceBudgetaire)
            .Include(p => p.TypeBudget)
            .Include(p => p.ModePrevision)
            .Include(p => p.UniteBudgetaire)
            .Include(p => p.RubriqueBudgetaire)
            .Include(p => p.ItemBI)
            .Include(p => p.GroupeItemAE)
            .Include(p => p.RepartitionsMensuelles);

    private async Task SauvegarderAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationUnique(ex))
        {
            throw new InvalidOperationException(MessageDoublon(ex));
        }
        catch (DbUpdateException ex) when (EstViolationCheck(ex))
        {
            throw new InvalidOperationException(MessageCheck(ex));
        }
        catch (DbUpdateException ex) when (EstViolationTrigger(ex))
        {
            throw new InvalidOperationException(
                "Structure de prévision invalide pour le type de budget DC, AE ou BI.");
        }
        catch (DbUpdateException ex) when (EstViolationFk(ex))
        {
            throw new InvalidOperationException(
                "Une référence (version, type, mode, UB, rubrique, item, groupe ou utilisateur) est invalide.");
        }
    }

    private static PrevisionBudgetaireDto Map(PrevisionBudgetaire p)
        => new(
            p.IdPrevision,
            p.FK_VersionBudgetaire,
            p.VersionBudgetaire.NumeroVersion,
            p.VersionBudgetaire.Libelle,
            p.VersionBudgetaire.Statut,
            p.VersionBudgetaire.ExerciceBudgetaire.Annee,
            p.FK_TypeBudget,
            p.TypeBudget.CodeType,
            p.TypeBudget.Libelle,
            p.FK_ModePrevision,
            p.ModePrevision.CodeMode,
            p.ModePrevision.Libelle,
            p.FK_UniteBudgetaire,
            p.UniteBudgetaire.CodeUB,
            p.UniteBudgetaire.Libelle,
            p.FK_RubriqueBudgetaire,
            p.RubriqueBudgetaire?.CodeRB,
            p.RubriqueBudgetaire?.Libelle,
            p.FK_ItemBI,
            p.ItemBI?.CodeItem,
            p.ItemBI?.Libelle,
            p.FK_GroupeItemAE,
            p.GroupeItemAE?.Libelle,
            p.LibelleItemAE,
            p.DetailBI,
            p.MontantAnnuel,
            p.RepartitionsMensuelles
                .OrderBy(r => r.Mois)
                .Select(r => new RepartitionMensuelleDto(r.Mois, r.Montant))
                .ToList(),
            p.DateCreation,
            p.FK_UtilisateurCreation,
            p.DateModification,
            p.FK_UtilisateurModification);

    private static bool EstViolationUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UX_PREVISION_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UQ_REPARTITION_PREVISION_MOIS", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationCheck(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("CK_PREVISION_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("CK_REPARTITION_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationTrigger(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("Structure de prevision invalide", StringComparison.OrdinalIgnoreCase)
            || message.Contains("50001", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationFk(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("FK_PREVISION_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FK_REPARTITION_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static string MessageDoublon(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("UX_PREVISION_DC", StringComparison.OrdinalIgnoreCase))
        {
            return "Une prévision DC existe déjà pour cette combinaison version, unité budgétaire et rubrique.";
        }

        if (message.Contains("UX_PREVISION_AE", StringComparison.OrdinalIgnoreCase))
        {
            return "Une prévision AE existe déjà pour cette combinaison version, unité budgétaire, rubrique et action d'exploitation.";
        }

        if (message.Contains("UX_PREVISION_BI", StringComparison.OrdinalIgnoreCase))
        {
            return "Une prévision BI existe déjà pour cette combinaison version, unité budgétaire, item BI et détail.";
        }

        if (message.Contains("UQ_REPARTITION_PREVISION_MOIS", StringComparison.OrdinalIgnoreCase))
        {
            return "Une répartition existe déjà pour ce mois sur cette prévision.";
        }

        return "Une prévision ou une répartition en doublon a été détectée.";
    }

    private static string MessageCheck(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("CK_PREVISION_Montant", StringComparison.OrdinalIgnoreCase))
        {
            return "Le montant annuel ne peut pas être négatif.";
        }

        if (message.Contains("CK_REPARTITION_Mois", StringComparison.OrdinalIgnoreCase))
        {
            return "Le mois de répartition doit être compris entre 1 et 12.";
        }

        if (message.Contains("CK_REPARTITION_Montant", StringComparison.OrdinalIgnoreCase))
        {
            return "Les montants mensuels ne peuvent pas être négatifs.";
        }

        return "Les données de prévision ne respectent pas les règles de la base.";
    }
}

public class GroupeItemAERepository : IGroupeItemAERepository
{
    private readonly BudgetDbContext _context;

    public GroupeItemAERepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GroupeItemAEDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.GroupesItemsAE.AsNoTracking()
            .OrderBy(g => g.Libelle)
            .Select(g => new GroupeItemAEDto(
                g.IdGroupeItemAE,
                g.Libelle,
                g.Actif,
                g.DateCreation,
                g.PrevisionsBudgetaires.Count))
            .ToListAsync(cancellationToken);

    public async Task<GroupeItemAEDto?> GetByIdAsync(long idGroupeItemAE, CancellationToken cancellationToken = default)
        => await _context.GroupesItemsAE.AsNoTracking()
            .Where(g => g.IdGroupeItemAE == idGroupeItemAE)
            .Select(g => new GroupeItemAEDto(
                g.IdGroupeItemAE,
                g.Libelle,
                g.Actif,
                g.DateCreation,
                g.PrevisionsBudgetaires.Count))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<GroupeItemAEDto> CreateAsync(string libelle, bool actif, CancellationToken cancellationToken = default)
    {
        var entity = new GroupeItemAE
        {
            Libelle = libelle,
            Actif = actif,
            DateCreation = DateTime.Now,
        };
        _context.GroupesItemsAE.Add(entity);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when ((ex.InnerException?.Message ?? ex.Message)
            .Contains("UQ_GROUPE_ITEM_AE_Libelle", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Un groupe AE avec le libellé « {libelle} » existe déjà.");
        }

        return new GroupeItemAEDto(entity.IdGroupeItemAE, entity.Libelle, entity.Actif, entity.DateCreation, 0);
    }

    public async Task<GroupeItemAEDto?> UpdateAsync(
        long idGroupeItemAE,
        string libelle,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.GroupesItemsAE.FirstOrDefaultAsync(g => g.IdGroupeItemAE == idGroupeItemAE, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Libelle = libelle;
        entity.Actif = actif;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when ((ex.InnerException?.Message ?? ex.Message)
            .Contains("UQ_GROUPE_ITEM_AE_Libelle", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Un groupe AE avec le libellé « {libelle} » existe déjà.");
        }

        return await GetByIdAsync(idGroupeItemAE, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idGroupeItemAE, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GroupesItemsAE.FirstOrDefaultAsync(g => g.IdGroupeItemAE == idGroupeItemAE, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.GroupesItemsAE.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<int> CountPrevisionsAsync(long idGroupeItemAE, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires.AsNoTracking()
            .CountAsync(p => p.FK_GroupeItemAE == idGroupeItemAE, cancellationToken);
}
