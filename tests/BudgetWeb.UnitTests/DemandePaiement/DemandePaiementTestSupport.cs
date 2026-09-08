using System.Text;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.DTOs.Referentiels;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Interfaces.Referentiels;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Referentiels;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Fixe la date de traitement DPM pour les tests (jour courant par défaut en prod).</summary>
internal sealed class DateTraitementDpmScope : IDisposable
{
    private readonly Func<DateOnly>? _previous;

    public DateTraitementDpmScope(DateOnly date)
    {
        _previous = DemandePaiementService.ResolveDateTraitementDpmForTests;
        DemandePaiementService.ResolveDateTraitementDpmForTests = () => date;
    }

    public void Dispose() => DemandePaiementService.ResolveDateTraitementDpmForTests = _previous;
}

internal sealed class FakeUser : ICurrentUserService
{
    public long? UserId { get; set; } = 1;
    public string? Username => "test.user";
    public string? DisplayName => "Test User";
    public IReadOnlyList<string> Roles { get; set; } = [AppRoles.UserAdminFull];
    public IReadOnlyList<string> Permissions { get; set; } = AppPermissions.AdminFull;
    public bool IsAuthenticated => true;

    public bool IsInRole(string role) =>
        Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

    public bool HasPermission(string permission) =>
        Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));

    public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException();
}

internal sealed class FakePerimetreReader : IPerimetreUtilisateurReader
{
    private readonly FakeDemandePaiementRepo _repo;

    public FakePerimetreReader(FakeDemandePaiementRepo repo)
    {
        _repo = repo;
    }

    public Task<PerimetreUtilisateurSnapshot?> GetAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_repo.Perimetre);

    public Task<long?> GetDepartementUbAsync(
        long idUB,
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            _repo.UbDepartements.TryGetValue(idUB, out var d) ? (long?)d : null);

    public Task<bool> UtilisateurPeutAccederUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
        => _repo.UtilisateurPeutAccederUbAsync(idUtilisateur, idUB, cancellationToken);

    public Task<IReadOnlyList<long>> ResoudreIdsUbPerimetreAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        var snapshot = _repo.Perimetre;
        if (snapshot is null || !PerimetreAccess.EstConfigure(snapshot))
            return Task.FromResult<IReadOnlyList<long>>([]);

        if (snapshot.ToutesUnitesBudgetaires || snapshot.TousDepartements)
        {
            return Task.FromResult<IReadOnlyList<long>>(_repo.UbDepartements.Keys.ToList());
        }

        if (snapshot.IdUnitesBudgetaires.Count > 0)
        {
            var result = new List<long>();
            foreach (var idUb in snapshot.IdUnitesBudgetaires)
            {
                if (!_repo.UbDepartements.TryGetValue(idUb, out var idDept))
                    continue;
                if (PerimetreAccess.PeutAccederUb(snapshot, idUb, idDept))
                    result.Add(idUb);
            }

            return Task.FromResult<IReadOnlyList<long>>(result.OrderBy(x => x).ToList());
        }

        var ids = new HashSet<long>();
        foreach (var (idUb, idDept) in _repo.UbDepartements)
        {
            if (snapshot.IdDepartements.Contains(idDept))
                ids.Add(idUb);
        }

        return Task.FromResult<IReadOnlyList<long>>(ids.OrderBy(x => x).ToList());
    }

    public Task<bool> UtilisateurACreePrevisionSurUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_repo.UbProxyPrevision.Contains(idUB));

    public Task<IReadOnlyList<long>> ResoudreIdsUbProxyPrevisionAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<long>>(_repo.UbProxyPrevision.OrderBy(x => x).ToList());
}

internal sealed class FakeTauxChangeService : ITauxChangeService
{
    public const decimal TauxUsdCdf = 2_290m;
    public const decimal TauxEurCdf = 2_800m;
    private static readonly TauxChangeConventions.PaireCanonique PaireUsdCdf =
        new(TauxChangeConventions.DeviseUsd, TauxChangeConventions.DeviseCdf);
    private static readonly TauxChangeConventions.PaireCanonique PaireEurCdf =
        new("EUR", TauxChangeConventions.DeviseCdf);
    private static readonly TauxChangeConventions.PaireCanonique PaireEurUsd =
        new("EUR", TauxChangeConventions.DeviseUsd);

    public IReadOnlyList<PaireTauxChangeDto> ListerPairesSupportees()
        =>
        [
            new PaireTauxChangeDto("USD", "CDF", "USD/CDF"),
            new PaireTauxChangeDto("EUR", "CDF", "EUR/CDF"),
            new PaireTauxChangeDto("EUR", "USD", "EUR/USD"),
        ];

    public Task<IReadOnlyList<PaireTauxChangeDto>> ListerPairesSupporteesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(ListerPairesSupportees());

