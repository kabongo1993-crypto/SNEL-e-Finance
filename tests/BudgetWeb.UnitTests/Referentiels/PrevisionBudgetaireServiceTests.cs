using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class PrevisionBudgetaireServiceTests
{
    [Fact]
    public async Task Create_DC_RefuseSansUB()
    {
        var repo = FakeRepo.Create();
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ReqDc(idUB: 0)));
        Assert.Contains("unité budgétaire", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_DC_RefuseSansRB()
    {
        var repo = FakeRepo.Create();
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ReqDc(idRB: null)));
        Assert.Contains("rubrique", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_DC_RefuseMontantNegatif()
    {
        var repo = FakeRepo.Create();
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ReqDc(montant: -10m)));
        Assert.Contains("négatif", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_DC_Annuel_Ok()
    {
        var repo = FakeRepo.Create();
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var created = await service.CreateAsync(ReqDc(montant: 1500m));
        Assert.Equal(1500m, created.MontantAnnuel);
        Assert.Equal(TypeBudgetCode.DepensesCourantes, created.CodeType);
        Assert.Empty(created.Repartitions);
        Assert.Equal(1, repo.CreateCount);
    }

    [Fact]
    public async Task Create_DC_Mensuel_SansRepartition_ConserveMontantAnnuel()
    {
        var repo = FakeRepo.Create(mode: ModePrevisionCode.Mensuel);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var created = await service.CreateAsync(ReqDc(
            montant: 12000m,
            mode: ModePrevisionCode.Mensuel,
            reps: []));

        Assert.Equal(12000m, created.MontantAnnuel);
        Assert.Empty(created.Repartitions);
        Assert.Equal(1, repo.CreateCount);
    }

    [Fact]
    public async Task Create_DC_Mensuel_CalculeSomme()
    {
        var repo = FakeRepo.Create(mode: ModePrevisionCode.Mensuel);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var created = await service.CreateAsync(ReqDc(
            montant: 0,
            mode: ModePrevisionCode.Mensuel,
            reps:
            [
                new RepartitionMensuelleDto(1, 100),
                new RepartitionMensuelleDto(2, 250),
            ]));
        Assert.Equal(350m, created.MontantAnnuel);
        Assert.Equal(2, created.Repartitions.Count);
    }

    [Fact]
    public async Task Create_RefuseMoisInvalide()
    {
        var repo = FakeRepo.Create(mode: ModePrevisionCode.Mensuel);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var ex0 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ReqDc(mode: ModePrevisionCode.Mensuel, reps: [new RepartitionMensuelleDto(0, 10)])));
        Assert.Contains("1 et 12", ex0.Message);

        var ex13 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ReqDc(mode: ModePrevisionCode.Mensuel, reps: [new RepartitionMensuelleDto(13, 10)])));
        Assert.Contains("1 et 12", ex13.Message);
    }

    [Fact]
    public async Task Create_RefuseMoisDuplique()
    {
        var repo = FakeRepo.Create(mode: ModePrevisionCode.Mensuel);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ReqDc(mode: ModePrevisionCode.Mensuel, reps:
            [
                new RepartitionMensuelleDto(1, 10),
                new RepartitionMensuelleDto(1, 20),
            ])));
        Assert.Contains("dupliqué", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_AE_RefuseSansAction()
    {
        var repo = FakeRepo.Create(type: TypeBudgetCode.ActionsExploitation);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ReqAe(libelleItemAE: "")));
        Assert.Contains("action d'exploitation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_AE_GroupeFacultatif_Ok()
    {
        var repo = FakeRepo.Create(type: TypeBudgetCode.ActionsExploitation);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var created = await service.CreateAsync(ReqAe(idGroupe: null, libelleItemAE: "Organisation de formations"));
        Assert.Equal("Organisation de formations", created.LibelleItemAE);
        Assert.Null(created.IdGroupeItemAE);
    }

    [Fact]
    public async Task Create_AE_DeuxActionsMemeRB_Ok()
    {
        var repo = FakeRepo.Create(type: TypeBudgetCode.ActionsExploitation);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        await service.CreateAsync(ReqAe(libelleItemAE: "Formation A"));
        await service.CreateAsync(ReqAe(libelleItemAE: "Formation B"));
        Assert.Equal(2, repo.CreateCount);
    }

    [Fact]
    public async Task Create_BI_RefuseSansDetail()
    {
        var repo = FakeRepo.Create(type: TypeBudgetCode.BudgetInvestissement);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ReqBi(detail: "")));
        Assert.Contains("détail", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_BI_RefuseSansItem()
    {
        var repo = FakeRepo.Create(type: TypeBudgetCode.BudgetInvestissement);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ReqBi(idItemBI: null)));
        Assert.Contains("item BI", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_BI_PlusieursDetails_Ok()
    {
        var repo = FakeRepo.Create(type: TypeBudgetCode.BudgetInvestissement);
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        await service.CreateAsync(ReqBi(detail: "Ordinateur des statistiques"));
        await service.CreateAsync(ReqBi(detail: "Imprimante de bureautique"));
        Assert.Equal(2, repo.CreateCount);
    }

    [Fact]
    public async Task Create_RefuseUtilisateurInexistant()
    {
        var repo = FakeRepo.Create();
        repo.UtilisateurExiste = false;
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(ReqDc()));
        Assert.Contains("UTILISATEUR", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefuseUbSoumise_MemeSiVersionAgregee()
    {
        // VERSION peut être SOUMISE (agrégat) ; seul le statut UB compte pour verrouiller.
        var repo = FakeRepo.Create();
        repo.StatutVersion = StatutVersionBudgetaire.Soumise;
        var service = new PrevisionBudgetaireService(
            repo, new FixedStatutWorkflowUbService(StatutVersionBudgetaire.Soumise), new NoopClassementAeService(),
            new NoopPerimetreAccesService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(ReqDc()));
        Assert.Contains("unité budgétaire", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_AutoriseUbBrouillon_MemeSiVersionSoumise()
    {
        var repo = FakeRepo.Create();
        repo.StatutVersion = StatutVersionBudgetaire.Soumise;
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService()); // BROUILLON

        var created = await service.CreateAsync(ReqDc(montant: 100m));
        Assert.Equal(100m, created.MontantAnnuel);
    }

    [Fact]
    public async Task GetGrille_DC_GroupeNiveau1_SansSectionsTechniques()
    {
        var repo = FakeRepo.Create();
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var grille = await service.GetGrilleAsync(1, 1, 1, 10, null, null, null);
        Assert.True(grille.ModificationAutorisee);
        Assert.Equal(3, grille.Lignes.Count);

        // Pas de section technique 0010
        Assert.DoesNotContain(grille.Lignes, l => l.CodeRB == "0010");

        var groupe = Assert.Single(grille.Lignes, l => l.EstSection);
        Assert.Null(groupe.IdRB);
        Assert.Equal(1, groupe.IdGroupeRB);
        Assert.Equal("00", groupe.CodeRB);
        Assert.Equal("ACHAT ET VARIATIONS DE STOCKS", groupe.LibelleRB);

        Assert.Contains(grille.Lignes, l => l.CodeRB == "00100" && !l.EstSection && l.IdGroupeRB == 1);
        Assert.Contains(grille.Lignes, l => l.CodeRB == "00110" && !l.EstSection && l.IdGroupeRB == 1);
    }

    [Fact]
    public async Task GetGrille_DC_SepareDeuxGroupesMemeCode02()
    {
        var repo = FakeRepo.CreateWithDeuxGroupes02();
        var service = new PrevisionBudgetaireService(
            repo, new NoopWorkflowUbService(), new NoopClassementAeService(), new NoopPerimetreAccesService());

        var grille = await service.GetGrilleAsync(1, 1, 1, 10, null, null, null);
        var groupes = grille.Lignes.Where(l => l.EstSection).ToList();
        Assert.Equal(2, groupes.Count);
        Assert.All(groupes, g => Assert.Equal("02", g.CodeRB));
        Assert.Contains(groupes, g => g.IdGroupeRB == 2 && g.LibelleRB!.Contains("A"));
        Assert.Contains(groupes, g => g.IdGroupeRB == 3 && g.LibelleRB!.Contains("B"));
        Assert.DoesNotContain(grille.Lignes, l => l.CodeRB == "0230" || l.CodeRB == "0330");
    }

    private static CreatePrevisionBudgetaireRequest ReqDc(
        long idUB = 10,
        long? idRB = 100,
        decimal montant = 100,
        string mode = ModePrevisionCode.Annuel,
        IReadOnlyList<RepartitionMensuelleDto>? reps = null)
        => new(1, 1, mode == ModePrevisionCode.Mensuel ? 2 : 1, idUB, idRB, null, null, null, null, montant, reps, 1);

    private static CreatePrevisionBudgetaireRequest ReqAe(
        string? libelleItemAE = "Action",
        long? idGroupe = null)
        => new(1, 2, 1, 10, 100, null, idGroupe, libelleItemAE, null, 100m, null, 1);

    private static CreatePrevisionBudgetaireRequest ReqBi(
        long? idItemBI = 50,
        string? detail = "Détail")
        => new(1, 3, 1, 10, null, idItemBI, null, null, detail, 100m, null, 1);

    private sealed class FakeRepo : IPrevisionBudgetaireRepository
    {
        public string StatutVersion { get; set; } = StatutVersionBudgetaire.Brouillon;
        public bool UtilisateurExiste { get; set; } = true;
        public int CreateCount { get; private set; }
        public string TypeCode { get; init; } = TypeBudgetCode.DepensesCourantes;
        public string ModeCode { get; init; } = ModePrevisionCode.Annuel;

        public static FakeRepo Create(string type = TypeBudgetCode.DepensesCourantes, string mode = ModePrevisionCode.Annuel)
            => new() { TypeCode = type, ModeCode = mode };

        public static FakeRepo CreateWithDeuxGroupes02()
            => new() { RubriquesMode = RubriquesFixture.DeuxGroupes02 };

        private enum RubriquesFixture { Standard, DeuxGroupes02 }
        private RubriquesFixture RubriquesMode { get; init; } = RubriquesFixture.Standard;

        public Task<IReadOnlyList<PrevisionBudgetaireDto>> GetByFiltresAsync(
            long? idVersion, long? idTypeBudget, long? idUB, long? idModePrevision, string? libelleItemAE, long? idItemBI,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PrevisionBudgetaireDto>>([]);

        public Task<IReadOnlyList<PrevisionGrilleSourceDto>> GetForGrilleAsync(
            long idVersion,
            long idTypeBudget,
            long idUB,
            string? libelleItemAE,
            long? idItemBI,
            bool includeRepartitions,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PrevisionGrilleSourceDto>>([]);

        public Task<PrevisionBudgetaireDto?> GetByIdAsync(long idPrevision, CancellationToken cancellationToken = default)
            => Task.FromResult<PrevisionBudgetaireDto?>(null);

        public Task<(string Statut, bool Exists)> GetVersionStatutAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult((StatutVersion, idVersion > 0));

        public Task<(string CodeType, string Libelle, bool Exists, bool Actif)> GetTypeBudgetAsync(
            long idTypeBudget, CancellationToken cancellationToken = default)
        {
            var code = idTypeBudget switch
            {
                2 => TypeBudgetCode.ActionsExploitation,
                3 => TypeBudgetCode.BudgetInvestissement,
                _ => TypeBudgetCode.DepensesCourantes
            };
            return Task.FromResult((code, code, idTypeBudget > 0, true));
        }

        public Task<(string CodeMode, string Libelle, bool Exists, bool Actif)> GetModePrevisionAsync(
            long idModePrevision, CancellationToken cancellationToken = default)
        {
            var code = idModePrevision == 2 ? ModePrevisionCode.Mensuel : ModePrevisionCode.Annuel;
            return Task.FromResult((code, code, idModePrevision > 0, true));
        }

        public Task<bool> ExistsUBAsync(long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult(idUB > 0);

        public Task<bool> ExistsRBAsync(long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult(idRB > 0);

        public Task<bool> ExistsItemBIAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult(idItemBI > 0);

        public Task<bool> ExistsGroupeItemAEAsync(long idGroupeItemAE, CancellationToken cancellationToken = default)
            => Task.FromResult(idGroupeItemAE > 0);

        public Task<bool> ExistsUtilisateurAsync(long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult(UtilisateurExiste && idUtilisateur > 0);

        public Task<(string CodeUB, string LibelleUB)?> GetUBInfoAsync(long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult<(string, string)?>(($"UB{idUB}", "UB Test"));

        public Task<(string? CodeItem, string? LibelleItem)?> GetItemBIInfoAsync(long idItemBI, CancellationToken cancellationToken = default)
            => Task.FromResult<(string?, string?)?>(("ITEM 1", "Item test"));

        public Task<long?> FindIdDcAsync(long idVersion, long idUB, long idRB, CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(null);

        public Task<long?> FindIdAeAsync(long idVersion, long idUB, long idRB, string libelleItemAE, CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(null);

        public Task<long?> FindIdBiAsync(long idVersion, long idUB, long idItemBI, string detailBI, CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(null);

        public Task<IReadOnlyList<(
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
            if (RubriquesMode == RubriquesFixture.DeuxGroupes02)
            {
                return Task.FromResult<IReadOnlyList<(long, string, string, long?, int, bool, long?, string?, string?, int?)>>([
                    (230, "0230", "Services A section", null, 0, true, null, null, null, null),
                    (231, "02300", "Services A feuille", 230, 1, true, 2, "02", "SERVICES EXTERIEURS A", 3),
                    (330, "0330", "Services B section", null, 0, true, null, null, null, null),
                    (331, "03316", "Services B feuille", 330, 1, true, 3, "02", "SERVICES EXTERIEURS B", 4),
                ]);
            }

            return Task.FromResult<IReadOnlyList<(long, string, string, long?, int, bool, long?, string?, string?, int?)>>([
                (10, "0010", "Achats énergie", null, 0, true, null, null, null, null),
                (100, "00100", "Achats énergie électrique", 10, 1, true, 1, "00", "ACHAT ET VARIATIONS DE STOCKS", 1),
                (110, "00110", "Achats Matières combustibles", 10, 1, true, 1, "00", "ACHAT ET VARIATIONS DE STOCKS", 1),
            ]);
        }
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public Task<PrevisionBudgetaireDto> CreateAsync(
            long idVersion, long idTypeBudget, long idModePrevision, long idUB, long? idRB, long? idItemBI,
            long? idGroupeItemAE, string? libelleItemAE, string? detailBI, decimal montantAnnuel,
            long idUtilisateurCreation, IReadOnlyList<RepartitionMensuelleDto> repartitions,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            var (codeType, _, _, _) = GetTypeBudgetAsync(idTypeBudget).Result;
            var (codeMode, _, _, _) = GetModePrevisionAsync(idModePrevision).Result;
            return Task.FromResult(new PrevisionBudgetaireDto(
                CreateCount, idVersion, 1, "V1", StatutVersion, 2026, idTypeBudget, codeType, codeType,
                idModePrevision, codeMode, codeMode, idUB, "UB", "UB",
                idRB, idRB?.ToString(), null, idItemBI, null, null, idGroupeItemAE, null,
                libelleItemAE, detailBI, montantAnnuel, repartitions, DateTime.Now, idUtilisateurCreation, null, null));
        }

        public Task<PrevisionBudgetaireDto?> UpdateAsync(
            long idPrevision, long? idGroupeItemAE, string? libelleItemAE, string? detailBI, decimal montantAnnuel,
            long idUtilisateurModification, IReadOnlyList<RepartitionMensuelleDto>? repartitions, bool remplacerRepartitions,
            CancellationToken cancellationToken = default)
            => Task.FromResult<PrevisionBudgetaireDto?>(null);

        public Task<bool> DeleteAsync(long idPrevision, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task RemplacerRepartitionsAsync(
            long idPrevision, IReadOnlyList<RepartitionMensuelleDto> repartitions, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PrevisionResumeCategorieDto>> GetResumeParTypeAsync(
            long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PrevisionResumeCategorieDto>>([]);

        public Task<IReadOnlyList<string>> ListLibellesItemAEAsync(
            long? idVersion, long? idExercice, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class NoopVersionService : IVersionBudgetaireService
    {
        public Task<IReadOnlyList<VersionBudgetaireDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VersionBudgetaireDto>>([]);

        public Task<VersionBudgetaireDto?> GetByIdAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult<VersionBudgetaireDto?>(null);

        public Task<IReadOnlyList<UtilisateurLookupDto>> GetUtilisateursAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UtilisateurLookupDto>>([]);

        public Task<VersionBudgetaireDto> CreateAsync(CreateVersionBudgetaireRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VersionBudgetaireDto?> UpdateAsync(long idVersion, UpdateVersionBudgetaireRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<VersionBudgetaireDto?>(null);

        public Task<bool> DeleteAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<VersionBudgetaireDto> SoumettreAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VersionBudgetaireDto> ControlerAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VersionBudgetaireDto> ValiderAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VersionBudgetaireDto> RejeterAsync(long idVersion, long idUtilisateur, string motif, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VersionBudgetaireDto> ReouvrirAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReouvrirSiRejeteeApresSaisieAsync(long idVersion, long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FixedStatutWorkflowUbService(string statut) : NoopWorkflowUbService
    {
        public override Task<string> GetStatutOperationnelAsync(
            long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult(statut);
    }

    private class NoopClassementAeService : IClassementAeService
    {
        public Task<IReadOnlyList<ClassementAeLigneDto>> GetAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ClassementAeLigneDto>>([]);

        public Task ReorderAsync(ReorderClassementAeRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ClassementAeInitResultDto> InitialiserManquantsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ClassementAeInitResultDto(0, 0, 0));

        public Task GarantirGroupeHomogeneAsync(
            long idVersion, long idUB, string libelleItemAE, long? idGroupePropose, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ApresEcritureAeAsync(
            long idVersion, long idUB, string libelleItemAE, long? idGroupeItemAE, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ApresSuppressionAeAsync(
            long idVersion, long idUB, string? libelleItemAE, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ApresRenommageAeAsync(
            long idVersion, long idUB, string ancienLibelle, string nouveauLibelle, long? idGroupeItemAE, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ApresChangementGroupeAeAsync(
            long idVersion, long idUB, string libelleItemAE, long? nouveauGroupe, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class NoopWorkflowUbService : IWorkflowPrevisionUbService
    {
        public Task EnsureExistsAsync(long idVersion, long idUB, long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public virtual Task<string> GetStatutOperationnelAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult(StatutVersionBudgetaire.Brouillon);

        public Task ReouvrirSiRejeteeApresSaisieAsync(long idVersion, long idUB, long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecalculerStatutVersionAsync(long idVersion, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<WorkflowPrevisionUbDto> SoumettreAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowPrevisionUbDto> ControlerAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowPrevisionUbDto> ValiderAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowPrevisionUbDto> RejeterAsync(long idVersion, long idUB, string motif, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDepartementBulkResultDto> SoumettreDepartementAsync(
            long idVersion, long idDepartement, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDepartementBulkResultDto> ControlerDepartementAsync(
            long idVersion, long idDepartement, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDepartementBulkResultDto> ValiderDepartementAsync(
            long idVersion, long idDepartement, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDepartementBulkResultDto> RejeterDepartementAsync(
            long idVersion, long idDepartement, string motif, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowPrevisionUbDto> ReouvrirAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowPrevisionUbDto> AnnulerSoumissionAsync(long idVersion, long idUB, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
