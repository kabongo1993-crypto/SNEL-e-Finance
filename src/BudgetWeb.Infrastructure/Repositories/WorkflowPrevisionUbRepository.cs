using System.Text.Json;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class WorkflowPrevisionUbRepository : IWorkflowPrevisionUbRepository
{
    private readonly BudgetDbContext _context;

    public WorkflowPrevisionUbRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task EnsureExistsAsync(
        long idVersion,
        long idUB,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        var exists = await _context.WorkflowsPrevisionUb.AsNoTracking()
            .AnyAsync(w => w.FK_VersionBudgetaire == idVersion && w.FK_UniteBudgetaire == idUB, cancellationToken);
        if (exists)
        {
            return;
        }

        // Toujours BROUILLON pour une nouvelle UB — ne jamais copier VERSION.Statut.
        _context.WorkflowsPrevisionUb.Add(new WorkflowPrevisionUb
        {
            FK_VersionBudgetaire = idVersion,
            FK_UniteBudgetaire = idUB,
            Statut = StatutVersionBudgetaire.Brouillon,
            FK_UtilisateurCreation = idUtilisateur,
            DateCreation = DateTime.Now,
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetStatutAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowsPrevisionUb.AsNoTracking()
            .Where(w => w.FK_VersionBudgetaire == idVersion && w.FK_UniteBudgetaire == idUB)
            .Select(w => w.Statut)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkflowPrevisionUbDto?> GetAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        var w = await _context.WorkflowsPrevisionUb.AsNoTracking()
            .Include(x => x.UniteBudgetaire).ThenInclude(u => u.Departement)
            .Include(x => x.UtilisateurSoumission)
            .Include(x => x.UtilisateurControle)
            .Include(x => x.UtilisateurValidation)
            .Include(x => x.UtilisateurRejet)
            .FirstOrDefaultAsync(
                x => x.FK_VersionBudgetaire == idVersion && x.FK_UniteBudgetaire == idUB,
                cancellationToken);
        return w is null ? null : MapDto(w);
    }

    public Task<bool> ExistsPrevisionForPairAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires.AsNoTracking()
            .AnyAsync(p => p.FK_VersionBudgetaire == idVersion && p.FK_UniteBudgetaire == idUB, cancellationToken);

    public Task<bool> ExistsVersionAsync(long idVersion, CancellationToken cancellationToken = default)
        => _context.VersionsBudgetaires.AsNoTracking().AnyAsync(v => v.IdVersion == idVersion, cancellationToken);

    public Task<bool> ExistsUbAsync(long idUB, CancellationToken cancellationToken = default)
        => _context.UnitesBudgetaires.AsNoTracking().AnyAsync(u => u.IdUB == idUB, cancellationToken);

    public async Task<(long IdDepartement, string CodeDepartement, string LibelleDepartement)?> GetUbDepartementAsync(
        long idUB,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => u.IdUB == idUB)
            .Select(u => new
            {
                IdDepartement = u.FK_Departement,
                CodeDepartement = u.Departement.Code,
                LibelleDepartement = u.Departement.Libelle,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.IdDepartement, row.CodeDepartement, row.LibelleDepartement);
    }

    public async Task TransitionnerUbAsync(
        long idVersion,
        long idUB,
        string nouveauStatut,
        long idUtilisateur,
        string operationAudit,
        string portee,
        Action<WorkflowPrevisionUb> appliquerTrace,
        object? auditExtra,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await _context.WorkflowsPrevisionUb
                .FirstOrDefaultAsync(
                    w => w.FK_VersionBudgetaire == idVersion && w.FK_UniteBudgetaire == idUB,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Aucun workflow Version×UB trouvé. Enregistrez d'abord une prévision pour cette UB.");

            var ancien = entity.Statut;
            if (!StatutVersionBudgetaire.EstTransitionAutorisee(ancien, nouveauStatut))
            {
                throw new InvalidOperationException(
                    $"Transition interdite : {StatutVersionBudgetaire.Normaliser(ancien)} → {StatutVersionBudgetaire.Normaliser(nouveauStatut)}.");
            }

            entity.Statut = StatutVersionBudgetaire.Normaliser(nouveauStatut);
            entity.FK_UtilisateurModification = idUtilisateur;
            entity.DateModification = DateTime.Now;
            appliquerTrace(entity);

            var dept = await GetUbDepartementAsync(idUB, cancellationToken);
            var payload = new Dictionary<string, object?>
            {
                ["idVersion"] = idVersion,
                ["idUB"] = idUB,
                ["idDepartement"] = dept?.IdDepartement,
                ["codeDepartement"] = dept?.CodeDepartement,
                ["portee"] = portee,
                ["statutAvant"] = ancien,
                ["statutApres"] = entity.Statut,
            };
            if (auditExtra is not null)
            {
                payload["extra"] = auditExtra;
            }

            _context.JournalAudits.Add(new JournalAudit
            {
                FK_Utilisateur = idUtilisateur,
                DateHeure = DateTime.Now,
                Operation = operationAudit,
                Entite = "WORKFLOW_PREVISION_UB",
                IdEntite = entity.IdWorkflowPrevisionUB,
                AnciennesValeurs = JsonSerializer.Serialize(new { statut = ancien }),
                NouvellesValeurs = JsonSerializer.Serialize(payload),
            });

            await _context.SaveChangesAsync(cancellationToken);
            await RecalculerStatutVersionInternalAsync(idVersion, idUtilisateur, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<WorkflowDepartementBulkResultDto> SoumettreDepartementAsync(
        long idVersion,
        long idDepartement,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        // Créer les lignes workflow manquantes (BROUILLON) pour les UB du département ayant des prévisions.
        var ubIds = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p =>
                p.FK_VersionBudgetaire == idVersion
                && p.UniteBudgetaire.FK_Departement == idDepartement)
            .Select(p => p.FK_UniteBudgetaire)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var idUb in ubIds)
        {
            await EnsureExistsAsync(idVersion, idUb, idUtilisateur, cancellationToken);
        }

        return await TransitionnerDepartementAsync(
            idVersion,
            idDepartement,
            idUtilisateur,
            operationAudit: "SOUMISSION_DEPARTEMENT",
            motif: null,
            eligible: s => s is StatutVersionBudgetaire.Brouillon or StatutVersionBudgetaire.Rejetee,
            appliquer: (w, uid) =>
            {
                w.Statut = StatutVersionBudgetaire.Soumise;
                w.FK_UtilisateurSoumission = uid;
                w.DateSoumission = DateTime.Now;
            },
            outcomeTraite: "SOUMISE",
            cancellationToken);
    }

    public Task<WorkflowDepartementBulkResultDto> ControlerDepartementAsync(
        long idVersion,
        long idDepartement,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
        => TransitionnerDepartementAsync(
            idVersion,
            idDepartement,
            idUtilisateur,
            operationAudit: "CONTROLE_DEPARTEMENT",
            motif: null,
            eligible: s => s == StatutVersionBudgetaire.Soumise,
            appliquer: (w, uid) =>
            {
                w.Statut = StatutVersionBudgetaire.Controlee;
                w.FK_UtilisateurControle = uid;
                w.DateControle = DateTime.Now;
            },
            outcomeTraite: "CONTROLEE",
            cancellationToken);

    public Task<WorkflowDepartementBulkResultDto> ValiderDepartementAsync(
        long idVersion,
        long idDepartement,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
        => TransitionnerDepartementAsync(
            idVersion,
            idDepartement,
            idUtilisateur,
            operationAudit: "VALIDATION_DEPARTEMENT",
            motif: null,
            eligible: s => s == StatutVersionBudgetaire.Controlee,
            appliquer: (w, uid) =>
            {
                w.Statut = StatutVersionBudgetaire.Validee;
                w.FK_UtilisateurValidation = uid;
                w.DateValidation = DateTime.Now;
            },
            outcomeTraite: "VALIDEE",
            cancellationToken);

    public Task<WorkflowDepartementBulkResultDto> RejeterDepartementAsync(
        long idVersion,
        long idDepartement,
        string motif,
        long idUtilisateur,
        CancellationToken cancellationToken = default)
        => TransitionnerDepartementAsync(
            idVersion,
            idDepartement,
            idUtilisateur,
            operationAudit: "REJET_DEPARTEMENT",
            motif,
            eligible: s => s is StatutVersionBudgetaire.Soumise or StatutVersionBudgetaire.Controlee,
            appliquer: (w, uid) =>
            {
                w.Statut = StatutVersionBudgetaire.Rejetee;
                w.FK_UtilisateurRejet = uid;
                w.DateRejet = DateTime.Now;
                w.MotifRejet = motif;
            },
            outcomeTraite: "REJETEE",
            cancellationToken);

    private async Task<WorkflowDepartementBulkResultDto> TransitionnerDepartementAsync(
        long idVersion,
        long idDepartement,
        long idUtilisateur,
        string operationAudit,
        string? motif,
        Func<string, bool> eligible,
        Action<WorkflowPrevisionUb, long> appliquer,
        string outcomeTraite,
        CancellationToken cancellationToken)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var workflows = await _context.WorkflowsPrevisionUb
                .Include(w => w.UniteBudgetaire)
                .Where(w =>
                    w.FK_VersionBudgetaire == idVersion
                    && w.UniteBudgetaire.FK_Departement == idDepartement)
                .ToListAsync(cancellationToken);

            var details = new List<WorkflowUbRejetDetailDto>();
            var traitees = 0;
            var ignorees = 0;
            var ubTraitees = new List<object>();

            foreach (var w in workflows)
            {
                var avant = StatutVersionBudgetaire.Normaliser(w.Statut);
                if (eligible(avant))
                {
                    appliquer(w, idUtilisateur);
                    w.FK_UtilisateurModification = idUtilisateur;
                    w.DateModification = DateTime.Now;
                    traitees++;
                    details.Add(new WorkflowUbRejetDetailDto(
                        w.FK_UniteBudgetaire, w.UniteBudgetaire.CodeUB, avant, w.Statut, outcomeTraite));
                    ubTraitees.Add(new { idUB = w.FK_UniteBudgetaire, codeUB = w.UniteBudgetaire.CodeUB, statutAvant = avant });
                }
                else
                {
                    ignorees++;
                    details.Add(new WorkflowUbRejetDetailDto(
                        w.FK_UniteBudgetaire, w.UniteBudgetaire.CodeUB, avant, null, "IGNORE"));
                }
            }

            var dept = await _context.Departements.AsNoTracking()
                .Where(d => d.IdDepartement == idDepartement)
                .Select(d => new { d.Code, d.Libelle })
                .FirstOrDefaultAsync(cancellationToken);

            _context.JournalAudits.Add(new JournalAudit
            {
                FK_Utilisateur = idUtilisateur,
                DateHeure = DateTime.Now,
                Operation = operationAudit,
                Entite = "WORKFLOW_PREVISION_UB",
                IdEntite = idVersion,
                AnciennesValeurs = null,
                NouvellesValeurs = JsonSerializer.Serialize(new
                {
                    idVersion,
                    idDepartement,
                    codeDepartement = dept?.Code,
                    libelleDepartement = dept?.Libelle,
                    portee = "DEPARTEMENT",
                    motif,
                    traitees,
                    ignorees,
                    ub = ubTraitees,
                }),
            });

            await _context.SaveChangesAsync(cancellationToken);
            await RecalculerStatutVersionInternalAsync(idVersion, idUtilisateur, cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new WorkflowDepartementBulkResultDto(traitees, ignorees, details);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task RecalculerStatutVersionAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default)
        => RecalculerStatutVersionInternalAsync(idVersion, idUtilisateur, cancellationToken);

    public async Task<IReadOnlyList<string>> ListStatutsUbAsync(long idVersion, CancellationToken cancellationToken = default)
    {
        return await _context.WorkflowsPrevisionUb.AsNoTracking()
            .Where(w => w.FK_VersionBudgetaire == idVersion)
            .Select(w => w.Statut)
            .ToListAsync(cancellationToken);
    }

    private async Task RecalculerStatutVersionInternalAsync(
        long idVersion,
        long idUtilisateur,
        CancellationToken cancellationToken)
    {
        // Uniquement les UB ayant réellement une prévision dans la version.
        var statuts = await (
            from w in _context.WorkflowsPrevisionUb
            where w.FK_VersionBudgetaire == idVersion
            where _context.PrevisionsBudgetaires.Any(p =>
                p.FK_VersionBudgetaire == idVersion
                && p.FK_UniteBudgetaire == w.FK_UniteBudgetaire)
            select w.Statut
        ).ToListAsync(cancellationToken);

        var agrege = VersionStatutAgrege.Calculer(statuts);
        var version = await _context.VersionsBudgetaires
            .FirstOrDefaultAsync(v => v.IdVersion == idVersion, cancellationToken);
        if (version is null)
        {
            return;
        }

        var ancien = version.Statut;
        if (string.Equals(StatutVersionBudgetaire.Normaliser(ancien), agrege, StringComparison.Ordinal))
        {
            return;
        }

        version.Statut = agrege;
        _context.JournalAudits.Add(new JournalAudit
        {
            FK_Utilisateur = idUtilisateur,
            DateHeure = DateTime.Now,
            Operation = "AGREGAT_STATUT_VERSION",
            Entite = "VERSION_BUDGETAIRE",
            IdEntite = idVersion,
            AnciennesValeurs = JsonSerializer.Serialize(new { statut = ancien }),
            NouvellesValeurs = JsonSerializer.Serialize(new { statut = agrege, source = "WORKFLOW_PREVISION_UB" }),
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static WorkflowPrevisionUbDto MapDto(WorkflowPrevisionUb w)
    {
        static string? NomComplet(Utilisateur? u)
        {
            if (u is null) return null;
            var n = $"{u.Nom} {u.Prenom}".Trim();
            return string.IsNullOrWhiteSpace(n) ? null : n;
        }

        return new WorkflowPrevisionUbDto(
            w.IdWorkflowPrevisionUB,
            w.FK_VersionBudgetaire,
            w.FK_UniteBudgetaire,
            w.UniteBudgetaire.CodeUB,
            w.UniteBudgetaire.Libelle,
            w.UniteBudgetaire.FK_Departement,
            w.UniteBudgetaire.Departement.Code,
            w.UniteBudgetaire.Departement.Libelle,
            w.Statut,
            w.DateSoumission,
            NomComplet(w.UtilisateurSoumission),
            w.DateControle,
            NomComplet(w.UtilisateurControle),
            w.DateValidation,
            NomComplet(w.UtilisateurValidation),
            w.DateRejet,
            NomComplet(w.UtilisateurRejet),
            w.MotifRejet);
    }
}