    public Task<IReadOnlyList<TauxChangeDto>> ListAsync(
        TauxChangeListQuery? query = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TauxChangeDto>>([]);

    public Task<TauxChangeDto?> GetByIdAsync(long idTauxChange, CancellationToken cancellationToken = default)
        => Task.FromResult<TauxChangeDto?>(null);

    public Task<TauxChangeApplicableDto?> GetApplicableVersUsdAsync(
        string deviseSource,
        DateOnly dateReference,
        CancellationToken cancellationToken = default)
        => GetApplicableAsync(deviseSource, "USD", dateReference, cancellationToken);

    private static readonly IReadOnlyList<TauxChangeConventions.PaireCanonique> PairesSupportees =
        [PaireUsdCdf, PaireEurCdf, PaireEurUsd];

    private static decimal TauxReferenceCanonique(TauxChangeConventions.PaireCanonique paire)
        => paire.DeviseBase switch
        {
            "EUR" when paire.DeviseQuote == TauxChangeConventions.DeviseCdf => TauxEurCdf,
            "EUR" when paire.DeviseQuote == TauxChangeConventions.DeviseUsd => 1.08m,
            _ => TauxUsdCdf,
        };

    public Task<TauxChangeApplicableDto?> GetApplicableAsync(
        string deviseSource,
        string deviseCible,
        DateOnly dateReference,
        CancellationToken cancellationToken = default)
    {
        var source = deviseSource.Trim().ToUpperInvariant();
        var cible = deviseCible.Trim().ToUpperInvariant();
        if (source == cible)
        {
            return Task.FromResult<TauxChangeApplicableDto?>(new TauxChangeApplicableDto(
                null, source, cible, 1m, 1m, dateReference, StatutTauxChange.Actif, EstIdentite: true, EstInverseCalcule: false));
        }

        if (!TauxChangeConventions.TryResoudrePaire(PairesSupportees, source, cible, out var paire))
            return Task.FromResult<TauxChangeApplicableDto?>(null);

        var tauxRef = TauxReferenceCanonique(paire);
        var tauxDir = TauxChangeConventions.CalculerTauxDirectionnel(tauxRef, source, cible, paire);
        var inverse = source == paire.DeviseQuote && cible == paire.DeviseBase;
        return Task.FromResult<TauxChangeApplicableDto?>(new TauxChangeApplicableDto(
            1, source, cible, tauxDir, tauxRef, dateReference, StatutTauxChange.Actif, EstIdentite: false, EstInverseCalcule: inverse));
    }

    public ConversionUsdResultDto ConvertirVersUsd(
        decimal montantBrut,
        string deviseSource,
        decimal tauxReference,
        decimal tauxDirectionnel,
        long? idTauxChange,
        bool estIdentite,
        TauxChangeConventions.PaireCanonique paire)
        => new(
            montantBrut,
            deviseSource.Trim().ToUpperInvariant(),
            tauxReference,
            tauxReference,
            estIdentite
                ? montantBrut
                : TauxChangeConventions.ConvertirVersUsd(montantBrut, deviseSource, tauxReference, paire),
            idTauxChange,
            estIdentite);

    public async Task<ConversionUsdResultDto> ConvertirVersUsdAsync(
        decimal montantBrut,
        string deviseSource,
        DateOnly dateReference,
        CancellationToken cancellationToken = default)
    {
        var applicable = await GetApplicableVersUsdAsync(deviseSource, dateReference, cancellationToken)
            ?? throw new InvalidOperationException("Taux introuvable.");
        if (applicable.EstIdentite)
        {
            return ConvertirVersUsd(
                montantBrut,
                applicable.DeviseSource,
                1m,
                1m,
                null,
                estIdentite: true,
                PaireUsdCdf);
        }

        var paire = TauxChangeConventions.ResoudrePaire(
            PairesSupportees,
            applicable.DeviseSource,
            applicable.DeviseCible);
        return ConvertirVersUsd(
            montantBrut,
            applicable.DeviseSource,
            applicable.TauxReference,
            applicable.Taux,
            applicable.IdTauxChange,
            applicable.EstIdentite,
            paire);
    }

    public ConversionResultDto Convertir(
        decimal montantSource,
        string deviseSource,
        string deviseCible,
        decimal tauxReference,
        decimal tauxDirectionnel,
        long? idTauxChange,
        bool estIdentite,
        TauxChangeConventions.PaireCanonique paire)
    {
        var source = deviseSource.Trim().ToUpperInvariant();
        var cible = deviseCible.Trim().ToUpperInvariant();
        var montantCible = estIdentite
            ? montantSource
            : decimal.Round(
                TauxChangeConventions.Convertir(montantSource, source, cible, tauxReference, paire),
                4,
                MidpointRounding.AwayFromZero);

        return new ConversionResultDto(
            montantSource,
            source,
            montantCible,
            cible,
            tauxDirectionnel,
            tauxReference,
            idTauxChange,
            estIdentite);
    }

    public async Task<ConversionResultDto> ConvertirAsync(
        decimal montantSource,
        string deviseSource,
        string deviseCible,
        DateOnly dateReference,
        CancellationToken cancellationToken = default)
    {
        var source = deviseSource.Trim().ToUpperInvariant();
        var cible = deviseCible.Trim().ToUpperInvariant();
        if (source == cible)
            return Convertir(montantSource, source, cible, 1m, 1m, null, estIdentite: true, PaireUsdCdf);

        var applicable = await GetApplicableAsync(source, cible, dateReference, cancellationToken)
            ?? throw new InvalidOperationException("Taux introuvable.");

        var paire = TauxChangeConventions.ResoudrePaire(PairesSupportees, source, cible);
        return Convertir(
            montantSource,
            source,
            cible,
            applicable.TauxReference,
            applicable.Taux,
            applicable.IdTauxChange,
            applicable.EstIdentite,
            paire);
    }

    public Task<TauxChangeDto> CreateVersionAsync(
        CreateTauxChangeRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<TauxChangeDto> InactivateAsync(
        long idTauxChange,
        InactivateTauxChangeRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<TauxChangeDto> UpdateVersionAsync(
        long idTauxChange,
        UpdateTauxChangeRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class FakeDemandePaiementDocumentRenderer : IDemandePaiementDocumentRenderer
{
    public byte[] Render(DemandePaiementDocumentDto payload) => [0x25, 0x50, 0x44, 0x46];
}

internal sealed class FakeBilletConversionDocumentRenderer : IBilletConversionDocumentRenderer
{
    public byte[] Render(BilletConversionDocumentDto payload) => [0x25, 0x50, 0x44, 0x46];
}

internal sealed class FakePieceCaisseDocumentRenderer : IPieceCaisseDocumentRenderer
{
    public byte[] Render(PieceCaisseDocumentDto payload) => [0x25, 0x50, 0x44, 0x46];
}

internal sealed class CapturingPieceCaisseDocumentRenderer : IPieceCaisseDocumentRenderer
{
    public PieceCaisseDocumentDto? LastPayload { get; private set; }

    public byte[] Render(PieceCaisseDocumentDto payload)
    {
        LastPayload = payload;
        return [0x25, 0x50, 0x44, 0x46];
    }
}

internal sealed class FakeBonProvisoireDocumentRenderer : IBonProvisoireDocumentRenderer
{
    public byte[] Render(BonProvisoireDocumentDto payload) => [0x25, 0x50, 0x44, 0x46];
}

internal sealed class FakeMinuteChequeDocumentRenderer : IMinuteChequeDocumentRenderer
{
    public byte[] Render(MinuteChequeDocumentDto payload) => [0x25, 0x50, 0x44, 0x46];
}

internal sealed class FakeFicheImputationBudgetaireRenderer : IFicheImputationBudgetaireRenderer
{
    public byte[] Render(FicheImputationBudgetaireDto payload) => [0x25, 0x50, 0x44, 0x46];
}

internal sealed class FakeDemandePaiementRepo : IDemandePaiementRepository
{
    private long _nextDemandeId = 1;
    private long _nextImputationId = 1;
    private long _nextPieceId = 1;
    private long _nextAuditId = 1;
    private long _nextBilletId = 1;
    private long _nextPieceCaisseId = 1;
    private long _nextBonProvisoireId = 1;
    private long _nextMinuteChequeId = 1;
    private long _nextRoutageId = 1;
    private int _refSeq;

    public List<DemandePaiementEntity> Demandes { get; } = [];
    public List<DemandePaiementRoutage> Routages { get; } = [];
    public List<BilletConversion> Billets { get; } = [];
    public List<PieceCaisse> PiecesCaisse { get; } = [];
    public List<BonProvisoire> BonsProvisoire { get; } = [];
    public List<MinuteCheque> MinutesCheque { get; } = [];
    public List<ParametreInstrumentPaiement> ParametresInstrument { get; } = [];
    public Dictionary<long, List<DemandePaiementBeneficiaire>> BeneficiairesByDemande { get; } = new();
    public List<CasDossierPieceObligatoire> PiecesObligatoires { get; set; } = [];
    public Dictionary<long, TypeBudget> TypesBudget { get; set; } = new()
    {
        [1] = new TypeBudget { IdTypeBudget = 1, CodeType = TypeBudgetCode.DepensesCourantes, Libelle = "DC" },
        [2] = new TypeBudget { IdTypeBudget = 2, CodeType = TypeBudgetCode.ActionsExploitation, Libelle = "AE" },
        [3] = new TypeBudget { IdTypeBudget = 3, CodeType = TypeBudgetCode.BudgetInvestissement, Libelle = "BI" },
    };
    public Dictionary<long, PrevisionBudgetaire> Previsions { get; set; } = new();
    public List<RubriqueBudgetaire> RubriquesDc { get; set; } = [];
    public Dictionary<long, Utilisateur> UtilisateursAssignes { get; } = new();
    public long? IdVersionValidee { get; set; } = 4;
    public string WorkflowStatutUb { get; set; } = StatutVersionBudgetaire.Validee;
    public List<JournalAudit> Audits { get; } = [];
    public bool FailSaveChanges { get; set; }

    public int GetDetailAsyncCallCount { get; private set; }
    public int GetMutationHeaderAsyncCallCount { get; private set; }
    public int GetValidationsEntiteMinimalAsyncCallCount { get; private set; }
    public int GetValidationsEntiteN2AsyncCallCount { get; private set; }
    public int GetValidationsEntiteSoumettreAsyncCallCount { get; private set; }
    public int GetEmpreinteReadAsyncCallCount { get; private set; }
    public int EnrichDemandeMapDetailShellAsyncCallCount { get; private set; }
    public int EnrichStatutsInstrumentsMapDetailAsyncCallCount { get; private set; }
    public int GetDetailDtoApresMutationAsyncCallCount { get; private set; }
    public int GetDetailDtoApresEnvoyerAsyncCallCount { get; private set; }
    public int GetDemandeAccesContextAsyncCallCount { get; private set; }
    public int GetPieceCaissePdfDataAsyncCallCount { get; private set; }
    public int GetBonProvisoirePdfDataAsyncCallCount { get; private set; }
    public int GetMinuteChequePdfDataAsyncCallCount { get; private set; }
    public int GetBilletConversionPdfDataAsyncCallCount { get; private set; }
    public int GetDemandePaiementPdfDataAsyncCallCount { get; private set; }

    public void ResetPdfPipelineCounters()
    {
        GetDetailAsyncCallCount = 0;
        GetMutationHeaderAsyncCallCount = 0;
        GetValidationsEntiteMinimalAsyncCallCount = 0;
        GetValidationsEntiteN2AsyncCallCount = 0;
        GetValidationsEntiteSoumettreAsyncCallCount = 0;
        GetEmpreinteReadAsyncCallCount = 0;
        EnrichDemandeMapDetailShellAsyncCallCount = 0;
        EnrichStatutsInstrumentsMapDetailAsyncCallCount = 0;
        GetDetailDtoApresMutationAsyncCallCount = 0;
        GetDetailDtoApresEnvoyerAsyncCallCount = 0;
        GetDemandeAccesContextAsyncCallCount = 0;
        GetPieceCaissePdfDataAsyncCallCount = 0;
        GetBonProvisoirePdfDataAsyncCallCount = 0;
        GetMinuteChequePdfDataAsyncCallCount = 0;
        GetBilletConversionPdfDataAsyncCallCount = 0;
        GetDemandePaiementPdfDataAsyncCallCount = 0;
    }

    /// <summary>Hook test : exécuté au début de <see cref="GetTrackedAsync"/>.</summary>
    public Action<long>? OnBeforeGetTracked { get; set; }

    /// <summary>Si true, <see cref="GetDetailAsync"/> lève une exception (tests Vague 2b).</summary>
    public bool ForbidGetDetailAsync { get; set; }

    /// <summary>
    /// IDs pour lesquels <see cref="GetDetailAsync"/> lève une exception technique générique
    /// (simule panne infra / SQL pour le mapping INTERNAL_ERROR du batch).
    /// </summary>
    public HashSet<long> ForceInternalErrorOnGetDetailIds { get; } = [];

    /// <summary>Si true, <see cref="GetDetailDtoApresMutationAsync"/> lève une exception (tests Vague 2b-2).</summary>
    public bool ForbidGetDetailDtoApresMutationAsync { get; set; }

    /// <summary>
    /// Simule un stub CasDossier (Ordre=0) laissé dans le ChangeTracker après un item réussi
    /// (GetDetailDtoApresEnvoyer) — provoque un échec au prochain SaveChanges si non clearé.
    /// </summary>
    public bool SimulateCasDossierChangeTrackerContamination { get; set; }

    /// <summary>État simulé du ChangeTracker (pollué tant que <see cref="ClearChangeTracker"/> n'a pas été appelé).</summary>
    public bool IsChangeTrackerPolluted { get; private set; }

    /// <summary>Nombre d'appels à <see cref="ClearChangeTracker"/>.</summary>
    public int ClearChangeTrackerCallCount { get; private set; }

    /// <summary>
    /// IDs pour lesquels <see cref="GetTrackedAsync"/> marque le ChangeTracker comme pollué
    /// (simule contamination même en cas d'échec avant le shell post-mutation).
    /// </summary>
    public HashSet<long> PolluteChangeTrackerOnGetTrackedIds { get; } = [];

    public DemandePaiementEntity? GetDemandeEntityForTest(long idDemande) => FindDemande(idDemande);

    /// <summary>Paramétrage minimal pour les tests d'établissement (valeurs non métier SNEL).</summary>
    public void SeedParametreInstrument(string typeInstrument)
    {
        var type = TypeInstrumentPaiement.Normaliser(typeInstrument);
        if (ParametresInstrument.Any(p => p.Actif && p.TypeInstrument == type))
            return;

        ParametreInstrumentPaiement parametre = type switch
        {
            TypeInstrumentPaiement.PieceCaisse => new ParametreInstrumentPaiement
            {
                TypeInstrument = type,
                Actif = true,
                Sr = "TEST-SR",
                ComptabiliteGenerale = "TEST-CG",
                Cp = "TEST-CP",
                Cpa = "TEST-CPA",
                NumeroAppariement = "TEST-APP",
                RecuInstitutionnel = "TEST-RECU",
            },
            TypeInstrumentPaiement.BonProvisoire => new ParametreInstrumentPaiement
            {
                TypeInstrument = type,
                Actif = true,
                CompteGeneral = "TEST-CG",
                CompteParticulier = "TEST-CP",
                NumeroAppariement = "TEST-APP",
                RecuInstitutionnel = "TEST-RECU",
            },
            TypeInstrumentPaiement.MinuteCheque => new ParametreInstrumentPaiement
            {
                TypeInstrument = type,
                Actif = true,
                CompteGeneral = "TEST-CG",
                CpCa = "TEST-CPCA",
                Ls = "TEST-LS",
                SuiviExtraComptable = "TEST-SEC",
                NumeroAppariement = "TEST-APP",
            },
            _ => throw new InvalidOperationException($"Instrument inconnu : {type}"),
        };

        ParametresInstrument.Add(parametre);
    }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var snapshot = CaptureSnapshot();
        return RunAsync();

        async Task<T> RunAsync()
        {
            try
            {
                return await action(cancellationToken);
            }
            catch
            {
                RestoreSnapshot(snapshot);
                throw;
            }
        }
    }

    public int ListScopeRowsCallCount { get; private set; }

    public Task<IReadOnlyList<DemandePaiementEntity>> ListAsync(
        DemandePaiementQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = FilterDemandes(query).ToList();
        foreach (var d in rows)
            AttachNavigations(d);
        return Task.FromResult<IReadOnlyList<DemandePaiementEntity>>(rows);
    }

    public Task<IReadOnlyList<DemandePaiementScopeRow>> ListScopeRowsAsync(
        DemandePaiementQuery query,
        CancellationToken cancellationToken = default)
    {
        ListScopeRowsCallCount++;
        var rows = FilterDemandes(query with { Statut = null });
        var result = rows.Select(d =>
        {
            AttachNavigations(d);
            return new DemandePaiementScopeRow(
                d.IdDemandePaiement,
                d.Statut,
                d.FK_UniteBudgetaire,
                d.FK_UtilisateurCreation,
                d.FK_TypeBudget,
                d.TypeBudget?.CodeType,
                d.FK_UtilisateurAssigne,
                d.FK_UtilisateurRetour);
        }).ToList();
        return Task.FromResult<IReadOnlyList<DemandePaiementScopeRow>>(result);
    }

    private IEnumerable<DemandePaiementEntity> FilterDemandes(DemandePaiementQuery query)
    {
        IEnumerable<DemandePaiementEntity> rows = Demandes;
        if (query.Statut is not null)
        {
            var st = StatutDemandePaiement.Normaliser(query.Statut);
            rows = rows.Where(d => StatutDemandePaiement.Normaliser(d.Statut) == st);
        }
        if (query.IdExercice is long ex)
            rows = rows.Where(d => d.FK_ExerciceBudgetaire == ex);
        if (query.IdUB is long ub)
            rows = rows.Where(d => d.FK_UniteBudgetaire == ub);
        if (query.IdDepartement is long idDept)
            rows = rows.Where(d =>
                (d.UniteBudgetaire?.FK_Departement ?? UbDepartements.GetValueOrDefault(d.FK_UniteBudgetaire)) == idDept);
        if (query.IdCasDossier is long idCas)
            rows = rows.Where(d => d.FK_CasDossier == idCas);
        if (query.IdTypeBudget is long idType)
            rows = rows.Where(d => d.FK_TypeBudget == idType);
        if (query.IdDemandeur is long idDem)
            rows = rows.Where(d => d.FK_Demandeur == idDem);
        if (!string.IsNullOrWhiteSpace(query.Reference))
        {
            var reference = query.Reference.Trim();
            rows = rows.Where(d => d.Reference.Contains(reference, StringComparison.OrdinalIgnoreCase));
        }
        if (query.DateDebut is DateOnly debut)
            rows = rows.Where(d => d.DateEmission >= debut);
        if (query.DateFin is DateOnly fin)
            rows = rows.Where(d => d.DateEmission <= fin);
        return rows;
    }

    public Task<DemandePaiementEntity?> GetByIdAsync(long idDemande, CancellationToken cancellationToken = default)
    {
        var row = FindDemande(idDemande);
        if (row is not null)
            AttachNavigations(row);
        return Task.FromResult(row);
    }

    public Task<DemandePaiementEntity?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
        => Task.FromResult(Demandes.FirstOrDefault(d =>
            string.Equals(d.Reference, reference, StringComparison.OrdinalIgnoreCase)));

    public Task<DemandePaiementEntity?> GetTrackedAsync(long idDemande, CancellationToken cancellationToken = default)
    {
        OnBeforeGetTracked?.Invoke(idDemande);
        if (PolluteChangeTrackerOnGetTrackedIds.Contains(idDemande))
            IsChangeTrackerPolluted = true;

        var row = FindDemande(idDemande);
        if (row is null)
            return Task.FromResult<DemandePaiementEntity?>(null);

        row.BilletConversion = Billets.FirstOrDefault(b => b.FK_DemandePaiement == idDemande);
        row.PieceCaisse = PiecesCaisse.FirstOrDefault(p => p.FK_DemandePaiement == idDemande);
        row.BonProvisoire = BonsProvisoire.FirstOrDefault(b => b.FK_DemandePaiement == idDemande);
        row.MinuteCheque = MinutesCheque.FirstOrDefault(m => m.FK_DemandePaiement == idDemande);
        return Task.FromResult<DemandePaiementEntity?>(row);
    }

    public Task<DemandePaiementEntity?> GetForPieceUploadAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => Task.FromResult(FindDemande(idDemande));

    public Task<bool> HasValidatedEntiteValidationsAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        var demande = FindDemande(idDemande);
        if (demande is null)
            return Task.FromResult(false);

        return Task.FromResult(demande.ValidationsEntite.Any(v =>
            string.Equals(v.Statut, StatutValidationEntite.Validee, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AttachEmpreinteCollectionsForUploadAsync(
        DemandePaiementEntity demande,
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        if (BeneficiairesByDemande.TryGetValue(idDemande, out var benefs))
            demande.Beneficiaires = benefs;
        demande.Imputations = demande.Imputations.ToList();
        demande.PiecesJointes = demande.PiecesJointes.ToList();
        demande.ValidationsEntite = demande.ValidationsEntite.ToList();
        return Task.CompletedTask;
    }

    public Task<DemandePaiementEntity?> GetDetailAsync(long idDemande, CancellationToken cancellationToken = default)
    {
        GetDetailAsyncCallCount++;
        if (ForbidGetDetailAsync)
        {
            throw new InvalidOperationException(
                "GetDetailAsync ne doit pas être invoqué par ce workflow de mutation optimisé (Vague 2b).");
        }

        if (ForceInternalErrorOnGetDetailIds.Contains(idDemande))
        {
            throw new Exception(
                "SqlException: Invalid object name 'dbo.DemandePaiement'. Constraint FK_Demande_Ub failed.");
        }

        return Task.FromResult(CloneDemandeDetail(idDemande));
    }

    public Task<DemandePaiementEntity?> GetFicheImputationContextAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => GetDetailAsync(idDemande, cancellationToken);

    public Task<DemandePaiementEntity?> GetDetailDtoApresMutationAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetDetailDtoApresMutationAsyncCallCount++;
        if (ForbidGetDetailDtoApresMutationAsync)
        {
            throw new InvalidOperationException(
                "GetDetailDtoApresMutationAsync ne doit pas être invoqué par EnvoyerEnValidationAsync (Vague 2b-2).");
        }

        return Task.FromResult(CloneDemandeDetail(idDemande));
    }

    public Task<DemandePaiementEntity> GetDetailDtoApresEnvoyerAsync(
        DemandePaiementEntity tracked,
        CancellationToken cancellationToken = default)
    {
        GetDetailDtoApresEnvoyerAsyncCallCount++;
        AttachNavigations(tracked);
        if (BeneficiairesByDemande.TryGetValue(tracked.IdDemandePaiement, out var benefs))
            tracked.Beneficiaires = benefs;
        tracked.BilletConversion = Billets.FirstOrDefault(b => b.FK_DemandePaiement == tracked.IdDemandePaiement);
        tracked.PieceCaisse = PiecesCaisse.FirstOrDefault(p => p.FK_DemandePaiement == tracked.IdDemandePaiement);
        tracked.BonProvisoire = BonsProvisoire.FirstOrDefault(b => b.FK_DemandePaiement == tracked.IdDemandePaiement);
        tracked.MinuteCheque = MinutesCheque.FirstOrDefault(m => m.FK_DemandePaiement == tracked.IdDemandePaiement);

        // Miroir du bug réel : shell post-envoyer attache un stub CasDossier (Ordre=0) au tracker.
        if (SimulateCasDossierChangeTrackerContamination)
            IsChangeTrackerPolluted = true;

        return Task.FromResult(tracked);
    }

    private DemandePaiementEntity? CloneDemandeDetail(long idDemande)
    {
        var row = FindDemande(idDemande);
        if (row is null)
            return null;

        AttachNavigations(row);
        if (BeneficiairesByDemande.TryGetValue(idDemande, out var benefs))
            row.Beneficiaires = benefs;
        row.BilletConversion = Billets.FirstOrDefault(b => b.FK_DemandePaiement == idDemande);
        row.PieceCaisse = PiecesCaisse.FirstOrDefault(p => p.FK_DemandePaiement == idDemande);
        row.BonProvisoire = BonsProvisoire.FirstOrDefault(b => b.FK_DemandePaiement == idDemande);
        row.MinuteCheque = MinutesCheque.FirstOrDefault(m => m.FK_DemandePaiement == idDemande);

        return row;
    }

    public Task<DemandePaiementEntity?> GetDetailConsultationAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => GetDetailAsync(idDemande, cancellationToken);

    public Task<IReadOnlyList<DemandePaiementValidation>> GetValidationsEntiteMinimalAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetValidationsEntiteMinimalAsyncCallCount++;
        var row = FindDemande(idDemande);
        if (row is null)
            return Task.FromResult<IReadOnlyList<DemandePaiementValidation>>([]);

        AttachNavigations(row);
        var minimal = row.ValidationsEntite
            .OrderBy(v => v.Ordre)
            .Select(v => new DemandePaiementValidation
            {
                IdValidation = v.IdValidation,
                FK_DemandePaiement = v.FK_DemandePaiement,
                Niveau = v.Niveau,
                Ordre = v.Ordre,
                Statut = v.Statut,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<DemandePaiementValidation>>(minimal);
    }

    public Task<IReadOnlyList<DemandePaiementValidation>> GetValidationsEntiteN2Async(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetValidationsEntiteN2AsyncCallCount++;
        var row = FindDemande(idDemande);
        if (row is null)
            return Task.FromResult<IReadOnlyList<DemandePaiementValidation>>([]);

        AttachNavigations(row);
        var n2Rows = row.ValidationsEntite
            .OrderBy(v => v.Ordre)
            .Select(v => new DemandePaiementValidation
            {
                IdValidation = v.IdValidation,
                FK_DemandePaiement = v.FK_DemandePaiement,
                Niveau = v.Niveau,
                Ordre = v.Ordre,
                Statut = v.Statut,
                ModeValidation = v.ModeValidation,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<DemandePaiementValidation>>(n2Rows);
    }

    public Task<IReadOnlyList<DemandePaiementValidation>> GetValidationsEntiteSoumettreAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetValidationsEntiteSoumettreAsyncCallCount++;
        var row = FindDemande(idDemande);
        if (row is null)
            return Task.FromResult<IReadOnlyList<DemandePaiementValidation>>([]);

        AttachNavigations(row);
        var soumettreRows = row.ValidationsEntite
            .OrderBy(v => v.Ordre)
            .Select(v => new DemandePaiementValidation
            {
                IdValidation = v.IdValidation,
                FK_DemandePaiement = v.FK_DemandePaiement,
                Niveau = v.Niveau,
                Ordre = v.Ordre,
                Statut = v.Statut,
                ModeValidation = v.ModeValidation,
                EmpreinteDonnees = v.EmpreinteDonnees,
                FK_UtilisateurValidateur = v.FK_UtilisateurValidateur,
                FK_UtilisateurDeclarant = v.FK_UtilisateurDeclarant,
                NomSignatairePhysique = v.NomSignatairePhysique,
                FonctionSignatairePhysique = v.FonctionSignatairePhysique,
                DateSignaturePhysique = v.DateSignaturePhysique,
                DateValidation = v.DateValidation,
                Commentaire = v.Commentaire,
                UtilisateurValidateur = v.UtilisateurValidateur is null
                    ? null
                    : new Utilisateur
                    {
                        IdUtilisateur = v.UtilisateurValidateur.IdUtilisateur,
                        Nom = v.UtilisateurValidateur.Nom,
                    },
                UtilisateurDeclarant = v.UtilisateurDeclarant is null
                    ? null
                    : new Utilisateur
                    {
                        IdUtilisateur = v.UtilisateurDeclarant.IdUtilisateur,
                        Nom = v.UtilisateurDeclarant.Nom,
                    },
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<DemandePaiementValidation>>(soumettreRows);
    }

    public Task<DemandePaiementEntity?> GetEmpreinteReadAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetEmpreinteReadAsyncCallCount++;
        var row = FindDemande(idDemande);
        if (row is null)
            return Task.FromResult<DemandePaiementEntity?>(null);

        AttachNavigations(row);
        if (BeneficiairesByDemande.TryGetValue(idDemande, out var benefs))
            row.Beneficiaires = benefs;

        var empreinte = new DemandePaiementEntity
        {
            IdDemandePaiement = row.IdDemandePaiement,
            DateEmission = row.DateEmission,
            LieuEmission = row.LieuEmission,
            FK_Demandeur = row.FK_Demandeur,
            FK_CasDossier = row.FK_CasDossier,
            TypeBudgetSollicite = row.TypeBudgetSollicite,
            ItemSollicite = row.ItemSollicite,
            Objet = row.Objet,
            CompteSection = row.CompteSection,
            MontantBrut = row.MontantBrut,
            Devise = row.Devise,
            FK_Devise = row.FK_Devise,
            ModePaiementSollicite = row.ModePaiementSollicite,
            Beneficiaires = row.Beneficiaires
                .OrderBy(b => b.Ordre)
                .ThenBy(b => b.IdBeneficiaire)
                .Select(b => new DemandePaiementBeneficiaire
                {
                    IdBeneficiaire = b.IdBeneficiaire,
                    Ordre = b.Ordre,
                    TypeBeneficiaire = b.TypeBeneficiaire,
                    NomComplet = b.NomComplet,
                    Matricule = b.Matricule,
                    Fonction = b.Fonction,
                    RaisonSociale = b.RaisonSociale,
                    Rccm = b.Rccm,
                    Adresse = b.Adresse,
                    Banque = b.Banque,
                    NumeroCompte = b.NumeroCompte,
                    EstPrincipal = b.EstPrincipal,
                })
                .ToList(),
            PiecesJointes = row.PiecesJointes
                .Select(p => new PieceJointe
                {
                    IdPieceJointe = p.IdPieceJointe,
                    CodeTypePiece = p.CodeTypePiece,
                    FK_PieceObligatoire = p.FK_PieceObligatoire,
                    Libelle = p.Libelle,
                    EstObligatoire = p.EstObligatoire,
                    HashSha256 = p.HashSha256,
                    TailleOctets = p.TailleOctets,
                })
                .ToList(),
            Imputations = row.Imputations
                .OrderBy(i => i.Ordre)
                .ThenBy(i => i.IdImputation)
                .Select(i => new DemandePaiementImputation
                {
                    IdImputation = i.IdImputation,
                    Ordre = i.Ordre,
                    FK_TypeBudget = i.FK_TypeBudget,
                    FK_UniteBudgetaire = i.FK_UniteBudgetaire,
                    FK_ExerciceBudgetaire = i.FK_ExerciceBudgetaire,
                    FK_RubriqueBudgetaire = i.FK_RubriqueBudgetaire,
                    Mois = i.Mois,
                    LibelleItemAE = i.LibelleItemAE,
                    FK_GroupeItemAE = i.FK_GroupeItemAE,
                    FK_ItemBI = i.FK_ItemBI,
                    DetailBI = i.DetailBI,
                    FK_BudgetLigne = i.FK_BudgetLigne,
                    MontantBrut = i.MontantBrut,
                    Devise = i.Devise,
                    NumeroFicheSuivi = i.NumeroFicheSuivi,
                    TypeBudget = i.TypeBudget,
                })
                .ToList(),
        };

        return Task.FromResult<DemandePaiementEntity?>(empreinte);
    }

    public Task EnrichDemandeMapDetailShellAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken = default)
    {
        EnrichDemandeMapDetailShellAsyncCallCount++;
        AttachNavigations(demande);
        var source = FindDemande(demande.IdDemandePaiement);
        if (source is null)
            return Task.CompletedTask;

        AttachNavigations(source);
        demande.Reference = source.Reference;
        demande.FK_ExerciceBudgetaire = source.FK_ExerciceBudgetaire;
        demande.FK_VersionBudgetaire = source.FK_VersionBudgetaire;
        demande.FK_UniteBudgetaire = source.FK_UniteBudgetaire;
        demande.FK_TypeBudget = source.FK_TypeBudget;
        demande.TauxConversion = source.TauxConversion;
        demande.MontantUsd = source.MontantUsd;
        demande.FK_TauxChange = source.FK_TauxChange;
        demande.TypeInstrumentPaiement = source.TypeInstrumentPaiement;
        demande.DevisePaiement = source.DevisePaiement;
        demande.MontantPaiement = source.MontantPaiement;
        demande.TauxPaiement = source.TauxPaiement;
        demande.FK_TauxChangePaiement = source.FK_TauxChangePaiement;
        demande.MotifRetour = source.MotifRetour;
        demande.CommentaireRetour = source.CommentaireRetour;
        demande.FK_UtilisateurCreation = source.FK_UtilisateurCreation;
        demande.DateCreation = source.DateCreation;
        demande.DateSoumission = source.DateSoumission;
        demande.DateReception = source.DateReception;
        demande.DateControle = source.DateControle;
        demande.DateVisa = source.DateVisa;
        demande.DateRetour = source.DateRetour;
        demande.FK_UtilisateurAssigne = source.FK_UtilisateurAssigne;
        demande.UtilisateurAssigne = source.UtilisateurAssigne;
        demande.CasDossier = source.CasDossier;
        demande.ExerciceBudgetaire = source.ExerciceBudgetaire;
        demande.UniteBudgetaire = source.UniteBudgetaire;
        demande.Demandeur = source.Demandeur;
        demande.TypeBudget = source.TypeBudget;
        demande.VersionBudgetaire = source.VersionBudgetaire;
        foreach (var imputation in demande.Imputations)
        {
            var src = source.Imputations.FirstOrDefault(i => i.IdImputation == imputation.IdImputation);
            if (src?.TypeBudget is not null)
                imputation.TypeBudget = src.TypeBudget;
        }

        return Task.CompletedTask;
    }

    public Task EnrichStatutsInstrumentsMapDetailAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken = default)
    {
        EnrichStatutsInstrumentsMapDetailAsyncCallCount++;
        var source = FindDemande(demande.IdDemandePaiement);
        if (source is null)
            return Task.CompletedTask;

        demande.BilletConversion = source.BilletConversion;
        demande.PieceCaisse = source.PieceCaisse;
        demande.BonProvisoire = source.BonProvisoire;
        demande.MinuteCheque = source.MinuteCheque;
        return Task.CompletedTask;
    }

    public Task<DemandePaiementMutationHeaderReadModel?> GetMutationHeaderAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetMutationHeaderAsyncCallCount++;
        var row = FindDemande(idDemande);
        if (row is null)
            return Task.FromResult<DemandePaiementMutationHeaderReadModel?>(null);

        AttachNavigations(row);
        return Task.FromResult<DemandePaiementMutationHeaderReadModel?>(new DemandePaiementMutationHeaderReadModel(
            row.IdDemandePaiement,
            row.Statut,
            row.FK_UniteBudgetaire,
            row.FK_UtilisateurCreation,
            row.FK_UtilisateurAssigne,
            row.TypeBudgetSollicite,
            row.TypeBudget?.CodeType,
            row.FK_Demandeur,
            row.Objet,
            row.MontantBrut,
            row.FK_CasDossier,
            row.Devise,
            row.ItemSollicite,
            row.ModePaiementSollicite ?? string.Empty));
    }

    public Task<string> GenererReferenceAsync(short anneeExercice, CancellationToken cancellationToken = default)
    {
        _refSeq++;
        return Task.FromResult($"DP-{anneeExercice}-{_refSeq:D5}");
    }

    public Task<long?> ResolveVersionBudgetaireValideeAsync(
        long idExercice,
        long idUB,
        CancellationToken cancellationToken = default)
        => Task.FromResult(IdVersionValidee);

    public Task<string?> GetWorkflowStatutUbAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(WorkflowStatutUb);

    public Task<TypeBudget?> GetTypeBudgetAsync(long idTypeBudget, CancellationToken cancellationToken = default)
        => Task.FromResult(TypesBudget.GetValueOrDefault(idTypeBudget));

    public Task<TypeBudget?> GetTypeBudgetByCodeAsync(string codeType, CancellationToken cancellationToken = default)
    {
        var code = (codeType ?? string.Empty).Trim().ToUpperInvariant();
        return Task.FromResult(TypesBudget.Values.FirstOrDefault(t =>
            string.Equals(t.CodeType, code, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<PrevisionBudgetaire?> GetPrevisionAsync(long idPrevision, CancellationToken cancellationToken = default)
        => Task.FromResult(Previsions.GetValueOrDefault(idPrevision));

    public Task<long?> FindPrevisionIdAsync(
        DemandePaiementImputation imputation,
        long idVersion,
        CancellationToken cancellationToken = default)
    {
        if (imputation.FK_BudgetLigne is long idLigne)
            return Task.FromResult<long?>(idLigne);

        var match = Previsions.Values.FirstOrDefault(p =>
            p.FK_VersionBudgetaire == idVersion
            && p.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
            && (
                (imputation.FK_RubriqueBudgetaire.HasValue
                 && p.FK_RubriqueBudgetaire == imputation.FK_RubriqueBudgetaire
                 && string.IsNullOrWhiteSpace(imputation.LibelleItemAE)
                 && !imputation.FK_ItemBI.HasValue)
                || (imputation.LibelleItemAE is not null
                    && string.Equals(p.LibelleItemAE, imputation.LibelleItemAE.Trim(), StringComparison.Ordinal))
                || (imputation.FK_ItemBI.HasValue
                    && p.FK_ItemBI == imputation.FK_ItemBI
                    && imputation.DetailBI is not null
                    && string.Equals(p.DetailBI, imputation.DetailBI.Trim(), StringComparison.OrdinalIgnoreCase))));

        return Task.FromResult(match?.IdPrevision);
    }

    public Task<decimal> SumEngageDcMensuelAsync(
        long idExercice,
        long idUB,
        long idRB,
        byte mois,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EngagedImputations(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire == idRB
                        && i.Mois == mois)
            .Sum(i => i.MontantUsd));

    public Task<decimal> SumEngageDcAnnuelAsync(
        long idExercice,
        long idUB,
        long idRB,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EngagedImputations(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire == idRB
                        && i.Mois != null)
            .Sum(i => i.MontantUsd));

    public Task<decimal> SumEngageAeAnnuelAsync(
        long idExercice,
        long idUB,
        long idRB,
        string libelleItemAE,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
    {
        var action = libelleItemAE.Trim();
        return Task.FromResult(EngagedImputations(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire == idRB
                        && i.LibelleItemAE == action)
            .Sum(i => i.MontantUsd));
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
        return Task.FromResult(EngagedImputations(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_ItemBI == idItemBI
                        && i.DetailBI == detail)
            .Sum(i => i.MontantUsd));
    }

    public Task<DemandePaiementEntity> AddAsync(DemandePaiementEntity entity, CancellationToken cancellationToken = default)
    {
        entity.IdDemandePaiement = _nextDemandeId++;
        AttachNavigations(entity);
        Demandes.Add(entity);
        return Task.FromResult(entity);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (FailSaveChanges)
            throw new InvalidOperationException("Échec simulé de persistance.");
        if (IsChangeTrackerPolluted)
        {
            throw new Exception(
                "Simulated DbUpdateException: UPDATE conflict with CHECK CK_DPM_CAS_DOSSIER_Ordre (ChangeTracker contamination).");
        }

        return Task.CompletedTask;
    }

    public void ClearChangeTracker()
    {
        ClearChangeTrackerCallCount++;
        IsChangeTrackerPolluted = false;
    }

    public Task<DemandePaiementImputation?> GetImputationTrackedAsync(
        long idImputation,
        CancellationToken cancellationToken = default)
        => Task.FromResult(FindImputation(idImputation));

    public Task AddImputationAsync(
        DemandePaiementImputation imputation,
        CancellationToken cancellationToken = default)
    {
        imputation.IdImputation = _nextImputationId++;
        var demande = FindDemande(imputation.FK_DemandePaiement)
            ?? throw new InvalidOperationException("Demande introuvable pour l'imputation.");

        if (imputation.TypeBudget is null && TypesBudget.TryGetValue(imputation.FK_TypeBudget, out var tb))
            imputation.TypeBudget = tb;

        demande.Imputations.Add(imputation);
        return Task.CompletedTask;
    }

    public Task RemoveImputationAsync(
        DemandePaiementImputation imputation,
        CancellationToken cancellationToken = default)
    {
        var demande = FindDemande(imputation.FK_DemandePaiement);
        demande?.Imputations.Remove(imputation);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RubriqueBudgetaire>> ListRubriquesDcActivesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RubriqueBudgetaire>>(
            RubriquesDc.Where(r => r.Actif).OrderBy(r => r.CodeRB).ThenBy(r => r.IdRB).ToList());

    public Task<IReadOnlyDictionary<long, PrevisionBudgetaire>> GetPrevisionsDcParRubriqueAsync(
        long idVersion,
        long idUB,
        long idTypeBudgetDc,
        CancellationToken cancellationToken = default)
    {
        var map = Previsions.Values
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == idUB
                        && p.FK_TypeBudget == idTypeBudgetDc
                        && p.FK_RubriqueBudgetaire.HasValue)
            .GroupBy(p => p.FK_RubriqueBudgetaire!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        return Task.FromResult<IReadOnlyDictionary<long, PrevisionBudgetaire>>(map);
    }

    public Task<(IReadOnlyDictionary<EngageDcMensuelCle, decimal> Mensuel, IReadOnlyDictionary<long, decimal> Annuel)>
        SumEngageDcMapsAsync(
            long idExercice,
            long idUB,
            long? excludeDemandeId,
            CancellationToken cancellationToken = default)
    {
        var rows = EngagedImputations(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire != null
                        && i.Mois != null)
            .ToList();

        var mensuel = rows
            .GroupBy(i => new EngageDcMensuelCle(i.FK_RubriqueBudgetaire!.Value, i.Mois!.Value))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));
        var annuel = rows
            .GroupBy(i => i.FK_RubriqueBudgetaire!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));
        return Task.FromResult<(IReadOnlyDictionary<EngageDcMensuelCle, decimal>, IReadOnlyDictionary<long, decimal>)>(
            (mensuel, annuel));
    }

    public Task ReplaceImputationsDcForMoisAsync(
        long idDemande,
        byte mois,
        IReadOnlyList<DemandePaiementImputation> nouvelles,
        CancellationToken cancellationToken = default)
    {
        var demande = FindDemande(idDemande)
            ?? throw new InvalidOperationException("Demande introuvable pour l'imputation DC.");

        var aRetirer = demande.Imputations
            .Where(i => i.Mois == mois
                        && i.FK_RubriqueBudgetaire != null
                        && string.IsNullOrWhiteSpace(i.LibelleItemAE)
                        && i.FK_ItemBI == null
                        && string.IsNullOrWhiteSpace(i.DetailBI))
            .ToList();
        foreach (var row in aRetirer)
            demande.Imputations.Remove(row);

        foreach (var imputation in nouvelles)
        {
            if (imputation.IdImputation <= 0)
                imputation.IdImputation = _nextImputationId++;
            if (imputation.TypeBudget is null && TypesBudget.TryGetValue(imputation.FK_TypeBudget, out var tb))
                imputation.TypeBudget = tb;
            imputation.FK_DemandePaiement = idDemande;
            demande.Imputations.Add(imputation);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<long, PrevisionBudgetaire>> GetPrevisionsAeParRubriqueAsync(
        long idVersion,
        long idUB,
        long idTypeBudgetAe,
        string libelleItemAE,
        CancellationToken cancellationToken = default)
    {
        var item = (libelleItemAE ?? string.Empty).Trim();
        var map = Previsions.Values
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && p.FK_UniteBudgetaire == idUB
                        && p.FK_TypeBudget == idTypeBudgetAe
                        && p.FK_RubriqueBudgetaire.HasValue
                        && string.Equals((p.LibelleItemAE ?? string.Empty).Trim(), item, StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.FK_RubriqueBudgetaire!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        return Task.FromResult<IReadOnlyDictionary<long, PrevisionBudgetaire>>(map);
    }

    public Task<IReadOnlyDictionary<EngageAeAnnuelCle, decimal>> SumEngageAeMapsAsync(
        long idExercice,
        long idUB,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
    {
        var map = EngagedImputations(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_RubriqueBudgetaire != null
                        && !string.IsNullOrWhiteSpace(i.LibelleItemAE))
            .GroupBy(i => new EngageAeAnnuelCle(i.FK_RubriqueBudgetaire!.Value, i.LibelleItemAE!.Trim()))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));
        return Task.FromResult<IReadOnlyDictionary<EngageAeAnnuelCle, decimal>>(map);
    }

    public Task ReplaceImputationsAeForItemMoisAsync(
        long idDemande,
        string libelleItemAE,
        byte? mois,
        IReadOnlyList<DemandePaiementImputation> nouvelles,
        CancellationToken cancellationToken = default)
    {
        var demande = FindDemande(idDemande)
            ?? throw new InvalidOperationException("Demande introuvable pour l'imputation AE.");
        var item = (libelleItemAE ?? string.Empty).Trim();

        var aRetirer = demande.Imputations
            .Where(i => i.FK_RubriqueBudgetaire != null
                        && !string.IsNullOrWhiteSpace(i.LibelleItemAE)
                        && i.FK_ItemBI == null
                        && string.IsNullOrWhiteSpace(i.DetailBI)
                        && string.Equals((i.LibelleItemAE ?? string.Empty).Trim(), item, StringComparison.OrdinalIgnoreCase)
                        && i.Mois == mois)
            .ToList();
        foreach (var row in aRetirer)
            demande.Imputations.Remove(row);

        foreach (var imputation in nouvelles)
        {
            if (imputation.IdImputation <= 0)
                imputation.IdImputation = _nextImputationId++;
            if (imputation.TypeBudget is null && TypesBudget.TryGetValue(imputation.FK_TypeBudget, out var tb))
                imputation.TypeBudget = tb;
            imputation.FK_DemandePaiement = idDemande;
            demande.Imputations.Add(imputation);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, PrevisionBudgetaire>> GetPrevisionsBiParDetailAsync(
        long idVersion,
        long idUB,
        long idTypeBudgetBi,
        long idItemBI,
        CancellationToken cancellationToken = default)
    {
        var map = new Dictionary<string, PrevisionBudgetaire>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Previsions.Values.Where(p =>
                     p.FK_VersionBudgetaire == idVersion
                     && p.FK_UniteBudgetaire == idUB
                     && p.FK_TypeBudget == idTypeBudgetBi
                     && p.FK_ItemBI == idItemBI
                     && !string.IsNullOrWhiteSpace(p.DetailBI)))
        {
            var cle = BudgetWeb.Domain.DetailBI.DetailBILibelle.Normaliser(row.DetailBI);
            if (!string.IsNullOrEmpty(cle))
                map.TryAdd(cle, row);
        }

        return Task.FromResult<IReadOnlyDictionary<string, PrevisionBudgetaire>>(map);
    }

    public Task<IReadOnlyDictionary<EngageBiAnnuelCle, decimal>> SumEngageBiMapsAsync(
        long idExercice,
        long idUB,
        long? excludeDemandeId,
        CancellationToken cancellationToken = default)
    {
        var map = EngagedImputations(excludeDemandeId)
            .Where(i => i.FK_ExerciceBudgetaire == idExercice
                        && i.FK_UniteBudgetaire == idUB
                        && i.FK_ItemBI != null
                        && !string.IsNullOrWhiteSpace(i.DetailBI))
            .GroupBy(i => new EngageBiAnnuelCle(
                i.FK_ItemBI!.Value,
                BudgetWeb.Domain.DetailBI.DetailBILibelle.Normaliser(i.DetailBI!)))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MontantUsd));
        return Task.FromResult<IReadOnlyDictionary<EngageBiAnnuelCle, decimal>>(map);
    }

    public Task ReplaceImputationsBiForItemMoisAsync(
        long idDemande,
        long idItemBI,
        byte? mois,
        IReadOnlyList<DemandePaiementImputation> nouvelles,
        CancellationToken cancellationToken = default)
    {
        var demande = FindDemande(idDemande)
            ?? throw new InvalidOperationException("Demande introuvable pour l'imputation BI.");

        var aRetirer = demande.Imputations
            .Where(i => i.FK_ItemBI == idItemBI
                        && i.FK_RubriqueBudgetaire == null
                        && string.IsNullOrWhiteSpace(i.LibelleItemAE)
                        && i.Mois == mois)
            .ToList();
        foreach (var row in aRetirer)
            demande.Imputations.Remove(row);

        foreach (var imputation in nouvelles)
        {
            if (imputation.IdImputation <= 0)
                imputation.IdImputation = _nextImputationId++;
            if (imputation.TypeBudget is null && TypesBudget.TryGetValue(imputation.FK_TypeBudget, out var tb))
                imputation.TypeBudget = tb;
            imputation.FK_DemandePaiement = idDemande;
            demande.Imputations.Add(imputation);
        }

        return Task.CompletedTask;
    }

    public Task AddPieceAsync(PieceJointe piece, CancellationToken cancellationToken = default)
    {
        piece.IdPieceJointe = _nextPieceId++;
        var demande = FindDemande(piece.FK_DemandePaiement)
            ?? throw new InvalidOperationException("Demande introuvable pour la pièce.");
        demande.PiecesJointes.Add(piece);
        return Task.CompletedTask;
    }

    public Task<PieceJointe?> GetPieceTrackedAsync(long idPiece, CancellationToken cancellationToken = default)
        => Task.FromResult(
            Demandes.SelectMany(d => d.PiecesJointes).FirstOrDefault(p => p.IdPieceJointe == idPiece));

    public Task RemovePieceAsync(PieceJointe piece, CancellationToken cancellationToken = default)
    {
        var demande = FindDemande(piece.FK_DemandePaiement);
        demande?.PiecesJointes.Remove(piece);
        return Task.CompletedTask;
    }

    public Task ReplaceBeneficiairesAsync(
        long idDemande,
        IReadOnlyList<DemandePaiementBeneficiaire> beneficiaires,
        CancellationToken cancellationToken = default)
    {
        BeneficiairesByDemande[idDemande] = beneficiaires.ToList();
        var demande = FindDemande(idDemande);
        if (demande is not null)
            demande.Beneficiaires = beneficiaires.ToList();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CasDossierPieceObligatoire>> GetPiecesActivesAsync(
        long idCasDossier,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CasDossierPieceObligatoire>>(
            PiecesObligatoires
                .Where(p => p.FK_CasDossier == idCasDossier && p.Actif)
                .OrderBy(p => p.Ordre)
                .ToList());

    public Task<IReadOnlyList<CasDossierPieceObligatoire>> GetPiecesObligatoiresAsync(
        long idCasDossier,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CasDossierPieceObligatoire>>(
            PiecesObligatoires
                .Where(p => p.FK_CasDossier == idCasDossier && p.Actif && p.Obligatoire)
                .OrderBy(p => p.Ordre)
                .ToList());

    public Task<IReadOnlyList<JournalAudit>> GetHistoriqueAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<JournalAudit>>(
            Audits.Where(a => a.IdEntite == idDemande).OrderBy(a => a.DateHeure).ToList());

    public Task<IReadOnlyList<JournalAudit>> GetHistoriqueConsultationAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => GetHistoriqueAsync(idDemande, cancellationToken);

    public HashSet<long> UbAutorisees { get; } = [1, 2, 3, 10];

    /// <summary>
    /// Si non null et <see cref="PerimetreAccess.EstConfigure"/> : utilisé par le fake
    /// pour <see cref="UtilisateurPeutAccederUbAsync"/> (simule le repo réel).
    /// </summary>
    public PerimetreUtilisateurSnapshot? Perimetre { get; set; }

    /// <summary>FK_Departement par UB — requis quand un périmètre est configuré.</summary>
    public Dictionary<long, long> UbDepartements { get; } = new()
    {
        [1] = 1,
        [2] = 1,
        [3] = 2,
        [10] = 1
    };

    /// <summary>UBs pour lesquelles le proxy « prévision créée » est vrai (fallback).</summary>
    public HashSet<long> UbProxyPrevision { get; } = [1, 2, 3, 10];

    public Task<bool> UtilisateurPeutAccederUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        if (PerimetreAccess.EstConfigure(Perimetre))
        {
            if (!UbDepartements.TryGetValue(idUB, out var idDept))
                return Task.FromResult(false);
            return Task.FromResult(PerimetreAccess.PeutAccederUb(Perimetre!, idUB, idDept));
        }

        return Task.FromResult(UbProxyPrevision.Contains(idUB) || UbAutorisees.Contains(idUB));
    }

    public Task AddAuditAsync(
        long idUtilisateur,
        string operation,
        long idDemande,
        object? anciennes,
        object? nouvelles,
        CancellationToken cancellationToken = default)
    {
        Audits.Add(new JournalAudit
        {
            IdAudit = _nextAuditId++,
            Operation = operation,
            IdEntite = idDemande,
            DateHeure = DateTime.Now,
            AnciennesValeurs = anciennes?.ToString(),
            NouvellesValeurs = nouvelles?.ToString(),
        });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentEtabliListItemDto>> ListDocumentsEtablisAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default)
    {
        var debut = query.DateDebut.ToDateTime(TimeOnly.MinValue);
        var finExcl = query.DateFin.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var type = TypeDocumentEtabli.Normaliser(query.TypeDocument);
        var tous = string.IsNullOrEmpty(type);
        var rows = new List<DocumentEtabliListItemDto>();

        IEnumerable<DemandePaiementEntity> dpm = Demandes;
        if (query.IdExercice is long idEx)
            dpm = dpm.Where(d => d.FK_ExerciceBudgetaire == idEx);
        if (query.IdUB is long idUb)
            dpm = dpm.Where(d => d.FK_UniteBudgetaire == idUb);
        if (query.IdDepartement is long idDept)
        {
            dpm = dpm.Where(d =>
                (d.UniteBudgetaire?.FK_Departement ?? UbDepartements.GetValueOrDefault(d.FK_UniteBudgetaire)) == idDept);
        }

        var dpmMap = dpm.ToDictionary(d => d.IdDemandePaiement);

        if (tous || type == TypeDocumentEtabli.BilletConversion)
        {
            foreach (var b in Billets.Where(x =>
                         StatutBilletConversion.Normaliser(x.Statut) == StatutBilletConversion.Etabli
                         && x.DateEtabli >= debut
                         && x.DateEtabli < finExcl
                         && dpmMap.ContainsKey(x.FK_DemandePaiement)))
            {
                var d = dpmMap[b.FK_DemandePaiement];
                rows.Add(new DocumentEtabliListItemDto(
                    d.IdDemandePaiement,
                    d.Reference,
                    TypeDocumentEtabli.BilletConversion,
                    TypeDocumentEtabli.Libelle(TypeDocumentEtabli.BilletConversion),
                    b.DemandeChequeNumero ?? string.Empty,
                    b.DateEtabli,
                    b.DateConversion,
                    b.MontantCdf,
                    "CDF",
                    string.Empty,
                    FormatNom(b.UtilisateurEtabli),
                    TypeDocumentEtabli.PdfRouteSegment(TypeDocumentEtabli.BilletConversion),
                    d.FK_UniteBudgetaire));
            }
        }

        if (tous || type == TypeDocumentEtabli.PieceCaisse)
        {
            foreach (var p in PiecesCaisse.Where(x =>
                         StatutDocumentInstrumentPaiement.Normaliser(x.Statut) == StatutDocumentInstrumentPaiement.Etabli
                         && x.DateEtabli >= debut
                         && x.DateEtabli < finExcl
                         && dpmMap.ContainsKey(x.FK_DemandePaiement)))
            {
                var d = dpmMap[p.FK_DemandePaiement];
                rows.Add(new DocumentEtabliListItemDto(
                    d.IdDemandePaiement,
                    d.Reference,
                    TypeDocumentEtabli.PieceCaisse,
                    TypeDocumentEtabli.Libelle(TypeDocumentEtabli.PieceCaisse),
                    p.NumeroPiece,
                    p.DateEtabli,
                    p.DatePiece,
                    p.MontantFc,
                    "CDF",
                    p.BeneficiaireAffichage,
                    FormatNom(p.UtilisateurEtabli),
                    TypeDocumentEtabli.PdfRouteSegment(TypeDocumentEtabli.PieceCaisse),
                    d.FK_UniteBudgetaire));
            }
        }

        if (tous || type == TypeDocumentEtabli.BonProvisoire)
        {
            foreach (var b in BonsProvisoire.Where(x =>
                         StatutDocumentInstrumentPaiement.Normaliser(x.Statut) == StatutDocumentInstrumentPaiement.Etabli
                         && x.DateEtabli >= debut
                         && x.DateEtabli < finExcl
                         && dpmMap.ContainsKey(x.FK_DemandePaiement)))
            {
                var d = dpmMap[b.FK_DemandePaiement];
                rows.Add(new DocumentEtabliListItemDto(
                    d.IdDemandePaiement,
                    d.Reference,
                    TypeDocumentEtabli.BonProvisoire,
                    TypeDocumentEtabli.Libelle(TypeDocumentEtabli.BonProvisoire),
                    b.NumeroBon,
                    b.DateEtabli,
                    b.DateBon,
                    b.MontantFc,
                    "CDF",
                    b.BeneficiaireAffichage,
                    FormatNom(b.UtilisateurEtabli),
                    TypeDocumentEtabli.PdfRouteSegment(TypeDocumentEtabli.BonProvisoire),
                    d.FK_UniteBudgetaire));
            }
        }

        if (tous || type == TypeDocumentEtabli.MinuteCheque)
        {
            foreach (var m in MinutesCheque.Where(x =>
                         StatutDocumentInstrumentPaiement.Normaliser(x.Statut) == StatutDocumentInstrumentPaiement.Etabli
                         && x.DateEtabli >= debut
                         && x.DateEtabli < finExcl
                         && dpmMap.ContainsKey(x.FK_DemandePaiement)))
            {
                var d = dpmMap[m.FK_DemandePaiement];
                rows.Add(new DocumentEtabliListItemDto(
                    d.IdDemandePaiement,
                    d.Reference,
                    TypeDocumentEtabli.MinuteCheque,
                    TypeDocumentEtabli.Libelle(TypeDocumentEtabli.MinuteCheque),
                    m.NumeroOp,
                    m.DateEtabli,
                    m.DateDocument,
                    m.MontantPaiement,
                    m.DevisePaiement,
                    m.BeneficiaireAffichage,
                    FormatNom(m.UtilisateurEtabli),
                    TypeDocumentEtabli.PdfRouteSegment(TypeDocumentEtabli.MinuteCheque),
                    d.FK_UniteBudgetaire));
            }
        }

        return Task.FromResult<IReadOnlyList<DocumentEtabliListItemDto>>(
            rows.OrderByDescending(r => r.DateEtabli).ThenBy(r => r.Reference).ToList());
    }

    private static string? FormatNom(Utilisateur? u)
        => u is null ? null : string.Join(' ', new[] { u.Prenom, u.Nom }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public Task<BilletConversion?> GetBilletConversionByDemandeAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Billets.FirstOrDefault(b => b.FK_DemandePaiement == idDemande));

    public Task AddBilletConversionAsync(BilletConversion billet, CancellationToken cancellationToken = default)
    {
        if (ForceConcurrencyOnDocumentSaveIds.Contains(billet.FK_DemandePaiement))
            throw new DemandePaiementConcurrencyException();

        billet.IdBilletConversion = _nextBilletId++;
        billet.UtilisateurEtabli ??= new Utilisateur
        {
            IdUtilisateur = billet.FK_UtilisateurEtabli,
            NomUtilisateur = "charge.dp",
            Nom = "Charge",
            Prenom = "DP",
        };
        Billets.Add(billet);
        var demande = FindDemande(billet.FK_DemandePaiement);
        if (demande is not null)
            demande.BilletConversion = billet;
        return Task.CompletedTask;
    }

    public Task<PieceCaisse?> GetPieceCaisseByDemandeAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PiecesCaisse.FirstOrDefault(p => p.FK_DemandePaiement == idDemande));

    public Task<DemandePaiementAccesContext?> GetDemandeAccesContextAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetDemandeAccesContextAsyncCallCount++;
        var row = FindDemande(idDemande);
        if (row is null)
            return Task.FromResult<DemandePaiementAccesContext?>(null);

        AttachNavigations(row);
        return Task.FromResult<DemandePaiementAccesContext?>(new DemandePaiementAccesContext(
            row.IdDemandePaiement,
            row.Statut,
            row.FK_UniteBudgetaire,
            row.FK_UtilisateurCreation,
            row.FK_UtilisateurAssigne,
            row.TypeBudgetSollicite,
            row.TypeBudget?.CodeType,
            row.ModePaiementSollicite,
            row.Devise));
    }

    public Task<PieceCaissePdfData?> GetPieceCaissePdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetPieceCaissePdfDataAsyncCallCount++;
        var piece = PiecesCaisse.FirstOrDefault(p =>
            p.FK_DemandePaiement == idDemande
            && string.Equals(
                StatutDocumentInstrumentPaiement.Normaliser(p.Statut),
                StatutDocumentInstrumentPaiement.Etabli,
                StringComparison.Ordinal));

        if (piece is null)
            return Task.FromResult<PieceCaissePdfData?>(null);

        piece.UtilisateurEtabli ??= UtilisateurCharge();
        return Task.FromResult<PieceCaissePdfData?>(new PieceCaissePdfData(
            piece.NumeroPiece,
            piece.DatePiece,
            piece.ReferenceDemande,
            piece.Motif,
            piece.PieceJustificative,
            piece.BeneficiaireAffichage,
            piece.BeneficiaireMatricule,
            piece.BeneficiaireIdentite,
            piece.MontantFc,
            piece.MontantEnLettres,
            piece.RecuSnel,
            piece.Sr,
            piece.ComptabiliteGenerale,
            piece.Cp,
            piece.Cpa,
            piece.NumeroAppariement,
            piece.IdentifiantVerification,
            new PdfUserDisplayName(
                piece.UtilisateurEtabli.Nom,
                piece.UtilisateurEtabli.Prenom,
                piece.UtilisateurEtabli.NomUtilisateur)));
    }

    public Task<BonProvisoirePdfData?> GetBonProvisoirePdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetBonProvisoirePdfDataAsyncCallCount++;
        var bon = BonsProvisoire.FirstOrDefault(b =>
            b.FK_DemandePaiement == idDemande
            && string.Equals(
                StatutDocumentInstrumentPaiement.Normaliser(b.Statut),
                StatutDocumentInstrumentPaiement.Etabli,
                StringComparison.Ordinal));

        if (bon is null)
            return Task.FromResult<BonProvisoirePdfData?>(null);

        bon.UtilisateurEtabli ??= UtilisateurCharge();
        return Task.FromResult<BonProvisoirePdfData?>(new BonProvisoirePdfData(
            bon.NumeroBon,
            bon.DateBon,
            bon.ReferenceDemande,
            bon.Motif,
            bon.MentionJustificationRetrait,
            bon.BeneficiaireAffichage,
            bon.BeneficiaireMatricule,
            bon.BeneficiaireIdentite,
            bon.DirectionBeneficiaire,
            bon.MontantFc,
            bon.MontantEnLettres,
            bon.RecuCaisseCentrale,
            bon.CompteGeneral,
            bon.CompteParticulier,
            bon.NumeroAppariement,
            bon.IdentifiantVerification,
            new PdfUserDisplayName(
                bon.UtilisateurEtabli.Nom,
                bon.UtilisateurEtabli.Prenom,
                bon.UtilisateurEtabli.NomUtilisateur)));
    }

    public Task<MinuteChequePdfData?> GetMinuteChequePdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetMinuteChequePdfDataAsyncCallCount++;
        var minute = MinutesCheque.FirstOrDefault(m =>
            m.FK_DemandePaiement == idDemande
            && string.Equals(
                StatutDocumentInstrumentPaiement.Normaliser(m.Statut),
                StatutDocumentInstrumentPaiement.Etabli,
                StringComparison.Ordinal));

        if (minute is null)
            return Task.FromResult<MinuteChequePdfData?>(null);

        minute.UtilisateurEtabli ??= UtilisateurCharge();
        return Task.FromResult<MinuteChequePdfData?>(new MinuteChequePdfData(
            minute.NumeroOp,
            minute.DateDocument,
            minute.ReferenceDemande,
            minute.Motif,
            minute.BeneficiaireAffichage,
            minute.BeneficiaireAdresse,
            minute.BeneficiaireBanque,
            minute.BeneficiaireNumeroCompte,
            minute.MontantPaiement,
            minute.DevisePaiement,
            minute.MontantEnLettres,
            minute.CompteGeneral,
            minute.CpCa,
            minute.Ls,
            minute.SuiviExtraComptable,
            minute.NumeroAppariement,
            minute.MontantSuiviExtraComptable,
            minute.IdentifiantVerification,
            new PdfUserDisplayName(
                minute.UtilisateurEtabli.Nom,
                minute.UtilisateurEtabli.Prenom,
                minute.UtilisateurEtabli.NomUtilisateur)));
    }

    public Task<BilletConversionPdfData?> GetBilletConversionPdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetBilletConversionPdfDataAsyncCallCount++;
        var billet = Billets.FirstOrDefault(b =>
            b.FK_DemandePaiement == idDemande
            && string.Equals(
                StatutBilletConversion.Normaliser(b.Statut),
                StatutBilletConversion.Etabli,
                StringComparison.Ordinal));

        if (billet is null)
            return Task.FromResult<BilletConversionPdfData?>(null);

        var demande = FindDemande(idDemande);
        if (demande is null)
            return Task.FromResult<BilletConversionPdfData?>(null);

        AttachNavigations(demande);
        if (BeneficiairesByDemande.TryGetValue(idDemande, out var benefs))
            demande.Beneficiaires = benefs;

        var principal = demande.Beneficiaires
            .FirstOrDefault(b => b.EstPrincipal)
            ?? demande.Beneficiaires.OrderBy(b => b.Ordre).FirstOrDefault();

        PdfBeneficiairePrincipal? beneficiaire = principal is null
            ? null
            : new PdfBeneficiairePrincipal(
                principal.RaisonSociale,
                principal.NomComplet,
                principal.EstPrincipal,
                principal.Ordre);

        billet.UtilisateurEtabli ??= UtilisateurCharge();
        return Task.FromResult<BilletConversionPdfData?>(new BilletConversionPdfData(
            demande.IdDemandePaiement,
            demande.Reference,
            beneficiaire,
            billet.MontantDeviseOrigine,
            billet.DeviseOrigine,
            billet.TauxApplique,
            billet.DateConversion,
            billet.DemandeChequeNumero,
            billet.CoursEchangeBanque,
            billet.MontantCdf,
            billet.SoldeAPayerDevise,
            new PdfUserDisplayName(
                billet.UtilisateurEtabli.Nom,
                billet.UtilisateurEtabli.Prenom,
                billet.UtilisateurEtabli.NomUtilisateur),
            billet.UtilisateurApprouve is null
                ? null
                : new PdfUserDisplayName(
                    billet.UtilisateurApprouve.Nom,
                    billet.UtilisateurApprouve.Prenom,
                    billet.UtilisateurApprouve.NomUtilisateur),
            billet.UtilisateurVisa is null
                ? null
                : new PdfUserDisplayName(
                    billet.UtilisateurVisa.Nom,
                    billet.UtilisateurVisa.Prenom,
                    billet.UtilisateurVisa.NomUtilisateur)));
    }

    public Task<DemandePaiementPdfData?> GetDemandePaiementPdfDataAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        GetDemandePaiementPdfDataAsyncCallCount++;
        var row = FindDemande(idDemande);
        if (row is null)
            return Task.FromResult<DemandePaiementPdfData?>(null);

        AttachNavigations(row);
        if (BeneficiairesByDemande.TryGetValue(idDemande, out var benefs))
            row.Beneficiaires = benefs;

        var beneficiaires = row.Beneficiaires
            .OrderBy(b => b.Ordre)
            .Select(b => new DemandePaiementPdfBeneficiaireRow(
                b.TypeBeneficiaire,
                b.NomComplet,
                b.Matricule,
                b.Fonction,
                b.RaisonSociale,
                b.Rccm,
                b.Banque,
                b.NumeroCompte,
                b.EstPrincipal,
                b.Ordre))
            .ToList();

        var validations = row.ValidationsEntite
            .OrderBy(v => v.Ordre)
            .Select(v => new DemandePaiementPdfValidationRow(
                v.Niveau,
                v.Ordre,
                v.Statut,
                v.ModeValidation,
                v.UtilisateurValidateur?.Nom,
                v.UtilisateurDeclarant?.Nom,
                v.NomSignatairePhysique,
                v.FonctionSignatairePhysique,
                v.DateSignaturePhysique,
                v.DateValidation,
                v.Commentaire))
            .ToList();

        var documentSigne = row.PiecesJointes.Any(p =>
            string.Equals(p.CodeTypePiece, TypePieceJointeDpm.DocumentDpmSigne, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<DemandePaiementPdfData?>(new DemandePaiementPdfData(
            row.IdDemandePaiement,
            row.Reference,
            row.DateEmission,
            row.LieuEmission,
            row.Objet,
            row.MontantBrut,
            row.Devise,
            row.TypeBudgetSollicite,
            row.ItemSollicite,
            row.ModePaiementSollicite,
            row.CompteSection,
            row.Statut,
            row.DateSoumission,
            row.FK_Demandeur ?? 0L,
            row.FK_CasDossier,
            row.Demandeur?.Libelle,
            row.CasDossier?.Libelle,
            documentSigne,
            beneficiaires,
            validations));
    }

    public Task AddPieceCaisseAsync(PieceCaisse piece, CancellationToken cancellationToken = default)
    {
        if (ForceConcurrencyOnDocumentSaveIds.Contains(piece.FK_DemandePaiement))
            throw new DemandePaiementConcurrencyException();

        piece.IdPieceCaisse = _nextPieceCaisseId++;
        piece.UtilisateurEtabli ??= UtilisateurCharge();
        PiecesCaisse.Add(piece);
        AttachDocumentToDemande(piece.FK_DemandePaiement, d => d.PieceCaisse = piece);
        return Task.CompletedTask;
    }

    public Task<BonProvisoire?> GetBonProvisoireByDemandeAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => Task.FromResult(BonsProvisoire.FirstOrDefault(b => b.FK_DemandePaiement == idDemande));

    public Task AddBonProvisoireAsync(BonProvisoire bon, CancellationToken cancellationToken = default)
    {
        if (ForceConcurrencyOnDocumentSaveIds.Contains(bon.FK_DemandePaiement))
            throw new DemandePaiementConcurrencyException();

        bon.IdBonProvisoire = _nextBonProvisoireId++;
        bon.UtilisateurEtabli ??= UtilisateurCharge();
        BonsProvisoire.Add(bon);
        AttachDocumentToDemande(bon.FK_DemandePaiement, d => d.BonProvisoire = bon);
        return Task.CompletedTask;
    }

    public Task<MinuteCheque?> GetMinuteChequeByDemandeAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => Task.FromResult(MinutesCheque.FirstOrDefault(m => m.FK_DemandePaiement == idDemande));

    public Task AddMinuteChequeAsync(MinuteCheque minute, CancellationToken cancellationToken = default)
    {
        if (ForceConcurrencyOnDocumentSaveIds.Contains(minute.FK_DemandePaiement))
            throw new DemandePaiementConcurrencyException();

        minute.IdMinuteCheque = _nextMinuteChequeId++;
        minute.UtilisateurEtabli ??= UtilisateurCharge();
        MinutesCheque.Add(minute);
        AttachDocumentToDemande(minute.FK_DemandePaiement, d => d.MinuteCheque = minute);
        return Task.CompletedTask;
    }

    public Task<ParametreInstrumentPaiement?> GetParametreInstrumentAsync(
        string typeInstrument,
        CancellationToken cancellationToken = default)
    {
        var type = typeInstrument.Trim().ToUpperInvariant();
        return Task.FromResult(
            ParametresInstrument.FirstOrDefault(p => p.Actif && p.TypeInstrument == type));
    }

    public Task<ParametreInstrumentPaiement?> GetParametreInstrumentByTypeAsync(
        string typeInstrument,
        CancellationToken cancellationToken = default)
    {
        var type = typeInstrument.Trim().ToUpperInvariant();
        return Task.FromResult(
            ParametresInstrument.FirstOrDefault(p => p.TypeInstrument == type));
    }

    public Task UpsertParametreInstrumentAsync(
        ParametreInstrumentPaiement parametre,
        CancellationToken cancellationToken = default)
    {
        var type = parametre.TypeInstrument.Trim().ToUpperInvariant();
        var existing = ParametresInstrument.FirstOrDefault(p => p.TypeInstrument == type);
        if (existing is null)
        {
            parametre.IdParametreInstrumentPaiement = ParametresInstrument.Count + 1;
            parametre.TypeInstrument = type;
            ParametresInstrument.Add(parametre);
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
        }

        return Task.CompletedTask;
    }

    public Task<string> GenererNumeroPieceCaisseAsync(short annee, CancellationToken cancellationToken = default)
        => Task.FromResult(GenererNumeroDocument("PC", annee, PiecesCaisse.Select(p => p.NumeroPiece)));

    public Task<string> GenererNumeroBonProvisoireAsync(short annee, CancellationToken cancellationToken = default)
        => Task.FromResult(GenererNumeroDocument("BP", annee, BonsProvisoire.Select(b => b.NumeroBon)));

    public Task<string> GenererNumeroMinuteChequeAsync(short annee, CancellationToken cancellationToken = default)
        => Task.FromResult(GenererNumeroDocument("OP", annee, MinutesCheque.Select(m => m.NumeroOp)));

    private string GenererNumeroDocument(string prefixCode, short annee, IEnumerable<string> numeros)
    {
        var prefix = $"{prefixCode}-{annee}-";
        var maxNum = numeros
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out var num) ? num : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{maxNum + 1:D5}";
    }

    private static Utilisateur UtilisateurCharge()
        => new()
        {
            IdUtilisateur = 1,
            NomUtilisateur = "charge.dp",
            Nom = "Charge",
            Prenom = "DP",
        };

    private void AttachDocumentToDemande(long idDemande, Action<DemandePaiementEntity> attach)
    {
        var demande = FindDemande(idDemande);
        if (demande is not null)
            attach(demande);
    }

    public Task<int> UpdateBrouillonHeaderIfModifiableAsync(
        long idDemande,
        DemandePaiementBrouillonHeaderPatch patch,
        CancellationToken cancellationToken = default)
    {
        var demande = FindDemande(idDemande);
        if (demande is null)
            return Task.FromResult(0);

        var statut = StatutDemandePaiement.Normaliser(demande.Statut);
        if (statut is not StatutDemandePaiement.Brouillon and not StatutDemandePaiement.ACorriger)
            return Task.FromResult(0);

        demande.DateEmission = patch.DateEmission;
        demande.LieuEmission = patch.LieuEmission;
        demande.Objet = patch.Objet;
        demande.CompteSection = patch.CompteSection;
        demande.MontantBrut = patch.MontantBrut;
        demande.FK_Devise = patch.FK_Devise;
        demande.Devise = patch.Devise;
        demande.TypeBudgetSollicite = patch.TypeBudgetSollicite;
        demande.ItemSollicite = patch.ItemSollicite;
        demande.ModePaiementSollicite = patch.ModePaiementSollicite;
        demande.FK_UtilisateurModification = patch.FK_UtilisateurModification;
        demande.DateModification = patch.DateModification;
        return Task.FromResult(1);
    }

    /// <summary>IDs pour lesquels <see cref="UpdateIfStatutMatchesAsync"/> retourne 0 (simule conflit optimistic).</summary>
    public HashSet<long> ForceConcurrencyOnUpdateIds { get; } = [];

    /// <summary>IDs pour lesquels l'ajout d'un document (billet/instrument) lève une concurrence.</summary>
    public HashSet<long> ForceConcurrencyOnDocumentSaveIds { get; } = [];

    public Task<int> UpdateIfStatutMatchesAsync(
        long idDemande,
        string statutAttendu,
        DemandePaiementConditionalUpdatePatch patch,
        CancellationToken cancellationToken = default)
    {
        if (ForceConcurrencyOnUpdateIds.Contains(idDemande))
            return Task.FromResult(0);

        var demande = FindDemande(idDemande);
        if (demande is null)
            return Task.FromResult(0);

        var attendu = StatutDemandePaiement.Normaliser(statutAttendu);
        if (!string.Equals(StatutDemandePaiement.Normaliser(demande.Statut), attendu, StringComparison.Ordinal))
            return Task.FromResult(0);

        if (patch.NouveauStatut is not null)
            demande.Statut = patch.NouveauStatut;

        if (patch.FK_UtilisateurModification is long fkModif)
            demande.FK_UtilisateurModification = fkModif;
        if (patch.DateModification is DateTime dateModif)
            demande.DateModification = dateModif;
        if (patch.FK_UtilisateurSoumission is long fkSoum)
            demande.FK_UtilisateurSoumission = fkSoum;
        if (patch.EffacerSoumission)
        {
            demande.FK_UtilisateurSoumission = null;
            demande.DateSoumission = null;
        }
        else if (patch.DateSoumission is DateTime dateSoum)
            demande.DateSoumission = dateSoum;
        if (patch.FK_UtilisateurReception is long fkRecep)
            demande.FK_UtilisateurReception = fkRecep;
        if (patch.DateReception is DateTime dateRecep)
            demande.DateReception = dateRecep;
        if (patch.FK_UtilisateurControle is long fkCtrl)
            demande.FK_UtilisateurControle = fkCtrl;
        if (patch.DateControle is DateTime dateCtrl)
            demande.DateControle = dateCtrl;
        if (patch.FK_UtilisateurVisa is long fkVisa)
            demande.FK_UtilisateurVisa = fkVisa;
        if (patch.DateVisa is DateTime dateVisa)
            demande.DateVisa = dateVisa;
        if (patch.FK_UtilisateurRetour is long fkRetour)
            demande.FK_UtilisateurRetour = fkRetour;
        if (patch.DateRetour is DateTime dateRetour)
            demande.DateRetour = dateRetour;
        if (patch.MotifRetour is not null)
            demande.MotifRetour = patch.MotifRetour;
        if (patch.CommentaireRetour is not null)
            demande.CommentaireRetour = patch.CommentaireRetour;
        if (patch.FK_TypeBudget is long fkType)
            demande.FK_TypeBudget = fkType;
        if (patch.TypeInstrumentPaiement is not null)
            demande.TypeInstrumentPaiement = patch.TypeInstrumentPaiement;
        if (patch.DevisePaiement is not null)
            demande.DevisePaiement = patch.DevisePaiement;
        if (patch.MontantPaiement is decimal montantPaiement)
            demande.MontantPaiement = montantPaiement;
        if (patch.TauxPaiement is decimal tauxPaiement)
            demande.TauxPaiement = tauxPaiement;
        if (patch.FK_TauxChangePaiement is long fkTauxPaiement)
            demande.FK_TauxChangePaiement = fkTauxPaiement;
        if (patch.TauxConversion is decimal tauxConv)
            demande.TauxConversion = tauxConv;
        if (patch.MontantUsd is decimal montantUsd)
            demande.MontantUsd = montantUsd;
        if (patch.FK_TauxChange is long fkTaux)
            demande.FK_TauxChange = fkTaux;
        if (patch.ModePaiementSollicite is not null)
            demande.ModePaiementSollicite = patch.ModePaiementSollicite;
        if (patch.TypeBudgetSollicite is not null)
            demande.TypeBudgetSollicite = patch.TypeBudgetSollicite;
        if (patch.ItemSollicite is not null)
            demande.ItemSollicite = patch.ItemSollicite;
        if (patch.MettreAJourAssigne)
            demande.FK_UtilisateurAssigne = patch.FK_UtilisateurAssigne;

        return Task.FromResult(1);
    }

    public Task DesactiverRoutagesActifsAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        foreach (var r in Routages.Where(r => r.FK_DemandePaiement == idDemande && r.EstActif))
            r.EstActif = false;
        return Task.CompletedTask;
    }

    public Task AddRoutageAsync(
        DemandePaiementRoutage routage,
        CancellationToken cancellationToken = default)
    {
        routage.IdRoutage = _nextRoutageId++;
        Routages.Add(routage);
        var demande = FindDemande(routage.FK_DemandePaiement);
        if (demande is not null)
            demande.Routages.Add(routage);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DemandePaiementRoutage>> GetRoutagesAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DemandePaiementRoutage>>(
            Routages
                .Where(r => r.FK_DemandePaiement == idDemande)
                .OrderByDescending(r => r.DateRoutage)
                .ThenByDescending(r => r.IdRoutage)
                .ToList());

    public int RoutagesLectureUtilisateurBatchCallCount { get; private set; }

    public Task<IReadOnlyList<DemandePaiementRoutage>> GetRoutagesLectureAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        var rows = Routages
            .Where(r => r.FK_DemandePaiement == idDemande)
            .OrderBy(r => r.DateRoutage)
            .ThenBy(r => r.IdRoutage)
            .ToList();

        if (rows.Count == 0)
            return Task.FromResult<IReadOnlyList<DemandePaiementRoutage>>(rows);

        RoutagesLectureUtilisateurBatchCallCount++;
        var userIds = rows
            .SelectMany(r => new[] { r.FK_UtilisateurSource, r.FK_UtilisateurCible })
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        foreach (var idUtilisateur in userIds)
        {
            if (!UtilisateursAssignes.TryGetValue(idUtilisateur, out var user))
                continue;

            foreach (var row in rows)
            {
                if (row.FK_UtilisateurSource == idUtilisateur)
                    row.UtilisateurSource ??= user;
                if (row.FK_UtilisateurCible == idUtilisateur)
                    row.UtilisateurCible ??= user;
            }
        }

        return Task.FromResult<IReadOnlyList<DemandePaiementRoutage>>(rows);
    }

    public Task DeleteDemandeGraphAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken = default)
    {
        if (FailSaveChanges)
            return SaveChangesAsync(cancellationToken);

        var id = demande.IdDemandePaiement;
        Audits.RemoveAll(a => string.Equals(a.Entite, "DEMANDE_PAIEMENT", StringComparison.OrdinalIgnoreCase)
                              && a.IdEntite == id);
        BeneficiairesByDemande.Remove(id);
        Billets.RemoveAll(b => b.FK_DemandePaiement == id);
        PiecesCaisse.RemoveAll(p => p.FK_DemandePaiement == id);
        BonsProvisoire.RemoveAll(b => b.FK_DemandePaiement == id);
        MinutesCheque.RemoveAll(m => m.FK_DemandePaiement == id);
        Demandes.Remove(demande);
        return SaveChangesAsync(cancellationToken);
    }

    public Task AddValidationsEntiteAsync(
        IReadOnlyList<DemandePaiementValidation> validations,
        CancellationToken cancellationToken = default)
    {
        foreach (var v in validations)
        {
            var demande = FindDemande(v.FK_DemandePaiement);
            if (demande is null)
                continue;
            v.IdValidation = demande.ValidationsEntite.Count == 0
                ? 1
                : demande.ValidationsEntite.Max(x => x.IdValidation) + 1;
            demande.ValidationsEntite.Add(v);
        }

        return Task.CompletedTask;
    }

    public void SeedPieceObligatoire(long idCasDossier = 1)
    {
        PiecesObligatoires =
        [
            new CasDossierPieceObligatoire
            {
                IdPieceObligatoire = 1,
                FK_CasDossier = idCasDossier,
                CodeTypePiece = "FACTURE",
                Libelle = "Facture fournisseur",
                Ordre = 1,
                Actif = true,
                Obligatoire = true,
            },
        ];
    }

    public void SeedPieceFacultative(long idCasDossier = 1)
    {
        PiecesObligatoires =
        [
            new CasDossierPieceObligatoire
            {
                IdPieceObligatoire = 1,
                FK_CasDossier = idCasDossier,
                CodeTypePiece = "FACTURE",
                Libelle = "Facture fournisseur",
                Ordre = 1,
                Actif = true,
                Obligatoire = true,
            },
            new CasDossierPieceObligatoire
            {
                IdPieceObligatoire = 2,
                FK_CasDossier = idCasDossier,
                CodeTypePiece = "RAPPORT",
                Libelle = "Rapport de mission",
                Ordre = 2,
                Actif = true,
                Obligatoire = false,
            },
        ];
    }

    public void SeedPieceDesactivee(long idCasDossier = 1)
    {
        PiecesObligatoires =
        [
            new CasDossierPieceObligatoire
            {
                IdPieceObligatoire = 1,
                FK_CasDossier = idCasDossier,
                CodeTypePiece = "FACTURE",
                Libelle = "Facture fournisseur",
                Ordre = 1,
                Actif = true,
                Obligatoire = true,
            },
            new CasDossierPieceObligatoire
            {
                IdPieceObligatoire = 3,
                FK_CasDossier = idCasDossier,
                CodeTypePiece = "ANCIEN",
                Libelle = "Ancien document",
                Ordre = 3,
                Actif = false,
                Obligatoire = false,
            },
        ];
    }

    public int TotalSnapshotCount()
        => Demandes.SelectMany(d => d.Imputations).Sum(i => i.Snapshots.Count);

    private IEnumerable<DemandePaiementImputation> EngagedImputations(long? excludeDemandeId)
        => Demandes
            .Where(d => string.Equals(
                StatutDemandePaiement.Normaliser(d.Statut),
                StatutDemandePaiement.ViseeBudgetairement,
                StringComparison.Ordinal))
            .Where(d => excludeDemandeId is null || d.IdDemandePaiement != excludeDemandeId)
            .SelectMany(d => d.Imputations);

    private DemandePaiementEntity? FindDemande(long id)
        => Demandes.FirstOrDefault(d => d.IdDemandePaiement == id);

    private DemandePaiementImputation? FindImputation(long id)
        => Demandes.SelectMany(d => d.Imputations).FirstOrDefault(i => i.IdImputation == id);

    private void AttachNavigations(DemandePaiementEntity row)
    {
        row.ExerciceBudgetaire ??= new ExerciceBudgetaire
        {
            IdExercice = row.FK_ExerciceBudgetaire,
            Annee = 2026,
            Statut = StatutExerciceBudgetaire.Ouvert,
        };
        row.UniteBudgetaire ??= new UniteBudgetaire
        {
            IdUB = row.FK_UniteBudgetaire,
            CodeUB = "UB10",
            Libelle = "UB Test",
            FK_Departement = 1,
            Departement = new Departement
            {
                IdDepartement = 1,
                Code = "DG",
                Libelle = "Direction Générale",
                Actif = true,
            },
        };
        row.Demandeur ??= row.FK_Demandeur is long idDem
            ? new Demandeur
            {
                IdDemandeur = idDem,
                Code = "DDK/DKC/DG",
                Libelle = "Demandeur test",
                FK_UniteBudgetaire = row.FK_UniteBudgetaire,
                Actif = true,
                UniteBudgetaire = row.UniteBudgetaire,
            }
            : null;
        row.CasDossier ??= new CasDossier
        {
            IdCasDossier = row.FK_CasDossier,
            Code = "CAS1",
            Libelle = "Cas standard",
            Actif = true,
        };
        if (row.FK_TypeBudget is long idType)
        {
            row.TypeBudget ??= TypesBudget.GetValueOrDefault(idType)
                ?? new TypeBudget { IdTypeBudget = idType, CodeType = "DC", Libelle = "DC" };
        }
        if (row.FK_VersionBudgetaire is long idVersion)
        {
            row.VersionBudgetaire ??= new VersionBudgetaire
            {
                IdVersion = idVersion,
                NumeroVersion = 1,
                Libelle = "V1",
                FK_ExerciceBudgetaire = row.FK_ExerciceBudgetaire,
            };
        }

        if (row.FK_UtilisateurAssigne is long idAssigne)
        {
            row.UtilisateurAssigne ??= UtilisateursAssignes.GetValueOrDefault(idAssigne)
                ?? new Utilisateur
                {
                    IdUtilisateur = idAssigne,
                    Nom = $"Nom{idAssigne}",
                    Prenom = $"Prenom{idAssigne}",
                    NomUtilisateur = $"user{idAssigne}",
                    Actif = true,
                };
        }

        foreach (var imputation in row.Imputations)
        {
            imputation.TypeBudget ??= TypesBudget.GetValueOrDefault(imputation.FK_TypeBudget);
            imputation.UniteBudgetaire ??= row.UniteBudgetaire;
            if (imputation.FK_RubriqueBudgetaire is long idRb)
            {
                imputation.RubriqueBudgetaire ??= RubriquesDc.FirstOrDefault(r => r.IdRB == idRb)
                    ?? new RubriqueBudgetaire { IdRB = idRb, CodeRB = "00100", Libelle = "Rubrique test", Actif = true };
            }
        }
    }

    private List<DemandeSnapshot> CaptureSnapshot()
        => Demandes.Select(d => new DemandeSnapshot
        {
            Id = d.IdDemandePaiement,
            Statut = d.Statut,
            FkVersion = d.FK_VersionBudgetaire,
            Imputations = d.Imputations.Select(i => new ImputationSnapshot
            {
                Id = i.IdImputation,
                FkBudgetLigne = i.FK_BudgetLigne,
                Snapshots = i.Snapshots.Select(s => CloneSnapshot(s)).ToList(),
            }).ToList(),
        }).ToList();

    private void RestoreSnapshot(List<DemandeSnapshot> snapshot)
    {
        foreach (var snap in snapshot)
        {
            var demande = FindDemande(snap.Id);
            if (demande is null)
                continue;

            demande.Statut = snap.Statut;
            demande.FK_VersionBudgetaire = snap.FkVersion;

            foreach (var impSnap in snap.Imputations)
            {
                var imputation = FindImputation(impSnap.Id);
                if (imputation is null)
                    continue;

                imputation.FK_BudgetLigne = impSnap.FkBudgetLigne;
                imputation.Snapshots.Clear();
                foreach (var s in impSnap.Snapshots)
                    imputation.Snapshots.Add(CloneSnapshot(s));
            }
        }

        while (Demandes.Count > snapshot.Count)
            Demandes.RemoveAt(Demandes.Count - 1);
    }

    private static DemandePaiementImputationSnapshot CloneSnapshot(DemandePaiementImputationSnapshot s)
        => new()
        {
            IdSnapshot = s.IdSnapshot,
            FK_Imputation = s.FK_Imputation,
            FK_DemandePaiement = s.FK_DemandePaiement,
            DateSnapshot = s.DateSnapshot,
            BudgetMensuel = s.BudgetMensuel,
            CreditEngageMensuel = s.CreditEngageMensuel,
            CreditDisponibleMensuelAvantVisa = s.CreditDisponibleMensuelAvantVisa,
            BudgetAnnuel = s.BudgetAnnuel,
            CreditEngageAnnuel = s.CreditEngageAnnuel,
            CreditDisponibleAnnuelAvantVisa = s.CreditDisponibleAnnuelAvantVisa,
            MontantPrevision = s.MontantPrevision,
            EcartPrevisionImputation = s.EcartPrevisionImputation,
            MontantBrut = s.MontantBrut,
            Devise = s.Devise,
            TauxConversion = s.TauxConversion,
            MontantUsd = s.MontantUsd,
            FK_BudgetLigne = s.FK_BudgetLigne,
        };

    private sealed class DemandeSnapshot
    {
        public long Id { get; init; }
        public string Statut { get; set; } = string.Empty;
        public long? FkVersion { get; set; }
        public List<ImputationSnapshot> Imputations { get; init; } = [];
    }

    private sealed class ImputationSnapshot
    {
        public long Id { get; init; }
        public long? FkBudgetLigne { get; set; }
        public List<DemandePaiementImputationSnapshot> Snapshots { get; init; } = [];
    }
}

internal sealed class FakeDemandeurRepo : IDemandeurRepository
{
    private long _nextId = 1;

    public List<Demandeur> Items { get; } = [];

    /// <summary>Dernière entité passée à AddAsync (pour tests de persistance).</summary>
    public Demandeur? LastAdded { get; private set; }

    /// <summary>Indique si la navigation UB était présente au moment de l'insert (avant reload).</summary>
    public bool LastAddedHadUbNavigationAtInsert { get; private set; }

    public Dictionary<long, UniteBudgetaire> Ubs { get; set; } = new()
    {
        [10] = new UniteBudgetaire
        {
            IdUB = 10,
            CodeUB = "UB001",
            Libelle = "UB Test",
            FK_Departement = 1,
            Actif = true,
            Departement = new Departement
            {
                IdDepartement = 1,
                Code = "DG",
                Libelle = "Direction Générale",
                Actif = true,
            },
        },
    };

    public FakeDemandeurRepo()
    {
        Items.Add(new Demandeur
        {
            IdDemandeur = 1,
            Code = "DDK/DKC/DG",
            Libelle = "Demandeur DDK",
            FK_UniteBudgetaire = 10,
            Actif = true,
            DateCreation = DateTime.UtcNow,
            FK_UtilisateurCreation = 1,
            UniteBudgetaire = Ubs[10],
        });
        _nextId = 2;
    }

    public Task<IReadOnlyList<Demandeur>> ListAsync(bool? actifOnly = true, CancellationToken cancellationToken = default)
    {
        IEnumerable<Demandeur> q = Items;
        if (actifOnly == true)
            q = q.Where(d => d.Actif);
        var rows = q.OrderBy(d => d.Code).ToList();
        foreach (var row in rows)
            AttachUbNavigation(row);
        return Task.FromResult<IReadOnlyList<Demandeur>>(rows);
    }

    public Task<Demandeur?> GetByIdAsync(long idDemandeur, CancellationToken cancellationToken = default)
    {
        var row = Items.FirstOrDefault(d => d.IdDemandeur == idDemandeur);
        AttachUbNavigation(row);
        return Task.FromResult(row);
    }

    private void AttachUbNavigation(Demandeur? row)
    {
        if (row is null || row.UniteBudgetaire is not null)
            return;
        if (Ubs.TryGetValue(row.FK_UniteBudgetaire, out var ub))
            row.UniteBudgetaire = ub;
    }

    public Task<Demandeur?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var c = code.Trim().ToUpperInvariant();
        return Task.FromResult(Items.FirstOrDefault(d => d.Code == c));
    }

    public Task<Demandeur?> GetTrackedAsync(long idDemandeur, CancellationToken cancellationToken = default)
        => GetByIdAsync(idDemandeur, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var c = code.Trim().ToUpperInvariant();
        return Task.FromResult(Items.Any(d => d.Code == c && (excludeId is null || d.IdDemandeur != excludeId)));
    }

    public Task<UniteBudgetaire?> GetUniteBudgetaireAsync(long idUB, CancellationToken cancellationToken = default)
        => Task.FromResult(Ubs.GetValueOrDefault(idUB));

    public Task<Demandeur> AddAsync(Demandeur entity, CancellationToken cancellationToken = default)
    {
        LastAddedHadUbNavigationAtInsert = entity.UniteBudgetaire is not null;
        LastAdded = entity;
        entity.IdDemandeur = _nextId++;
        Items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class FakeDeviseRepo : IDeviseRepository
{
    public List<Devise> Items { get; } =
    [
        new() { IdDevise = 1, Code = "CDF", Libelle = "Franc congolais", Symbole = "FC", Actif = true },
        new() { IdDevise = 2, Code = "USD", Libelle = "Dollar américain", Symbole = "$", Actif = true },
        new() { IdDevise = 3, Code = "EUR", Libelle = "Euro", Symbole = "€", Actif = true },
    ];

    public Task<IReadOnlyList<Devise>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
    {
        IEnumerable<Devise> q = Items;
        if (actifsSeulement == true)
            q = q.Where(d => d.Actif);
        return Task.FromResult<IReadOnlyList<Devise>>(q.OrderBy(d => d.Code).ToList());
    }

    public Task<Devise?> GetByIdAsync(long idDevise, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(d => d.IdDevise == idDevise));

    public Task<Devise?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var c = code.Trim().ToUpperInvariant();
        return Task.FromResult(Items.FirstOrDefault(d => d.Code == c));
    }

    public Task<bool> CodeExistsAsync(string code, long? excludeId, CancellationToken cancellationToken = default)
    {
        var c = code.Trim().ToUpperInvariant();
        return Task.FromResult(Items.Any(d => d.Code == c && (excludeId == null || d.IdDevise != excludeId)));
    }

    public Task<bool> EstUtiliseeAsync(long idDevise, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<Devise> AddAsync(Devise entity, CancellationToken cancellationToken = default)
    {
        entity.IdDevise = Items.Count == 0 ? 1 : Items.Max(d => d.IdDevise) + 1;
        Items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal static class DemandePaiementTestData
{
    public static CreateDemandePaiementRequest SampleCreateRequest(
        decimal montantBrut = 100m,
        string devise = "USD",
        long idDemandeur = 1,
        long? idUB = null,
        string typeBudgetSollicite = TypeBudgetCode.DepensesCourantes,
        string? itemSollicite = null,
        string modePaiementSollicite = ModePaiementDpm.Caisse,
        DateOnly? dateEmission = null,
        string? objet = null)
        => new(
            dateEmission ?? new DateOnly(2026, 3, 1),
            "Kinshasa",
            IdExercice: 1,
            IdDemandeur: idDemandeur,
            IdUB: idUB,
            IdCasDossier: 1,
            Objet: objet ?? "Frais de mission",
            CompteSection: null,
            MontantBrut: montantBrut,
            Devise: devise,
            TypeBudgetSollicite: typeBudgetSollicite,
            ItemSollicite: itemSollicite,
            ModePaiementSollicite: modePaiementSollicite,
            Beneficiaires: null);

    public static UpdateDemandePaiementRequest SampleUpdateRequest(
        decimal montantBrut = 150m,
        string devise = "USD",
        string typeBudgetSollicite = TypeBudgetCode.DepensesCourantes,
        string? itemSollicite = null,
        string modePaiementSollicite = ModePaiementDpm.Caisse)
        => new(
            new DateOnly(2026, 3, 15),
            "Lubumbashi",
            Objet: "Frais de mission révisés",
            CompteSection: "S01",
            MontantBrut: montantBrut,
            Devise: devise,
            TypeBudgetSollicite: typeBudgetSollicite,
            ItemSollicite: itemSollicite,
            ModePaiementSollicite: modePaiementSollicite,
            Beneficiaires: null);

    public static CreateImputationRequest ImputationDc(
        decimal montantUsd = 100m,
        byte mois = 3,
        long? idBudgetLigne = 100)
        => new(
            Ordre: 1,
            IdTypeBudget: 1,
            IdUB: 10,
            IdExercice: 1,
            IdRubriqueBudgetaire: 100,
            Mois: mois,
            LibelleItemAE: null,
            IdGroupeItemAE: null,
            IdItemBI: null,
            DetailBI: null,
            IdBudgetLigne: idBudgetLigne,
            MontantBrut: montantUsd,
            Devise: "USD",
            NumeroFicheSuivi: null);

    public static CreateImputationRequest ImputationAe(
        decimal montantUsd = 500m,
        string libelleItemAe = "Travaux réseau MT",
        long? idBudgetLigne = 200)
        => new(
            Ordre: 1,
            IdTypeBudget: 2,
            IdUB: 10,
            IdExercice: 1,
            IdRubriqueBudgetaire: 200,
            Mois: null,
            LibelleItemAE: libelleItemAe,
            IdGroupeItemAE: null,
            IdItemBI: null,
            DetailBI: null,
            IdBudgetLigne: idBudgetLigne,
            MontantBrut: montantUsd,
            Devise: "USD",
            NumeroFicheSuivi: null);

    public static CreateImputationRequest ImputationBi(
        decimal montantUsd = 1_000m,
        long idItemBi = 5,
        string detailBi = "Ligne A — transformateur",
        long? idBudgetLigne = 300)
        => new(
            Ordre: 1,
            IdTypeBudget: 3,
            IdUB: 10,
            IdExercice: 1,
            IdRubriqueBudgetaire: null,
            Mois: null,
            LibelleItemAE: null,
            IdGroupeItemAE: null,
            IdItemBI: idItemBi,
            DetailBI: detailBi,
            IdBudgetLigne: idBudgetLigne,
            MontantBrut: montantUsd,
            Devise: "USD",
            NumeroFicheSuivi: null);

    public static UploadDemandePaiementPieceMetadata SamplePieceMeta()
        => new(
            IdPieceObligatoire: 1,
            CodeTypePiece: "FACTURE",
            Libelle: "Facture fournisseur");

    public static UploadDemandePaiementPieceMetadata SampleInstrumentPieceMeta(
        string code = TypeInstrumentPaiement.PieceCaisse)
        => new(
            IdPieceObligatoire: null,
            CodeTypePiece: code,
            Libelle: code);

    public static Task<DemandePaiementPieceDto> AddSamplePieceAsync(
        DemandePaiementService svc,
        long idDemande)
    {
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 sample-piece");
        return svc.AddPieceAsync(
            idDemande,
            SamplePieceMeta(),
            new MemoryStream(bytes),
            "facture.pdf",
            "application/pdf");
    }

    public static async Task EtablirDocumentInstrumentStandardAsync(
        FakeDemandePaiementRepo repo,
        DemandePaiementService svc,
        long idDemande,
        string code = TypeInstrumentPaiement.PieceCaisse)
    {
        repo.SeedParametreInstrument(code);

        var detail = await svc.GetByIdAsync(idDemande)
            ?? throw new InvalidOperationException("Demande introuvable.");

        if (BilletConversionRules.NecessiteBillet(detail.ModePaiementSollicite, detail.Devise))
        {
            await EtablirBilletConversionStandardAsync(svc, idDemande);
        }

        switch (TypeInstrumentPaiement.Normaliser(code))
        {
            case TypeInstrumentPaiement.PieceCaisse:
                await svc.EtablirPieceCaisseAsync(idDemande, new EtablirPieceCaisseRequest());
                break;
            case TypeInstrumentPaiement.BonProvisoire:
                await svc.EtablirBonProvisoireAsync(idDemande, new EtablirBonProvisoireRequest());
                break;
            case TypeInstrumentPaiement.MinuteCheque:
                await svc.EtablirMinuteChequeAsync(idDemande, new EtablirMinuteChequeRequest());
                break;
            default:
                throw new InvalidOperationException($"Instrument inconnu : {code}");
        }
    }

    public static async Task<DemandePaiementPieceDto> AddSampleInstrumentPieceAsync(
        FakeDemandePaiementRepo repo,
        DemandePaiementService svc,
        long idDemande,
        string code = TypeInstrumentPaiement.PieceCaisse)
    {
        await EtablirDocumentInstrumentStandardAsync(repo, svc, idDemande, code);
        return new DemandePaiementPieceDto(
            0,
            null,
            code,
            code,
            false,
            $"{code.ToLowerInvariant()}-etabli.pdf",
            $"uploads/{code.ToLowerInvariant()}-etabli.pdf",
            "instrumenthash001",
            0,
            DateTime.Now);
    }

    [Obsolete("Utiliser SamplePieceMeta / AddSamplePieceAsync.")]
    public static CreateDemandePaiementPieceRequest SamplePiece()
        => new(
            IdPieceObligatoire: 1,
            CodeTypePiece: "FACTURE",
            Libelle: "Facture fournisseur",
            EstObligatoire: true,
            NomFichierOriginal: "facture.pdf",
            CheminRelatif: "uploads/facture.pdf",
            HashSha256: "abc123def456",
            TailleOctets: 2048);

    [Obsolete("Utiliser SampleInstrumentPieceMeta / AddSampleInstrumentPieceAsync.")]
    public static CreateDemandePaiementPieceRequest SampleInstrumentPiece(
        string code = TypeInstrumentPaiement.PieceCaisse)
        => new(
            IdPieceObligatoire: null,
            CodeTypePiece: code,
            Libelle: code,
            EstObligatoire: true,
            NomFichierOriginal: $"{code.ToLowerInvariant()}.pdf",
            CheminRelatif: $"uploads/{code.ToLowerInvariant()}.pdf",
            HashSha256: "instrumenthash001",
            TailleOctets: 1024);

    public static PrevisionBudgetaire PrevisionDc(
        long id = 100,
        decimal montantAnnuel = 120_000m,
        string mode = ModePrevisionCode.Annuel)
        => new()
        {
            IdPrevision = id,
            FK_VersionBudgetaire = 4,
            FK_UniteBudgetaire = 10,
            FK_RubriqueBudgetaire = 100,
            FK_TypeBudget = 1,
            MontantAnnuel = montantAnnuel,
            ModePrevision = new ModePrevision
            {
                IdModePrevision = 1,
                CodeMode = mode,
                Libelle = mode,
            },
            TypeBudget = new TypeBudget { IdTypeBudget = 1, CodeType = TypeBudgetCode.DepensesCourantes, Libelle = "DC" },
        };

    public static PrevisionBudgetaire PrevisionAe(long id = 200, decimal montantAnnuel = 50_000m)
        => new()
        {
            IdPrevision = id,
            FK_VersionBudgetaire = 4,
            FK_UniteBudgetaire = 10,
            FK_RubriqueBudgetaire = 200,
            FK_TypeBudget = 2,
            LibelleItemAE = "Travaux réseau MT",
            MontantAnnuel = montantAnnuel,
            ModePrevision = new ModePrevision { IdModePrevision = 1, CodeMode = ModePrevisionCode.Annuel, Libelle = "Annuel" },
            TypeBudget = new TypeBudget { IdTypeBudget = 2, CodeType = TypeBudgetCode.ActionsExploitation, Libelle = "AE" },
        };

    public static PrevisionBudgetaire PrevisionBi(
        long id = 300,
        decimal montantAnnuel = 80_000m,
        long idItemBI = 5,
        string? detailBI = "Installation électrique")
        => new()
        {
            IdPrevision = id,
            FK_VersionBudgetaire = 4,
            FK_UniteBudgetaire = 10,
            FK_ItemBI = idItemBI,
            DetailBI = detailBI,
            FK_TypeBudget = 3,
            MontantAnnuel = montantAnnuel,
            ModePrevision = new ModePrevision { IdModePrevision = 1, CodeMode = ModePrevisionCode.Annuel, Libelle = "Annuel" },
            TypeBudget = new TypeBudget { IdTypeBudget = 3, CodeType = TypeBudgetCode.BudgetInvestissement, Libelle = "BI" },
        };

    public static DemandePaiementImputation ImputationBiEntity(
        long idDemande,
        long idItemBi = 5,
        string detailBi = "Installation électrique",
        byte? mois = null,
        decimal montantBrut = 0m,
        decimal montantUsd = 0m)
        => new()
        {
            FK_DemandePaiement = idDemande,
            Ordre = 1,
            FK_TypeBudget = 3,
            FK_UniteBudgetaire = 10,
            FK_ExerciceBudgetaire = 1,
            FK_ItemBI = idItemBi,
            DetailBI = detailBi,
            Mois = mois,
            MontantBrut = montantBrut,
            Devise = "USD",
            TauxConversion = 1m,
            MontantUsd = montantUsd,
            FK_UtilisateurCreation = 1,
            DateImputation = DateTime.Now,
            TypeBudget = new TypeBudget { IdTypeBudget = 3, CodeType = TypeBudgetCode.BudgetInvestissement, Libelle = "BI" },
        };

    public static DemandePaiementService CreateServiceForUser(
        FakeDemandePaiementRepo repo,
        FakeUser user,
        FakeDemandeurRepo? demandeurs = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new BudgetWeb.Application.Options.PieceJointeStorageOptions());
        return new DemandePaiementService(
            repo,
            new FakeCasDossierRepo(repo),
            demandeurs ?? new FakeDemandeurRepo(),
            new FakeDeviseRepo(),
            new FakeTauxChangeService(),
            user,
            new FakePerimetreReader(repo),
            new FakeItemBIRepository(),
            new InMemoryFileStorage(),
            new PieceJointeUploadValidator(options),
            new FakeDemandePaiementDocumentRenderer(),
            new FakeBilletConversionDocumentRenderer(),
            new FakePieceCaisseDocumentRenderer(),
            new FakeBonProvisoireDocumentRenderer(),
            new FakeMinuteChequeDocumentRenderer(),
            new FakeFicheImputationBudgetaireRenderer(),
            NullLogger<DemandePaiementService>.Instance);
    }

    public static DemandePaiementService CreateService(
        FakeDemandePaiementRepo repo,
        IReadOnlyList<string>? permissions = null,
        FakeDemandeurRepo? demandeurs = null,
        IPerimetreUtilisateurReader? perimetre = null,
        IFileStorage? fileStorage = null,
        IDemandePaiementDocumentRenderer? renderer = null,
        IBilletConversionDocumentRenderer? billetRenderer = null,
        IPieceCaisseDocumentRenderer? pieceCaisseRenderer = null,
        IBonProvisoireDocumentRenderer? bonProvisoireRenderer = null,
        IMinuteChequeDocumentRenderer? minuteChequeRenderer = null,
        IFicheImputationBudgetaireRenderer? ficheImputationRenderer = null,
        ITauxChangeService? tauxChange = null,
        FakeCasDossierRepo? casDossiers = null)
    {
        var user = new FakeUser { Permissions = permissions ?? AppPermissions.AdminFull };
        var options = Microsoft.Extensions.Options.Options.Create(
            new BudgetWeb.Application.Options.PieceJointeStorageOptions());
        return new DemandePaiementService(
            repo,
            casDossiers ?? new FakeCasDossierRepo(repo),
            demandeurs ?? new FakeDemandeurRepo(),
            new FakeDeviseRepo(),
            tauxChange ?? new FakeTauxChangeService(),
            user,
            perimetre ?? new FakePerimetreReader(repo),
            new FakeItemBIRepository(),
            fileStorage ?? new InMemoryFileStorage(),
            new BudgetWeb.Application.Services.PieceJointeUploadValidator(options),
            renderer ?? new FakeDemandePaiementDocumentRenderer(),
            billetRenderer ?? new FakeBilletConversionDocumentRenderer(),
            pieceCaisseRenderer ?? new FakePieceCaisseDocumentRenderer(),
            bonProvisoireRenderer ?? new FakeBonProvisoireDocumentRenderer(),
            minuteChequeRenderer ?? new FakeMinuteChequeDocumentRenderer(),
            ficheImputationRenderer ?? new FakeFicheImputationBudgetaireRenderer(),
            NullLogger<DemandePaiementService>.Instance);
    }

    /// <summary>Établit le billet de conversion si mode CAISSE et devise demande ≠ CDF.</summary>
    public static async Task EtablirBilletConversionStandardAsync(
        DemandePaiementService charge,
        long idDemande,
        DateOnly? dateConversion = null)
    {
        var detail = await charge.GetByIdAsync(idDemande);
        if (detail is null
            || !BilletConversionRules.NecessiteBillet(detail.ModePaiementSollicite, detail.Devise))
            return;

        await charge.EtablirBilletConversionAsync(
            idDemande,
            new EtablirBilletConversionRequest(DateConversion: dateConversion));
    }

    public static async Task<DemandePaiementDetailDto> SoumettreApresValidationEntiteAsync(
        FakeDemandePaiementRepo repo,
        long idDemande,
        DemandePaiementService? agentSvc = null)
    {
        var agentPerms = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur);
        var validationAgent = CreateService(repo, agentPerms);
        await validationAgent.EnvoyerEnValidationAsync(idDemande);
        await CreateService(repo, AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur))
            .ValiderN1ElectroniqueAsync(idDemande);
        await CreateService(repo, AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice))
            .ValiderN2ElectroniqueAsync(idDemande);
        var submitter = agentSvc ?? validationAgent;
        return await submitter.SoumettreAsync(idDemande);
    }

    public static async Task AddDocumentSigneSampleAsync(
        DemandePaiementService svc,
        long idDemande)
    {
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        await svc.AddPieceAsync(
            idDemande,
            new UploadDemandePaiementPieceMetadata(
                null,
                TypePieceJointeDpm.DocumentDpmSigne,
                "Document DPM signé"),
            stream,
            "dpm-signe.pdf",
            "application/pdf");
    }

    public static DemandeurService CreateDemandeurService(
        FakeDemandeurRepo? repo = null,
        IReadOnlyList<string>? permissions = null,
        FakeDemandePaiementRepo? perimetreRepo = null)
    {
        var user = new FakeUser { Permissions = permissions ?? AppPermissions.AdminFull };
        IPerimetreUtilisateurReader reader = perimetreRepo is null
            ? new EmptyFakePerimetreReaderForDemandeur()
            : new FakePerimetreReader(perimetreRepo);
        var acces = new PerimetreAccesService(reader, user);
        return new DemandeurService(repo ?? new FakeDemandeurRepo(), user, acces);
    }
}

/// <summary>Proxy prévision ouvert pour tests demandeur admin.</summary>
internal sealed class EmptyFakePerimetreReaderForDemandeur : IPerimetreUtilisateurReader
{
    public Task<PerimetreUtilisateurSnapshot?> GetAsync(long idUtilisateur, CancellationToken cancellationToken = default)
        => Task.FromResult<PerimetreUtilisateurSnapshot?>(null);

    public Task<long?> GetDepartementUbAsync(long idUB, CancellationToken cancellationToken = default)
        => Task.FromResult<long?>(1);

    public Task<bool> UtilisateurPeutAccederUbAsync(long idUtilisateur, long idUB, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<IReadOnlyList<long>> ResoudreIdsUbPerimetreAsync(long idUtilisateur, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<long>>([]);

    public Task<bool> UtilisateurACreePrevisionSurUbAsync(long idUtilisateur, long idUB, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<IReadOnlyList<long>> ResoudreIdsUbProxyPrevisionAsync(long idUtilisateur, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<long>>([1, 2, 3, 10]);
}

internal sealed class FakeItemBIRepository : IItemBIRepository
{
    private static readonly ItemBIDto[] Items =
    [
        new(5, "BI-05", "Installations techniques", null, null, null, 1, null, true, DateTime.Now, 0, 0),
        new(6, "BI-06", "Mobilier", null, null, null, 1, null, true, DateTime.Now, 0, 0),
    ];

    public Task<IReadOnlyList<ItemBIDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ItemBIDto>>(Items);

    public Task<ItemBIDto?> GetByIdAsync(long idItemBI, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(i => i.IdItemBI == idItemBI));

    public Task<bool> ExistsByCodeAsync(string codeItem, long? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> ExistsByIdAsync(long idItemBI, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(i => i.IdItemBI == idItemBI));

    public Task<bool> WouldCreateCycleAsync(long idItemBI, long parentId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<int> CountEnfantsAsync(long idItemBI, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<int> CountPrevisionsAsync(long idItemBI, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<ItemBIDto> CreateAsync(
        string codeItem,
        string libelle,
        long? parentId,
        int niveau,
        string? categorie,
        bool actif,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ItemBIDto?> UpdateAsync(
        long idItemBI,
        string codeItem,
        string libelle,
        long? parentId,
        int niveau,
        string? categorie,
        bool actif,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ItemBIDto?> SetActifAsync(long idItemBI, bool actif, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> DeleteAsync(long idItemBI, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

internal sealed class FakeCasDossierRepo : ICasDossierRepository
{
    private readonly FakeDemandePaiementRepo _dpm;
    private long _nextCasId = 100;
    private long _nextPieceId = 100;

    public FakeCasDossierRepo(FakeDemandePaiementRepo dpm) => _dpm = dpm;

    public Task<IReadOnlyList<CasDossier>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
    {
        var cas = _dpm.Demandes
            .Select(d => d.CasDossier)
            .Where(c => c is not null)
            .DistinctBy(c => c!.IdCasDossier)
            .Select(c => c!)
            .AsEnumerable();
        if (actifsSeulement == true)
            cas = cas.Where(c => c.Actif);
        return Task.FromResult<IReadOnlyList<CasDossier>>(cas.OrderBy(c => c.Ordre).ToList());
    }

    public Task<CasDossier?> GetByIdAsync(long idCasDossier, CancellationToken cancellationToken = default)
        => Task.FromResult(
            _dpm.Demandes.Select(d => d.CasDossier).FirstOrDefault(c => c?.IdCasDossier == idCasDossier));

    public Task<CasDossier?> GetWithPiecesAsync(long idCasDossier, CancellationToken cancellationToken = default)
    {
        var cas = _dpm.Demandes.Select(d => d.CasDossier).FirstOrDefault(c => c?.IdCasDossier == idCasDossier);
        if (cas is null)
            return Task.FromResult<CasDossier?>(null);
        cas.PiecesObligatoires = _dpm.PiecesObligatoires
            .Where(p => p.FK_CasDossier == idCasDossier)
            .OrderBy(p => p.Ordre)
            .ToList();
        return Task.FromResult<CasDossier?>(cas);
    }

    public Task<CasDossier?> GetTrackedWithPiecesAsync(long idCasDossier, CancellationToken cancellationToken = default)
        => GetWithPiecesAsync(idCasDossier, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, long? excludeId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<CasDossier> AddAsync(CasDossier entity, CancellationToken cancellationToken = default)
    {
        entity.IdCasDossier = _nextCasId++;
        return Task.FromResult(entity);
    }

    public Task<CasDossierPieceObligatoire?> GetPieceByIdAsync(long idPieceObligatoire, CancellationToken cancellationToken = default)
        => Task.FromResult(_dpm.PiecesObligatoires.FirstOrDefault(p => p.IdPieceObligatoire == idPieceObligatoire));

    public Task<CasDossierPieceObligatoire?> GetPieceTrackedAsync(long idPiece, CancellationToken cancellationToken = default)
        => GetPieceByIdAsync(idPiece, cancellationToken);

    public Task<bool> PieceCodeExistsAsync(long idCasDossier, string codeTypePiece, long? excludeId, CancellationToken cancellationToken = default)
        => Task.FromResult(_dpm.PiecesObligatoires.Any(
            p => p.FK_CasDossier == idCasDossier
                 && string.Equals(p.CodeTypePiece, codeTypePiece, StringComparison.OrdinalIgnoreCase)
                 && (excludeId == null || p.IdPieceObligatoire != excludeId)));

    public Task<bool> PieceEstReferenceeAsync(long idPieceObligatoire, CancellationToken cancellationToken = default)
        => Task.FromResult(_dpm.Demandes
            .SelectMany(d => d.PiecesJointes)
            .Any(p => p.FK_PieceObligatoire == idPieceObligatoire));

    public Task AddPieceAsync(CasDossierPieceObligatoire piece, CancellationToken cancellationToken = default)
    {
        if (piece.IdPieceObligatoire <= 0)
            piece.IdPieceObligatoire = _nextPieceId++;
        _dpm.PiecesObligatoires.Add(piece);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
