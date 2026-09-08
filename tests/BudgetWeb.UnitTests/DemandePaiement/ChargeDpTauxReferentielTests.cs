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

using Xunit;



namespace BudgetWeb.UnitTests.DemandePaiement;



/// <summary>Phase 3 — Charge DP branchée sur le référentiel TAUX_CHANGE.</summary>

public class ChargeDpTauxReferentielTests

{

    private static readonly DateOnly DateTraitementAout = new(2026, 8, 27);

    private static readonly DateOnly DateTraitementSept = new(2026, 9, 5);

    private static readonly DateOnly DateEmissionDemandeur = new(2026, 8, 26);



    [Fact]

    public async Task TraiterCharge_UtiliseTauxReferentiel_FkEtSnapshot()

    {

        using var _ = new DateTraitementDpmScope(DateTraitementAout);

        var rows = HistorizedTauxRows();

        var taux = CreateHistorizedService(rows);

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm, tauxChange: taux);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                montantBrut: 10_000m,

                dateEmission: DateEmissionDemandeur));



        await DemandePaiementTestData.AddSamplePieceAsync(charge, created.IdDemandePaiement);

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, created.IdDemandePaiement);

        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);



        var traite = await charge.TraiterChargeAsync(

            created.IdDemandePaiement,

            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));



        Assert.Equal(2_290m, traite.TauxPaiement);

        Assert.Equal(1L, traite.IdTauxChangePaiement);

        Assert.Equal(10_000m * 2_290m, traite.MontantPaiement);

        Assert.Equal(1m, traite.TauxConversion);

        Assert.Equal(10_000m, traite.MontantUsd);

        Assert.Null(traite.IdTauxChange);



        var entity = repo.Demandes.Single();

        Assert.Equal(2_290m, entity.TauxPaiement);

        Assert.Equal(1L, entity.FK_TauxChangePaiement);

        Assert.Equal(10_000m * 2_290m, entity.MontantPaiement);

        Assert.Null(entity.FK_TauxChange);

    }



    [Fact]

    public async Task TraiterCharge_SansTauxApplicable_Refuse()

    {

        using var _ = new DateTraitementDpmScope(DateTraitementAout);

        var taux = CreateHistorizedService([]);

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm, tauxChange: taux);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                montantBrut: 5_000m,

                dateEmission: DateEmissionDemandeur));



        await DemandePaiementTestData.AddSamplePieceAsync(charge, created.IdDemandePaiement);

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

            DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement));



        Assert.Contains("Aucun taux", ex.Message, StringComparison.OrdinalIgnoreCase);

    }



    [Fact]

    public async Task TraiterCharge_RefuseTauxManuelClient()

    {

        using var _ = new DateTraitementDpmScope(DateTraitementAout);

        var rows = HistorizedTauxRows();

        var taux = CreateHistorizedService(rows);

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm, tauxChange: taux);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                montantBrut: 1_000m,

                dateEmission: DateEmissionDemandeur));



        await DemandePaiementTestData.AddSamplePieceAsync(charge, created.IdDemandePaiement);

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, created.IdDemandePaiement);

        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);



        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

            charge.TraiterChargeAsync(

                created.IdDemandePaiement,

                new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", 9_999m, 99L)));



        Assert.Contains("manuelle", ex.Message, StringComparison.OrdinalIgnoreCase);

    }



    [Fact]

    public async Task TraiterCharge_RefuseTauxManuelClient_SansBillet()

    {

        using var _ = new DateTraitementDpmScope(DateTraitementAout);

        var rows = HistorizedTauxRows();

        var taux = CreateHistorizedService(rows);

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm, tauxChange: taux);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                montantBrut: 1_000m,

                dateEmission: DateEmissionDemandeur));



        await DemandePaiementTestData.AddSamplePieceAsync(charge, created.IdDemandePaiement);

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

            charge.TraiterChargeAsync(

                created.IdDemandePaiement,

                new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", 9_999m, 99L)));



        Assert.Contains("billet de conversion", ex.Message, StringComparison.OrdinalIgnoreCase);

    }



    [Fact]

    public async Task TraiterCharge_SansBilletEtabli_Refuse()

    {

        using var _ = new DateTraitementDpmScope(DateTraitementAout);

        var rows = HistorizedTauxRows();

        var taux = CreateHistorizedService(rows);

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm, tauxChange: taux);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                montantBrut: 1_000m,

                dateEmission: DateEmissionDemandeur));



        await DemandePaiementTestData.AddSamplePieceAsync(charge, created.IdDemandePaiement);

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

            charge.TraiterChargeAsync(

                created.IdDemandePaiement,

                new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null)));



        Assert.Contains("billet de conversion", ex.Message, StringComparison.OrdinalIgnoreCase);

    }



    [Fact]

    public async Task TraiterCharge_DateTraitement_SelectionneBonTauxHistorique()

    {

        var rows = HistorizedTauxRows();

        var taux = CreateHistorizedService(rows);

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm, tauxChange: taux);



        using (new DateTraitementDpmScope(DateTraitementAout))

        {

            var aout = await charge.CreateBrouillonAsync(

                DemandePaiementTestData.SampleCreateRequest(

                    montantBrut: 100m,

                    dateEmission: DateEmissionDemandeur));

            await DemandePaiementTestData.AddSamplePieceAsync(charge, aout.IdDemandePaiement);

            await charge.EntrerTraitementAsync(aout.IdDemandePaiement);

            await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, aout.IdDemandePaiement);

            await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, aout.IdDemandePaiement);

            var traiteAout = await charge.TraiterChargeAsync(

                aout.IdDemandePaiement,

                new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));

            Assert.Equal(2_290m, traiteAout.TauxPaiement);

        }



        using (new DateTraitementDpmScope(DateTraitementSept))

        {

            var sept = await charge.CreateBrouillonAsync(

                DemandePaiementTestData.SampleCreateRequest(

                    montantBrut: 100m,

                    dateEmission: DateEmissionDemandeur));

            await DemandePaiementTestData.AddSamplePieceAsync(charge, sept.IdDemandePaiement);

            await charge.EntrerTraitementAsync(sept.IdDemandePaiement);

            await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, sept.IdDemandePaiement);

            await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, sept.IdDemandePaiement);

            var traiteSept = await charge.TraiterChargeAsync(

                sept.IdDemandePaiement,

                new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));

            Assert.Equal(2_350m, traiteSept.TauxPaiement);

        }

    }



    [Fact]

    public async Task TraiterCharge_OperationFigee_NonRetroactiveApresNouveauTaux()

    {

        using var _ = new DateTraitementDpmScope(DateTraitementAout);

        var rows = HistorizedTauxRows();

        var taux = CreateHistorizedService(rows);

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm, tauxChange: taux);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                montantBrut: 500m,

                dateEmission: DateEmissionDemandeur));



        await DemandePaiementTestData.AddSamplePieceAsync(charge, created.IdDemandePaiement);

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, created.IdDemandePaiement);

        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);

        await charge.TraiterChargeAsync(

            created.IdDemandePaiement,

            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));



        rows.Add(HistorizedRow(3, "USD", "CDF", 2_800m, new DateOnly(2026, 10, 1)));



        var entity = repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement);

        Assert.Equal(2_290m, entity.TauxPaiement);

        Assert.Equal(500m * 2_290m, entity.MontantPaiement);

        Assert.Equal(1L, entity.FK_TauxChangePaiement);

    }



    [Fact]

    public async Task TraiterCharge_MemeDevise_IdentiteSansFk()

    {

        using var _ = new DateTraitementDpmScope(DateTraitementAout);

        var rows = HistorizedTauxRows();

        var taux = CreateHistorizedService(rows);

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm, tauxChange: taux);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                montantBrut: 2_000m,

                devise: "CDF",

                modePaiementSollicite: ModePaiementDpm.Banque,

                dateEmission: DateEmissionDemandeur));



        await DemandePaiementTestData.AddSamplePieceAsync(charge, created.IdDemandePaiement);

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge,

            created.IdDemandePaiement,

            TypeInstrumentPaiement.MinuteCheque);



        var traite = await charge.TraiterChargeAsync(

            created.IdDemandePaiement,

            new TraitementChargeDpmRequest("MINUTE_CHEQUE", "CDF", null, null));



        Assert.Equal(1m, traite.TauxPaiement);

        Assert.Null(traite.IdTauxChangePaiement);

        Assert.Equal(2_000m, traite.MontantPaiement);

    }



    private static List<TauxChange> HistorizedTauxRows()

        =>

        [

            HistorizedRow(1, "USD", "CDF", 2_290m, new DateOnly(2026, 8, 1), StatutTauxChange.Inactif),

            HistorizedRow(2, "USD", "CDF", 2_350m, new DateOnly(2026, 9, 1), StatutTauxChange.Actif),

        ];



    private static TauxChange HistorizedRow(

        long id,

        string source,

        string cible,

        decimal taux,

        DateOnly dateEffet,

        string statut = StatutTauxChange.Actif)

        => new()

        {

            IdTauxChange = id,

            DeviseSource = source,

            DeviseCible = cible,

            Taux = taux,

            DateEffet = dateEffet,

            Statut = statut,

            FK_UtilisateurCreation = 1,

            DateCreation = DateTime.Now,

            UtilisateurCreation = new Utilisateur { IdUtilisateur = 1, NomUtilisateur = "admin" },

        };



    private static TauxChangeService CreateHistorizedService(List<TauxChange> rows)

        => new(new HistorizedTauxChangeRepository(rows), new HistorizedPaireRepository(), new HistorizedFakeUser());



    private sealed class HistorizedPaireRepository : IPaireTauxChangeRepository

    {

        private static readonly PaireTauxChange Row = new()

        {

            IdPaireTauxChange = 1,

            DeviseBase = "USD",

            DeviseQuote = "CDF",

            Actif = true,

            DateCreation = DateTime.UtcNow,

        };



        public Task<IReadOnlyList<PaireTauxChange>> ListActivesAsync(CancellationToken cancellationToken = default)

            => Task.FromResult<IReadOnlyList<PaireTauxChange>>([Row]);



        public Task<PaireTauxChange?> FindCanoniqueAsync(

            string deviseBase,

            string deviseQuote,

            CancellationToken cancellationToken = default)

            => Task.FromResult<PaireTauxChange?>(

                Row.DeviseBase == deviseBase.Trim().ToUpperInvariant()

                && Row.DeviseQuote == deviseQuote.Trim().ToUpperInvariant()

                    ? Row

                    : null);



        public Task<PaireTauxChange?> ResolveForDevisesAsync(

            string deviseA,

            string deviseB,

            CancellationToken cancellationToken = default)

            => Task.FromResult<PaireTauxChange?>(Row);



        public Task<bool> DevisesActivesExistentAsync(

            string deviseBase,

            string deviseQuote,

            CancellationToken cancellationToken = default)

            => Task.FromResult(true);



        public Task<PaireTauxChange> CreerOuObtenirPaireCanoniqueAsync(

            string deviseBase,

            string deviseQuote,

            CancellationToken cancellationToken = default)

            => Task.FromResult(Row);



        public Task<PaireTauxChange> AddAsync(PaireTauxChange entity, CancellationToken cancellationToken = default)

            => Task.FromResult(entity);



        public Task SaveChangesAsync(CancellationToken cancellationToken = default)

            => Task.CompletedTask;

    }



    private sealed class HistorizedFakeUser : ICurrentUserService

    {

        public long? UserId => 1;

        public string? Username => "charge";

        public string? DisplayName => "Charge DP";

        public IReadOnlyList<string> Roles { get; } = [];

        public IReadOnlyList<string> Permissions { get; } = AppPermissions.ChargeDpm;

        public bool IsAuthenticated => true;

        public bool IsInRole(string role) => false;

        public bool HasPermission(string permission) =>

            Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));

        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException();

    }



    private sealed class HistorizedTauxChangeRepository(List<TauxChange> rows) : ITauxChangeRepository

    {

        public Task<IReadOnlyList<TauxChange>> ListAsync(

            TauxChangeListQuery query,

            CancellationToken cancellationToken = default)

            => Task.FromResult<IReadOnlyList<TauxChange>>(rows.ToList());



        public Task<TauxChange?> GetByIdAsync(long idTauxChange, CancellationToken cancellationToken = default)

            => Task.FromResult(rows.FirstOrDefault(r => r.IdTauxChange == idTauxChange));



        public Task<TauxChange?> GetByIdTrackedAsync(long idTauxChange, CancellationToken cancellationToken = default)

            => GetByIdAsync(idTauxChange, cancellationToken);



        public Task<TauxChange?> FindApplicableCanoniqueAsync(

            string deviseBase,

            string deviseQuote,

            DateOnly dateReference,

            CancellationToken cancellationToken = default)

        {

            var row = rows

                .Where(r => r.DeviseSource == deviseBase.Trim().ToUpperInvariant()

                            && r.DeviseCible == deviseQuote.Trim().ToUpperInvariant()

                            && r.DateEffet <= dateReference)

                .OrderByDescending(r => r.DateEffet)

                .ThenByDescending(r => r.IdTauxChange)

                .FirstOrDefault();

            return Task.FromResult(row);

        }



        public Task<TauxChange?> FindActifCourantCanoniqueAsync(

            string deviseBase,

            string deviseQuote,

            CancellationToken cancellationToken = default)

        {

            var row = rows

                .Where(r => r.DeviseSource == deviseBase.Trim().ToUpperInvariant()

                            && r.DeviseCible == deviseQuote.Trim().ToUpperInvariant()

                            && r.Statut == StatutTauxChange.Actif)

                .OrderByDescending(r => r.DateEffet)

                .ThenByDescending(r => r.IdTauxChange)

                .FirstOrDefault();

            return Task.FromResult(row);

        }



        public Task<bool> ExistsForCanoniqueAndDateEffetAsync(

            string deviseBase,

            string deviseQuote,

            DateOnly dateEffet,

            long? excludeIdTauxChange = null,

            CancellationToken cancellationToken = default)

            => Task.FromResult(rows.Any(r =>

                r.DeviseSource == deviseBase.Trim().ToUpperInvariant()

                && r.DeviseCible == deviseQuote.Trim().ToUpperInvariant()

                && r.DateEffet == dateEffet

                && (excludeIdTauxChange == null || r.IdTauxChange != excludeIdTauxChange)));



        public Task<bool> EstReferenceParProcedureAsync(long idTauxChange, CancellationToken cancellationToken = default)

            => Task.FromResult(false);



        public Task<IReadOnlySet<long>> GetIdsReferenceParProcedureAsync(

            IEnumerable<long> idsTauxChange,

            CancellationToken cancellationToken = default)

            => Task.FromResult<IReadOnlySet<long>>(new HashSet<long>());



        public Task<bool> ExistsOrientationInverseAsync(

            string deviseBase,

            string deviseQuote,

            CancellationToken cancellationToken = default)

            => Task.FromResult(rows.Any(r =>

                r.DeviseSource == deviseQuote.Trim().ToUpperInvariant()

                && r.DeviseCible == deviseBase.Trim().ToUpperInvariant()));



        public Task<TauxChange> CreateVersionReplacingActifAsync(

            TauxChange newVersion,

            long userIdModification,

            CancellationToken cancellationToken = default)

        {

            foreach (var actif in rows.Where(r =>

                         r.Statut == StatutTauxChange.Actif

                         && ((r.DeviseSource == newVersion.DeviseSource && r.DeviseCible == newVersion.DeviseCible)

                             || (r.DeviseSource == newVersion.DeviseCible && r.DeviseCible == newVersion.DeviseSource))).ToList())

            {

                actif.Statut = StatutTauxChange.Inactif;

                actif.FK_UtilisateurModification = userIdModification;

            }



            newVersion.IdTauxChange = rows.Count == 0 ? 1 : rows.Max(r => r.IdTauxChange) + 1;

            rows.Add(newVersion);

            return Task.FromResult(newVersion);

        }



        public Task<TauxChange> AddAsync(TauxChange entity, CancellationToken cancellationToken = default)

        {

            entity.IdTauxChange = rows.Count == 0 ? 1 : rows.Max(r => r.IdTauxChange) + 1;

            rows.Add(entity);

            return Task.FromResult(entity);

        }



        public Task SaveChangesAsync(CancellationToken cancellationToken = default)

            => Task.CompletedTask;

    }

}

