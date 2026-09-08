using System.Text.Json;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class VersionBudgetaireRepository : IVersionBudgetaireRepository
{
    private readonly BudgetDbContext _context;

    public VersionBudgetaireRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<VersionBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Projection SQL : pas d'Include des collections Previsions/Transferts/VersionsSuivantes
        // (évite explosion cartésienne et chargement inutile pour listes / combos).
        return await _context.VersionsBudgetaires.AsNoTracking()
            .OrderByDescending(v => v.ExerciceBudgetaire.Annee)
            .ThenBy(v => v.NumeroVersion)
            .Select(v => new VersionBudgetaireDto(
                v.IdVersion,
                v.FK_ExerciceBudgetaire,
                v.ExerciceBudgetaire.Annee,
                v.ExerciceBudgetaire.Statut,
                v.NumeroVersion,
                v.Libelle,
                v.FK_VersionBudgetairePrecedente,
                v.VersionPrecedente != null ? v.VersionPrecedente.NumeroVersion : null,
                v.VersionPrecedente != null ? v.VersionPrecedente.Libelle : null,
                v.DateCreation,
                v.DateDebutEffet,
                v.DateFinEffet,
                v.Motif,
                v.Statut,
                v.FK_UtilisateurCreation,
                v.UtilisateurCreation.NomUtilisateur,
                v.FK_UtilisateurValidation,
                v.UtilisateurValidation != null ? v.UtilisateurValidation.NomUtilisateur : null,
                v.DateValidation,
                v.FK_UtilisateurSoumission,
                v.UtilisateurSoumission != null ? v.UtilisateurSoumission.NomUtilisateur : null,
                v.DateSoumission,
                v.FK_UtilisateurControle,
                v.UtilisateurControle != null ? v.UtilisateurControle.NomUtilisateur : null,
                v.DateControle,
                v.FK_UtilisateurRejet,
                v.UtilisateurRejet != null ? v.UtilisateurRejet.NomUtilisateur : null,
                v.DateRejet,
                v.MotifRejet,
                v.PrevisionsBudgetaires.Count,
                v.TransfertsBudgetaires.Count,
                v.VersionsSuivantes.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<VersionBudgetaireDto?> GetByIdAsync(long idVersion, CancellationToken cancellationToken = default)
    {
        var entity = await Query()
            .FirstOrDefaultAsync(v => v.IdVersion == idVersion, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public Task<bool> ExistsByExerciceNumeroAsync(
        long idExercice,
        int numeroVersion,
        long? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.VersionsBudgetaires
            .AsNoTracking()
            .Where(v => v.FK_ExerciceBudgetaire == idExercice && v.NumeroVersion == numeroVersion);
        if (excludeId is not null)
        {
            query = query.Where(v => v.IdVersion != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ExistsExerciceAsync(long idExercice, CancellationToken cancellationToken = default)
        => _context.ExercicesBudgetaires.AsNoTracking().AnyAsync(e => e.IdExercice == idExercice, cancellationToken);

    public Task<bool> ExistsVersionAsync(long idVersion, CancellationToken cancellationToken = default)
        => _context.VersionsBudgetaires.AsNoTracking().AnyAsync(v => v.IdVersion == idVersion, cancellationToken);

    public Task<bool> ExistsUtilisateurAsync(long idUtilisateur, CancellationToken cancellationToken = default)
        => _context.Utilisateurs.AsNoTracking().AnyAsync(u => u.IdUtilisateur == idUtilisateur, cancellationToken);

    public async Task<bool> WouldCreateCycleAsync(long idVersion, long idVersionPrecedente, CancellationToken cancellationToken = default)
    {
        if (idVersion == idVersionPrecedente)
        {
            return true;
        }

        var currentId = (long?)idVersionPrecedente;
        var guard = 0;
        while (currentId is long id && guard++ < 50)
        {
            if (id == idVersion)
            {
                return true;
            }

            currentId = await _context.VersionsBudgetaires
                .AsNoTracking()
                .Where(v => v.IdVersion == id)
                .Select(v => v.FK_VersionBudgetairePrecedente)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }

    public Task<int> CountPrevisionsAsync(long idVersion, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires.AsNoTracking().CountAsync(p => p.FK_VersionBudgetaire == idVersion, cancellationToken);

    public Task<int> CountTransfertsAsync(long idVersion, CancellationToken cancellationToken = default)
        => _context.TransfertsBudgetaires.AsNoTracking().CountAsync(t => t.FK_VersionBudgetaire == idVersion, cancellationToken);

    public Task<int> CountVersionsSuivantesAsync(long idVersion, CancellationToken cancellationToken = default)
        => _context.VersionsBudgetaires.AsNoTracking().CountAsync(v => v.FK_VersionBudgetairePrecedente == idVersion, cancellationToken);

    public async Task<IReadOnlyList<UtilisateurLookupDto>> GetUtilisateursAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Utilisateurs
            .AsNoTracking()
            .OrderBy(u => u.NomUtilisateur)
            .Select(u => new UtilisateurLookupDto(u.IdUtilisateur, u.NomUtilisateur, u.Nom, u.Prenom, u.Actif))
            .ToListAsync(cancellationToken);
    }

    public async Task<VersionBudgetaireDto> CreateAsync(
        long idExercice,
        int numeroVersion,
        string libelle,
        long? idVersionPrecedente,
        DateOnly dateDebutEffet,
        DateOnly? dateFinEffet,
        string? motif,
        string statut,
        long idUtilisateurCreation,
        CancellationToken cancellationToken = default)
    {
        var entity = new VersionBudgetaire
        {
            FK_ExerciceBudgetaire = idExercice,
            NumeroVersion = numeroVersion,
            Libelle = libelle,
            FK_VersionBudgetairePrecedente = idVersionPrecedente,
            DateCreation = DateTime.Now,
            DateDebutEffet = dateDebutEffet,
            DateFinEffet = dateFinEffet,
            Motif = motif,
            Statut = statut,
            FK_UtilisateurCreation = idUtilisateurCreation,
        };

        _context.VersionsBudgetaires.Add(entity);
        await SauvegarderAsync(numeroVersion, cancellationToken);
        return await RechargerAsync(entity.IdVersion, cancellationToken)
            ?? Map(entity);
    }

    public async Task<VersionBudgetaireDto?> UpdateAsync(
        long idVersion,
        long idExercice,
        int numeroVersion,
        string libelle,
        long? idVersionPrecedente,
        DateOnly dateDebutEffet,
        DateOnly? dateFinEffet,
        string? motif,
        long idUtilisateurCreation,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.VersionsBudgetaires
            .FirstOrDefaultAsync(v => v.IdVersion == idVersion, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        // Statut volontairement non modifié ici (workflow uniquement).
        entity.FK_ExerciceBudgetaire = idExercice;
        entity.NumeroVersion = numeroVersion;
        entity.Libelle = libelle;
        entity.FK_VersionBudgetairePrecedente = idVersionPrecedente;
        entity.DateDebutEffet = dateDebutEffet;
        entity.DateFinEffet = dateFinEffet;
        entity.Motif = motif;
        entity.FK_UtilisateurCreation = idUtilisateurCreation;
        await SauvegarderAsync(numeroVersion, cancellationToken);
        return await RechargerAsync(idVersion, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idVersion, CancellationToken cancellationToken = default)
    {
        var entity = await _context.VersionsBudgetaires
            .FirstOrDefaultAsync(v => v.IdVersion == idVersion, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _context.VersionsBudgetaires.Remove(entity);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationFk(ex))
        {
            throw new InvalidOperationException(MessageSuppressionImpossible(ex));
        }

        return true;
    }

    public async Task<VersionBudgetaireDto?> AppliquerTransitionAsync(
        long idVersion,
        string nouveauStatut,
        long idUtilisateur,
        string operation,
        Action<VersionBudgetaire> appliquerTrace,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await _context.VersionsBudgetaires
                .FirstOrDefaultAsync(v => v.IdVersion == idVersion, cancellationToken);
            if (entity is null)
            {
                return null;
            }

            var ancien = entity.Statut;
            entity.Statut = nouveauStatut;
            appliquerTrace(entity);

            _context.JournalAudits.Add(new JournalAudit
            {
                FK_Utilisateur = idUtilisateur,
                DateHeure = DateTime.Now,
                Operation = operation,
                Entite = "VERSION_BUDGETAIRE",
                IdEntite = idVersion,
                AnciennesValeurs = JsonSerializer.Serialize(new { statut = ancien }),
                NouvellesValeurs = JsonSerializer.Serialize(new { statut = nouveauStatut }),
            });

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return await RechargerAsync(idVersion, cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private IQueryable<VersionBudgetaire> Query()
        => _context.VersionsBudgetaires
            .AsNoTracking()
            .Include(v => v.ExerciceBudgetaire)
            .Include(v => v.VersionPrecedente)
            .Include(v => v.UtilisateurCreation)
            .Include(v => v.UtilisateurValidation)
            .Include(v => v.UtilisateurSoumission)
            .Include(v => v.UtilisateurControle)
            .Include(v => v.UtilisateurRejet)
            .Include(v => v.PrevisionsBudgetaires)
            .Include(v => v.TransfertsBudgetaires)
            .Include(v => v.VersionsSuivantes);

    private async Task<VersionBudgetaireDto?> RechargerAsync(long idVersion, CancellationToken cancellationToken)
    {
        var entity = await Query().FirstOrDefaultAsync(v => v.IdVersion == idVersion, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    private async Task SauvegarderAsync(int numeroVersion, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EstViolationUnique(ex))
        {
            throw new InvalidOperationException($"La version n° {numeroVersion} existe déjà pour cet exercice.");
        }
        catch (DbUpdateException ex) when (EstViolationCheck(ex))
        {
            throw new InvalidOperationException(MessageCheck(ex));
        }
        catch (DbUpdateException ex) when (EstViolationFk(ex))
        {
            throw new InvalidOperationException("L'exercice, la version précédente ou l'utilisateur indiqué n'existe pas.");
        }
    }

    private static VersionBudgetaireDto Map(VersionBudgetaire entity)
        => new(
            entity.IdVersion,
            entity.FK_ExerciceBudgetaire,
            entity.ExerciceBudgetaire.Annee,
            entity.ExerciceBudgetaire.Statut,
            entity.NumeroVersion,
            entity.Libelle,
            entity.FK_VersionBudgetairePrecedente,
            entity.VersionPrecedente?.NumeroVersion,
            entity.VersionPrecedente?.Libelle,
            entity.DateCreation,
            entity.DateDebutEffet,
            entity.DateFinEffet,
            entity.Motif,
            entity.Statut,
            entity.FK_UtilisateurCreation,
            entity.UtilisateurCreation.NomUtilisateur,
            entity.FK_UtilisateurValidation,
            entity.UtilisateurValidation?.NomUtilisateur,
            entity.DateValidation,
            entity.FK_UtilisateurSoumission,
            entity.UtilisateurSoumission?.NomUtilisateur,
            entity.DateSoumission,
            entity.FK_UtilisateurControle,
            entity.UtilisateurControle?.NomUtilisateur,
            entity.DateControle,
            entity.FK_UtilisateurRejet,
            entity.UtilisateurRejet?.NomUtilisateur,
            entity.DateRejet,
            entity.MotifRejet,
            entity.PrevisionsBudgetaires.Count,
            entity.TransfertsBudgetaires.Count,
            entity.VersionsSuivantes.Count);

    private static bool EstViolationUnique(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UQ_VERSION_Exercice_Numero", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationCheck(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("CK_VERSION_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("CHECK", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EstViolationFk(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("FK_VERSION_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FK_PREVISION_VERSION", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FK_TRANSFERT_VERSION", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static string MessageCheck(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("CK_VERSION_Numero", StringComparison.OrdinalIgnoreCase))
        {
            return "Le numéro de version doit être supérieur à 0.";
        }

        if (message.Contains("CK_VERSION_Statut", StringComparison.OrdinalIgnoreCase))
        {
            return "Le statut de la version doit être BROUILLON, SOUMISE, CONTROLEE, VALIDEE ou REJETEE.";
        }

        if (message.Contains("CK_VERSION_Dates", StringComparison.OrdinalIgnoreCase))
        {
            return "La date de fin d'effet ne peut pas être antérieure à la date de début d'effet.";
        }

        return "Les données de la version ne respectent pas les règles de la base.";
    }

    private static string MessageSuppressionImpossible(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("FK_VERSION_PRECEDENTE", StringComparison.OrdinalIgnoreCase))
        {
            return "Cette version ne peut pas être supprimée car elle est utilisée comme version précédente d'une ou plusieurs autres versions.";
        }

        if (message.Contains("FK_PREVISION_VERSION", StringComparison.OrdinalIgnoreCase))
        {
            return "Cette version ne peut pas être supprimée car elle est utilisée par une ou plusieurs prévisions budgétaires.";
        }

        if (message.Contains("FK_TRANSFERT_VERSION", StringComparison.OrdinalIgnoreCase))
        {
            return "Cette version ne peut pas être supprimée car elle est utilisée par un ou plusieurs transferts budgétaires.";
        }

        return "Cette version ne peut pas être supprimée car elle est utilisée par d'autres données.";
    }
}
