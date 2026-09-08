using System.Text.Json;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using BudgetWeb.Infrastructure.Diagnostics;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository : IDemandePaiementRepository
{
    private readonly BudgetDbContext _context;
    private readonly IPerimetreUtilisateurReader _perimetre;
    private readonly ILogger<DemandePaiementRepository> _logger;

    public DemandePaiementRepository(
        BudgetDbContext context,
        IPerimetreUtilisateurReader perimetre,
        ILogger<DemandePaiementRepository> logger)
    {
        _context = context;
        _perimetre = perimetre;
        _logger = logger;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
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

    public async Task<IReadOnlyList<DemandePaiement>> ListAsync(
        DemandePaiementQuery query,
        CancellationToken cancellationToken = default)
    {
        // Requête 1 : table DPM seule (évite memory grant / RESOURCE_SEMAPHORE des multi-JOIN).
        IQueryable<DemandePaiement> q = ApplyListQueryFilters(
            _context.DemandesPaiement.AsNoTracking(),
            query);

        var headers = await q
            .OrderByDescending(d => d.DateCreation)
            .ThenByDescending(d => d.IdDemandePaiement)
            .Select(d => new
            {
                d.IdDemandePaiement,
                d.Reference,
                d.DateEmission,
                d.FK_ExerciceBudgetaire,
                d.FK_UniteBudgetaire,
                d.FK_Demandeur,
                d.FK_CasDossier,
                d.Objet,
                d.MontantBrut,
                d.Devise,
                d.FK_Devise,
                d.MontantUsd,
                d.FK_TypeBudget,
                d.TypeBudgetSollicite,
                d.ItemSollicite,
                d.ModePaiementSollicite,
                d.TypeInstrumentPaiement,
                d.Statut,
                d.DateSoumission,
                d.DateCreation,
                d.FK_UtilisateurCreation,
                d.FK_UtilisateurAssigne,
            })
            .ToListAsync(cancellationToken);

        if (headers.Count == 0)
            return Array.Empty<DemandePaiement>();

        var idUbs = headers.Select(h => h.FK_UniteBudgetaire).Distinct().ToList();
        var idExercices = headers.Select(h => h.FK_ExerciceBudgetaire).Distinct().ToList();
        var idCasList = headers.Select(h => h.FK_CasDossier).Distinct().ToList();
        var idDemandeurs = headers.Where(h => h.FK_Demandeur is not null).Select(h => h.FK_Demandeur!.Value).Distinct().ToList();
        var idTypes = headers.Where(h => h.FK_TypeBudget is not null).Select(h => h.FK_TypeBudget!.Value).Distinct().ToList();

        var ubs = await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => idUbs.Contains(u.IdUB))
            .Select(u => new
            {
                u.IdUB,
                u.CodeUB,
                u.Libelle,
                u.FK_Departement,
                IdDepartement = (long?)u.Departement!.IdDepartement,
                CodeDepartement = u.Departement!.Code,
                LibelleDepartement = u.Departement!.Libelle,
            })
            .ToListAsync(cancellationToken);
        var ubMap = ubs.ToDictionary(u => u.IdUB);

        var exercices = await _context.ExercicesBudgetaires.AsNoTracking()
            .Where(e => idExercices.Contains(e.IdExercice))
            .Select(e => new { e.IdExercice, e.Annee })
            .ToListAsync(cancellationToken);
        var exMap = exercices.ToDictionary(e => e.IdExercice);

        var casList = await _context.CasDossiers.AsNoTracking()
            .Where(c => idCasList.Contains(c.IdCasDossier))
            .Select(c => new { c.IdCasDossier, c.Code, c.Libelle })
            .ToListAsync(cancellationToken);
        var casMap = casList.ToDictionary(c => c.IdCasDossier);

        var demandeurs = idDemandeurs.Count == 0
            ? []
            : await _context.Demandeurs.AsNoTracking()
                .Where(d => idDemandeurs.Contains(d.IdDemandeur))
                .Select(d => new { d.IdDemandeur, d.Code, d.Libelle })
                .ToListAsync(cancellationToken);
        var demMap = demandeurs.ToDictionary(d => d.IdDemandeur);

        var types = idTypes.Count == 0
            ? []
            : await _context.TypesBudget.AsNoTracking()
                .Where(t => idTypes.Contains(t.IdTypeBudget))
                .Select(t => new { t.IdTypeBudget, t.CodeType })
                .ToListAsync(cancellationToken);
        var typeMap = types.ToDictionary(t => t.IdTypeBudget);

        var idAssignes = headers
            .Where(h => h.FK_UtilisateurAssigne is not null)
            .Select(h => h.FK_UtilisateurAssigne!.Value)
            .Distinct()
            .ToList();
        var assignes = idAssignes.Count == 0
            ? []
            : await _context.Utilisateurs.AsNoTracking()
                .Where(u => idAssignes.Contains(u.IdUtilisateur))
                .Select(u => new { u.IdUtilisateur, u.Nom, u.Prenom })
                .ToListAsync(cancellationToken);
        var assigneMap = assignes.ToDictionary(u => u.IdUtilisateur);

        return headers.Select(h =>
        {
            ubMap.TryGetValue(h.FK_UniteBudgetaire, out var ub);
            exMap.TryGetValue(h.FK_ExerciceBudgetaire, out var ex);
            casMap.TryGetValue(h.FK_CasDossier, out var cas);
            Demandeur? dem = null;
            if (h.FK_Demandeur is long idDemandeur && demMap.TryGetValue(idDemandeur, out var demRow))
            {
                dem = new Demandeur
                {
                    IdDemandeur = demRow.IdDemandeur,
                    Code = demRow.Code,
                    Libelle = demRow.Libelle,
                };
            }

            TypeBudget? type = null;
            if (h.FK_TypeBudget is long idTypeBudget && typeMap.TryGetValue(idTypeBudget, out var typeRow))
            {
                type = new TypeBudget
                {
                    IdTypeBudget = typeRow.IdTypeBudget,
                    CodeType = typeRow.CodeType,
                };
            }

            Utilisateur? assigne = null;
            if (h.FK_UtilisateurAssigne is long idAssigne
                && assigneMap.TryGetValue(idAssigne, out var assigneRow))
            {
                assigne = new Utilisateur
                {
                    IdUtilisateur = assigneRow.IdUtilisateur,
                    Nom = assigneRow.Nom,
                    Prenom = assigneRow.Prenom,
                };
            }

            return new DemandePaiement
            {
                IdDemandePaiement = h.IdDemandePaiement,
                Reference = h.Reference,
                DateEmission = h.DateEmission,
                FK_ExerciceBudgetaire = h.FK_ExerciceBudgetaire,
                FK_UniteBudgetaire = h.FK_UniteBudgetaire,
                FK_Demandeur = h.FK_Demandeur,
                FK_CasDossier = h.FK_CasDossier,
                Objet = h.Objet,
                MontantBrut = h.MontantBrut,
                Devise = h.Devise,
                FK_Devise = h.FK_Devise,
                MontantUsd = h.MontantUsd,
                FK_TypeBudget = h.FK_TypeBudget,
                TypeBudgetSollicite = h.TypeBudgetSollicite,
                ItemSollicite = h.ItemSollicite,
                ModePaiementSollicite = h.ModePaiementSollicite,
                TypeInstrumentPaiement = h.TypeInstrumentPaiement,
                Statut = h.Statut,
                DateSoumission = h.DateSoumission,
                DateCreation = h.DateCreation,
                FK_UtilisateurCreation = h.FK_UtilisateurCreation,
                FK_UtilisateurAssigne = h.FK_UtilisateurAssigne,
                UtilisateurAssigne = assigne,
                ExerciceBudgetaire = new ExerciceBudgetaire
                {
                    IdExercice = h.FK_ExerciceBudgetaire,
                    Annee = ex?.Annee ?? (short)0,
                },
                UniteBudgetaire = new UniteBudgetaire
                {
                    IdUB = h.FK_UniteBudgetaire,
                    CodeUB = ub?.CodeUB ?? string.Empty,
                    Libelle = ub?.Libelle ?? string.Empty,
                    FK_Departement = ub?.FK_Departement ?? 0,
                    Departement = new Departement
                    {
                        IdDepartement = ub?.IdDepartement ?? ub?.FK_Departement ?? 0,
                        Code = ub?.CodeDepartement ?? string.Empty,
                        Libelle = ub?.LibelleDepartement ?? string.Empty,
                    },
                },
                Demandeur = dem,
                CasDossier = new CasDossier
                {
                    IdCasDossier = h.FK_CasDossier,
                    Code = cas?.Code ?? string.Empty,
                    Libelle = cas?.Libelle ?? string.Empty,
                },
                TypeBudget = type,
            };
        }).ToList();
    }

    public Task<DemandePaiement?> GetByIdAsync(long idDemande, CancellationToken cancellationToken = default)
        => TrackRepoAsync("GetByIdAsync", () =>
            BaseQuery().FirstOrDefaultAsync(d => d.IdDemandePaiement == idDemande, cancellationToken));

    public Task<DemandePaiement?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var refNorm = reference.Trim();
        return BaseQuery().FirstOrDefaultAsync(d => d.Reference == refNorm, cancellationToken);
    }

    public Task<DemandePaiement?> GetTrackedAsync(long idDemande, CancellationToken cancellationToken = default)
        => TrackRepoAsync("GetTrackedAsync", () =>
            _context.DemandesPaiement
                .Include(d => d.Beneficiaires)
                .Include(d => d.ValidationsEntite)
                .Include(d => d.PiecesJointes)
                .Include(d => d.Imputations)
                .Include(d => d.BilletConversion)
                .Include(d => d.PieceCaisse)
                .Include(d => d.BonProvisoire)
                .Include(d => d.MinuteCheque)
                .FirstOrDefaultAsync(d => d.IdDemandePaiement == idDemande, cancellationToken));

    public Task<DemandePaiement?> GetDetailAsync(long idDemande, CancellationToken cancellationToken = default)
        => TrackRepoAsync("GetDetailAsync", () => GetDetailAsyncCore(idDemande, cancellationToken));

    private async Task<DemandePaiement?> GetDetailAsyncCore(long idDemande, CancellationToken cancellationToken)
    {
        using var sqlScope = DetailQuerySqlPerfScope.Begin(_logger);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return await DetailQuery()
                .FirstOrDefaultAsync(d => d.IdDemandePaiement == idDemande, cancellationToken);
        }
        finally
        {
            sw.Stop();
            sqlScope.LogSummary(sw.ElapsedMilliseconds);
        }
    }

    public Task<string> GenererReferenceAsync(
        short anneeExercice,
        CancellationToken cancellationToken = default)
        => TrackRepoAsync("GenererReferenceAsync", () => GenererReferenceCoreAsync(anneeExercice, cancellationToken));

    private async Task<string> GenererReferenceCoreAsync(
        short anneeExercice,
        CancellationToken cancellationToken)
    {
        var prefix = $"DP-{anneeExercice}-";
        var references = await _context.DemandesPaiement.AsNoTracking()
            .Where(d => d.Reference.StartsWith(prefix))
            .Select(d => d.Reference)
            .ToListAsync(cancellationToken);

        var maxNum = 0;
        foreach (var reference in references)
        {
            if (reference.Length > prefix.Length
                && int.TryParse(reference.AsSpan(prefix.Length), out var num)
                && num > maxNum)
            {
                maxNum = num;
            }
        }

        return $"{prefix}{maxNum + 1:D5}";
    }

    public Task<long?> ResolveVersionBudgetaireValideeAsync(
        long idExercice,
        long idUB,
        CancellationToken cancellationToken = default)
        => _context.VersionsBudgetaires.AsNoTracking()
            .Where(v => v.FK_ExerciceBudgetaire == idExercice
                        && _context.WorkflowsPrevisionUb.Any(w =>
                            w.FK_VersionBudgetaire == v.IdVersion
                            && w.FK_UniteBudgetaire == idUB
                            && w.Statut == StatutVersionBudgetaire.Validee))
            .OrderByDescending(v => v.NumeroVersion)
            .Select(v => (long?)v.IdVersion)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> GetWorkflowStatutUbAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
        => _context.WorkflowsPrevisionUb.AsNoTracking()
            .Where(w => w.FK_VersionBudgetaire == idVersion && w.FK_UniteBudgetaire == idUB)
            .Select(w => w.Statut)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<TypeBudget?> GetTypeBudgetAsync(long idTypeBudget, CancellationToken cancellationToken = default)
        => _context.TypesBudget.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdTypeBudget == idTypeBudget, cancellationToken);

    public Task<TypeBudget?> GetTypeBudgetByCodeAsync(string codeType, CancellationToken cancellationToken = default)
    {
        var code = (codeType ?? string.Empty).Trim().ToUpperInvariant();
        return _context.TypesBudget.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CodeType == code, cancellationToken);
    }

    public Task<PrevisionBudgetaire?> GetPrevisionAsync(long idPrevision, CancellationToken cancellationToken = default)
        => _context.PrevisionsBudgetaires.AsNoTracking()
            .Include(p => p.VersionBudgetaire).ThenInclude(v => v.ExerciceBudgetaire)
            .Include(p => p.UniteBudgetaire).ThenInclude(u => u.Departement)
            .Include(p => p.TypeBudget)
            .Include(p => p.RubriqueBudgetaire)
            .Include(p => p.ItemBI)
            .Include(p => p.ModePrevision)
            .Include(p => p.RepartitionsMensuelles)
            .FirstOrDefaultAsync(p => p.IdPrevision == idPrevision, cancellationToken);

    public async Task<long?> FindPrevisionIdAsync(
        DemandePaiementImputation imputation,
        long idVersion,
        CancellationToken cancellationToken = default)
    {
        if (imputation.FK_BudgetLigne is long idLigne)
            return idLigne;

        var forme = DemandePaiementImputationRules.DetecterForme(imputation);

        return forme switch
        {
            FormeImputationBudgetaire.DepensesCourantes => await _context.PrevisionsBudgetaires.AsNoTracking()
                .Where(p => p.FK_VersionBudgetaire == idVersion
                            && p.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                            && p.FK_RubriqueBudgetaire == imputation.FK_RubriqueBudgetaire)
                .Select(p => (long?)p.IdPrevision)
                .FirstOrDefaultAsync(cancellationToken),

            FormeImputationBudgetaire.ActionsExploitation => await _context.PrevisionsBudgetaires.AsNoTracking()
                .Where(p => p.FK_VersionBudgetaire == idVersion
                            && p.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                            && p.FK_RubriqueBudgetaire == imputation.FK_RubriqueBudgetaire
                            && p.LibelleItemAE == imputation.LibelleItemAE!.Trim())
                .Select(p => (long?)p.IdPrevision)
                .FirstOrDefaultAsync(cancellationToken),

            FormeImputationBudgetaire.BudgetInvestissement => await FindPrevisionBiAsync(imputation, idVersion, cancellationToken),

            _ => null
        };
    }

    private async Task<long?> FindPrevisionBiAsync(
        DemandePaiementImputation imputation,
        long idVersion,
        CancellationToken cancellationToken)
        => await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                        && p.FK_ItemBI == imputation.FK_ItemBI
                        && p.DetailBI == imputation.DetailBI!.Trim())
            .Select(p => (long?)p.IdPrevision)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<decimal> SumEngageDcMensuelAsync(
        long idExercice,
        long idUB,
        long idRB,
        byte mois,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
        => EngageQuery(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire == idRB
                        && i.Mois == mois)
            .SumAsync(i => i.MontantUsd, cancellationToken);

    public Task<decimal> SumEngageDcAnnuelAsync(
        long idExercice,
        long idUB,
        long idRB,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
        => EngageQuery(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire == idRB
                        && i.Mois != null)
            .SumAsync(i => i.MontantUsd, cancellationToken);

    public Task<decimal> SumEngageAeAnnuelAsync(
        long idExercice,
        long idUB,
        long idRB,
        string libelleItemAE,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
    {
        var action = libelleItemAE.Trim();
        return EngageQuery(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire == idRB
                        && i.LibelleItemAE == action)
            .SumAsync(i => i.MontantUsd, cancellationToken);
    }

    public Task<decimal> SumEngageBiAnnuelAsync(
        long idExercice,
        long idUB,
        long idItemBI,
        string detailBI,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
    {
        var detail = detailBI.Trim();
        return EngageQuery(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_ItemBI == idItemBI
                        && i.DetailBI == detail)
            .SumAsync(i => i.MontantUsd, cancellationToken);
    }

    public Task<DemandePaiement> AddAsync(
        DemandePaiement entity,
        CancellationToken cancellationToken = default)
        => TrackRepoAsync("AddAsync", async () =>
        {
            _context.DemandesPaiement.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return entity;
        });

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => TrackRepoAsync("SaveChangesAsync", () => _context.SaveChangesAsync(cancellationToken));

    public void ClearChangeTracker() => _context.ChangeTracker.Clear();

    public Task<DemandePaiementImputation?> GetImputationTrackedAsync(
        long idImputation,
        CancellationToken cancellationToken = default)
        => _context.DemandePaiementImputations
            .FirstOrDefaultAsync(i => i.IdImputation == idImputation, cancellationToken);

    public async Task AddImputationAsync(
        DemandePaiementImputation imputation,
        CancellationToken cancellationToken = default)
    {
        _context.DemandePaiementImputations.Add(imputation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveImputationAsync(
        DemandePaiementImputation imputation,
        CancellationToken cancellationToken = default)
    {
        _context.DemandePaiementImputations.Remove(imputation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task AddPieceAsync(PieceJointe piece, CancellationToken cancellationToken = default)
    {
        _context.PiecesJointes.Add(piece);
        return Task.CompletedTask;
    }

    public Task<PieceJointe?> GetPieceTrackedAsync(long idPiece, CancellationToken cancellationToken = default)
        => _context.PiecesJointes
            .FirstOrDefaultAsync(p => p.IdPieceJointe == idPiece, cancellationToken);

    public async Task RemovePieceAsync(PieceJointe piece, CancellationToken cancellationToken = default)
    {
        _context.PiecesJointes.Remove(piece);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task ReplaceBeneficiairesAsync(
        long idDemande,
        IReadOnlyList<DemandePaiementBeneficiaire> beneficiaires,
        CancellationToken cancellationToken = default)
        => TrackRepoAsync("ReplaceBeneficiairesAsync", () =>
            ReplaceBeneficiairesCoreAsync(idDemande, beneficiaires, cancellationToken));

    private async Task ReplaceBeneficiairesCoreAsync(
        long idDemande,
        IReadOnlyList<DemandePaiementBeneficiaire> beneficiaires,
        CancellationToken cancellationToken)
    {
        var existing = await _context.DemandePaiementBeneficiaires
            .Where(b => b.FK_DemandePaiement == idDemande)
            .ToListAsync(cancellationToken);

        _context.DemandePaiementBeneficiaires.RemoveRange(existing);

        foreach (var beneficiaire in beneficiaires)
        {
            beneficiaire.FK_DemandePaiement = idDemande;
            _context.DemandePaiementBeneficiaires.Add(beneficiaire);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CasDossierPieceObligatoire>> GetPiecesActivesAsync(
        long idCasDossier,
        CancellationToken cancellationToken = default)
        => await _context.CasDossierPiecesObligatoires.AsNoTracking()
            .Where(p => p.FK_CasDossier == idCasDossier && p.Actif)
            .OrderBy(p => p.Ordre)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CasDossierPieceObligatoire>> GetPiecesObligatoiresAsync(
        long idCasDossier,
        CancellationToken cancellationToken = default)
        => await _context.CasDossierPiecesObligatoires.AsNoTracking()
            .Where(p => p.FK_CasDossier == idCasDossier && p.Actif && p.Obligatoire)
            .OrderBy(p => p.Ordre)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<JournalAudit>> GetHistoriqueAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => await _context.JournalAudits.AsNoTracking()
            .Include(j => j.Utilisateur)
            .Where(j => j.Entite == "DEMANDE_PAIEMENT" && j.IdEntite == idDemande)
            .OrderByDescending(j => j.DateHeure)
            .ToListAsync(cancellationToken);

    public Task<bool> UtilisateurPeutAccederUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
        => _perimetre.UtilisateurPeutAccederUbAsync(idUtilisateur, idUB, cancellationToken);

    public void AddAuditEntry(
        long idUtilisateur,
        string operation,
        long idDemande,
        object? anciennes,
        object? nouvelles)
    {
        _context.JournalAudits.Add(new JournalAudit
        {
            FK_Utilisateur = idUtilisateur,
            DateHeure = DateTime.Now,
            Operation = operation,
            Entite = "DEMANDE_PAIEMENT",
            IdEntite = idDemande,
            AnciennesValeurs = anciennes is null ? null : JsonSerializer.Serialize(anciennes),
            NouvellesValeurs = nouvelles is null ? null : JsonSerializer.Serialize(nouvelles),
        });
    }

    public Task AddAuditAsync(
        long idUtilisateur,
        string operation,
        long idDemande,
        object? anciennes,
        object? nouvelles,
        CancellationToken cancellationToken = default)
        => TrackRepoAsync("AddAuditAsync", async () =>
        {
            AddAuditEntry(idUtilisateur, operation, idDemande, anciennes, nouvelles);
            await _context.SaveChangesAsync(cancellationToken);
        });

    public async Task AddValidationsEntiteAsync(
        IReadOnlyList<DemandePaiementValidation> validations,
        CancellationToken cancellationToken = default)
    {
        _context.DemandePaiementValidations.AddRange(validations);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<DemandePaiementImputation> EngageQuery(long? excludeDemandeId)
    {
        var q = _context.DemandePaiementImputations.AsNoTracking()
            .Where(i => i.DemandePaiement.Statut == StatutDemandePaiement.ViseeBudgetairement);

        if (excludeDemandeId is long id)
            q = q.Where(i => i.FK_DemandePaiement != id);

        return q;
    }

    /// <summary>
    /// Projection liste / accès léger — uniquement les navigations utilisées par MapList.
    /// Ne pas Include Beneficiaires / taux / version : explosion cartésienne + RESOURCE_SEMAPHORE.
    /// </summary>
    private IQueryable<DemandePaiement> BaseQuery()
        => _context.DemandesPaiement.AsNoTracking()
            .Include(d => d.CasDossier)
            .Include(d => d.ExerciceBudgetaire)
            .Include(d => d.UniteBudgetaire).ThenInclude(u => u.Departement)
            .Include(d => d.Demandeur)
            .Include(d => d.TypeBudget)
            .Include(d => d.UtilisateurAssigne);

    private IQueryable<DemandePaiement> DetailQuery()
        => _context.DemandesPaiement.AsNoTracking()
            .Include(d => d.CasDossier)
            .Include(d => d.ExerciceBudgetaire)
            .Include(d => d.VersionBudgetaire)
            .Include(d => d.UniteBudgetaire).ThenInclude(u => u.Departement)
            .Include(d => d.Demandeur)
            .Include(d => d.TypeBudget)
            .Include(d => d.UtilisateurAssigne)
            .Include(d => d.TauxChange)
            .Include(d => d.TauxChangePaiement)
            .Include(d => d.Beneficiaires.OrderBy(b => b.Ordre))
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.TypeBudget)
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.Snapshots)
            .Include(d => d.PiecesJointes.OrderBy(p => p.DateUpload))
            .Include(d => d.ValidationsEntite.OrderBy(v => v.Ordre))
                .ThenInclude(v => v.UtilisateurValidateur)
            .Include(d => d.ValidationsEntite.OrderBy(v => v.Ordre))
                .ThenInclude(v => v.UtilisateurDeclarant)
            .Include(d => d.BilletConversion)
                .ThenInclude(b => b!.UtilisateurEtabli)
            .Include(d => d.BilletConversion)
                .ThenInclude(b => b!.UtilisateurApprouve)
            .Include(d => d.BilletConversion)
                .ThenInclude(b => b!.UtilisateurVisa)
            .Include(d => d.PieceCaisse)
                .ThenInclude(p => p!.UtilisateurEtabli)
            .Include(d => d.BonProvisoire)
                .ThenInclude(b => b!.UtilisateurEtabli)
            .Include(d => d.MinuteCheque)
                .ThenInclude(m => m!.UtilisateurEtabli)
            .AsSplitQuery();

    public Task<BilletConversion?> GetBilletConversionByDemandeAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _context.BilletsConversion
            .Include(b => b.UtilisateurEtabli)
            .Include(b => b.UtilisateurApprouve)
            .Include(b => b.UtilisateurVisa)
            .FirstOrDefaultAsync(b => b.FK_DemandePaiement == idDemande, cancellationToken);

    public async Task AddBilletConversionAsync(
        BilletConversion billet,
        CancellationToken cancellationToken = default)
    {
        _context.BilletsConversion.Add(billet);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<PieceCaisse?> GetPieceCaisseByDemandeAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _context.PiecesCaisse
            .Include(p => p.UtilisateurEtabli)
            .FirstOrDefaultAsync(p => p.FK_DemandePaiement == idDemande, cancellationToken);

    public async Task AddPieceCaisseAsync(PieceCaisse piece, CancellationToken cancellationToken = default)
    {
        _context.PiecesCaisse.Add(piece);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<BonProvisoire?> GetBonProvisoireByDemandeAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _context.BonsProvisoire
            .Include(b => b.UtilisateurEtabli)
            .FirstOrDefaultAsync(b => b.FK_DemandePaiement == idDemande, cancellationToken);

    public async Task AddBonProvisoireAsync(BonProvisoire bon, CancellationToken cancellationToken = default)
    {
        _context.BonsProvisoire.Add(bon);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<MinuteCheque?> GetMinuteChequeByDemandeAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _context.MinutesCheque
            .Include(m => m.UtilisateurEtabli)
            .FirstOrDefaultAsync(m => m.FK_DemandePaiement == idDemande, cancellationToken);

    public async Task AddMinuteChequeAsync(MinuteCheque minute, CancellationToken cancellationToken = default)
    {
        _context.MinutesCheque.Add(minute);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<ParametreInstrumentPaiement?> GetParametreInstrumentAsync(
        string typeInstrument,
        CancellationToken cancellationToken = default)
    {
        var type = typeInstrument.Trim().ToUpperInvariant();
        return _context.ParametresInstrumentPaiement.AsNoTracking()
            .Where(p => p.Actif && p.TypeInstrument == type)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ParametreInstrumentPaiement?> GetParametreInstrumentByTypeAsync(
        string typeInstrument,
        CancellationToken cancellationToken = default)
    {
        var type = typeInstrument.Trim().ToUpperInvariant();
        return _context.ParametresInstrumentPaiement.AsNoTracking()
            .Where(p => p.TypeInstrument == type)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertParametreInstrumentAsync(
        ParametreInstrumentPaiement parametre,
        CancellationToken cancellationToken = default)
    {
        var type = parametre.TypeInstrument.Trim().ToUpperInvariant();
        var existing = await _context.ParametresInstrumentPaiement
            .FirstOrDefaultAsync(p => p.TypeInstrument == type, cancellationToken);

        if (existing is null)
        {
            parametre.TypeInstrument = type;
            parametre.DateCreation = DateTime.UtcNow;
            _context.ParametresInstrumentPaiement.Add(parametre);
        }
        else
        {
            existing.Sr = parametre.Sr;
            existing.ComptabiliteGenerale = parametre.ComptabiliteGenerale;
            existing.Cp = parametre.Cp;
            existing.Cpa = parametre.Cpa;
            existing.CompteGeneral = parametre.CompteGeneral;
            existing.CompteParticulier = parametre.CompteParticulier;
            existing.CpCa = parametre.CpCa;
            existing.Ls = parametre.Ls;
            existing.SuiviExtraComptable = parametre.SuiviExtraComptable;
            existing.MontantSuiviExtraComptable = parametre.MontantSuiviExtraComptable;
            existing.NumeroAppariement = parametre.NumeroAppariement;
            existing.RecuInstitutionnel = parametre.RecuInstitutionnel;
            existing.Actif = parametre.Actif;
            existing.DateModification = DateTime.UtcNow;
            existing.FK_UtilisateurModification = parametre.FK_UtilisateurModification;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<string> GenererNumeroPieceCaisseAsync(
        short annee,
        CancellationToken cancellationToken = default)
        => GenererNumeroDocumentAsync("PC", annee, _context.PiecesCaisse.Select(p => p.NumeroPiece), cancellationToken);

    public Task<string> GenererNumeroBonProvisoireAsync(
        short annee,
        CancellationToken cancellationToken = default)
        => GenererNumeroDocumentAsync("BP", annee, _context.BonsProvisoire.Select(b => b.NumeroBon), cancellationToken);

    public Task<string> GenererNumeroMinuteChequeAsync(
        short annee,
        CancellationToken cancellationToken = default)
        => GenererNumeroDocumentAsync("OP", annee, _context.MinutesCheque.Select(m => m.NumeroOp), cancellationToken);

    private async Task<string> GenererNumeroDocumentAsync(
        string prefixCode,
        short annee,
        IQueryable<string> numeros,
        CancellationToken cancellationToken)
    {
        var prefix = $"{prefixCode}-{annee}-";
        var references = await numeros.AsNoTracking()
            .Where(n => n.StartsWith(prefix))
            .ToListAsync(cancellationToken);

        var maxNum = 0;
        foreach (var reference in references)
        {
            if (reference.Length > prefix.Length
                && int.TryParse(reference.AsSpan(prefix.Length), out var num)
                && num > maxNum)
            {
                maxNum = num;
            }
        }

        return $"{prefix}{maxNum + 1:D5}";
    }
}
