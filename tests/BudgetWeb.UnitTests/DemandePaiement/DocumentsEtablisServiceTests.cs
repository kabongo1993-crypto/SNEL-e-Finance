using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DocumentsEtablisServiceTests
{
    private static readonly DateOnly Debut = new(2026, 9, 1);
    private static readonly DateOnly Fin = new(2026, 9, 5);

    [Fact]
    public async Task Liste_Periode_Inclut_Billet_Et_Instrument()
    {
        var (svc, id) = await SeedCaisseEurAsync();
        var rows = await svc.ListAsync(new DocumentsEtablisQuery(Debut, Fin));

        Assert.Contains(rows, r =>
            r.IdDemandePaiement == id && r.TypeDocument == TypeDocumentEtabli.BilletConversion);
        Assert.Contains(rows, r =>
            r.IdDemandePaiement == id && r.TypeDocument == TypeDocumentEtabli.BonProvisoire);
        Assert.DoesNotContain(rows, r => r.TypeDocument == TypeDocumentEtabli.PieceCaisse);
    }

    [Fact]
    public async Task Liste_HorsPeriode_Exclut()
    {
        var (svc, _) = await SeedCaisseEurAsync();
        var rows = await svc.ListAsync(new DocumentsEtablisQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)));
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Liste_Filtre_TypeBon()
    {
        var (svc, id) = await SeedCaisseEurAsync();
        var rows = await svc.ListAsync(
            new DocumentsEtablisQuery(Debut, Fin, TypeDocumentEtabli.BonProvisoire));

        Assert.Single(rows);
        Assert.Equal(id, rows[0].IdDemandePaiement);
        Assert.Equal(TypeDocumentEtabli.BonProvisoire, rows[0].TypeDocument);
    }

    [Fact]
    public async Task Liste_SansPermissionCharge_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        var lecteur = new FakeUser { Permissions = [AppPermissions.PaiementsLire] };
        var dpm = DemandePaiementTestData.CreateService(repo);
        var svc = CreateDocs(repo, dpm, lecteur);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ListAsync(new DocumentsEtablisQuery(Debut, Fin)));
    }

    [Fact]
    public async Task Liste_PeriodeInversee_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        var dpm = DemandePaiementTestData.CreateService(repo);
        var svc = CreateDocs(repo, dpm);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ListAsync(new DocumentsEtablisQuery(Fin, Debut)));
        Assert.Contains("début", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListePdf_Passe_LesLignes_AuRenderer()
    {
        var (svc, id, renderer) = await SeedCaisseEurWithRendererAsync();
        var pdf = await svc.GenererListePdfAsync(new DocumentsEtablisQuery(Debut, Fin));

        Assert.Equal(new byte[] { 1, 2, 3 }, pdf);
        Assert.NotNull(renderer.LastRows);
        Assert.Contains(renderer.LastRows!, r => r.IdDemandePaiement == id);
    }

    [Fact]
    public async Task Archive_SansDocument_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        var dpm = DemandePaiementTestData.CreateService(repo);
        var svc = CreateDocs(repo, dpm);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenererArchivePdfAsync(new DocumentsEtablisQuery(Debut, Fin)));
    }

    [Fact]
    public async Task Liste_Selection_NeGardeQueLesClesChoisies()
    {
        var (svc, id) = await SeedCaisseEurAsync();
        var rows = await svc.ListAsync(new DocumentsEtablisQuery(
            Debut,
            Fin,
            Selection:
            [
                new DocumentEtabliSelectionDto(id, TypeDocumentEtabli.BonProvisoire, "BP-2026-1"),
            ]));

        Assert.Single(rows);
        Assert.Equal(TypeDocumentEtabli.BonProvisoire, rows[0].TypeDocument);
    }

    [Fact]
    public async Task Liste_SelectionInconnue_Vide()
    {
        var (svc, _) = await SeedCaisseEurAsync();
        var rows = await svc.ListAsync(new DocumentsEtablisQuery(
            Debut,
            Fin,
            Selection: [new DocumentEtabliSelectionDto(999_999, TypeDocumentEtabli.PieceCaisse, "X")]));

        Assert.Empty(rows);
    }

    [Fact]
    public void Selection_Parse_RefuseJetonInvalide()
    {
        Assert.Throws<ArgumentException>(() => DocumentEtabliSelectionDto.ParseList("abc|BON"));
    }

    [Fact]
    public async Task DocumentsPdf_SansDocument_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        var dpm = DemandePaiementTestData.CreateService(repo);
        var svc = CreateDocs(repo, dpm);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenererDocumentsPdfAsync(new DocumentsEtablisQuery(Debut, Fin)));
    }

    private static DocumentsEtablisService CreateDocs(
        FakeDemandePaiementRepo repo,
        IDemandePaiementService dpm,
        FakeUser? user = null,
        FakeListePdfRenderer? renderer = null)
        => new(
            repo,
            dpm,
            renderer ?? new FakeListePdfRenderer(),
            new FakePdfMerger(),
            user ?? new FakeUser(),
            NullLogger<DocumentsEtablisService>.Instance);

    private static async Task<(DocumentsEtablisService Svc, long Id)> SeedCaisseEurAsync()
    {
        var (svc, id, _) = await SeedCaisseEurWithRendererAsync();
        return (svc, id);
    }

    private static async Task<(DocumentsEtablisService Svc, long Id, FakeListePdfRenderer Renderer)> SeedCaisseEurWithRendererAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        var dpm = DemandePaiementTestData.CreateService(repo);
        var created = await dpm.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(devise: "EUR", objet: "00423"));

        repo.Billets.Add(new BilletConversion
        {
            FK_DemandePaiement = created.IdDemandePaiement,
            Statut = StatutBilletConversion.Etabli,
            DateEtabli = new DateTime(2026, 9, 5, 10, 0, 0),
            DateConversion = new DateOnly(2026, 9, 5),
            MontantCdf = 2_800_000m,
            UtilisateurEtabli = new Utilisateur { Prenom = "Charge", Nom = "DP" },
        });
        repo.BonsProvisoire.Add(new BonProvisoire
        {
            FK_DemandePaiement = created.IdDemandePaiement,
            Statut = StatutDocumentInstrumentPaiement.Etabli,
            NumeroBon = "BP-2026-1",
            DateEtabli = new DateTime(2026, 9, 5, 10, 5, 0),
            DateBon = new DateOnly(2026, 9, 5),
            MontantFc = 2_800_000m,
            BeneficiaireAffichage = "Bénéficiaire",
            UtilisateurEtabli = new Utilisateur { Prenom = "Charge", Nom = "DP" },
        });

        var renderer = new FakeListePdfRenderer();
        return (CreateDocs(repo, dpm, renderer: renderer), created.IdDemandePaiement, renderer);
    }

    private sealed class FakeListePdfRenderer : IDocumentsEtablisListePdfRenderer
    {
        public IReadOnlyList<DocumentEtabliListItemDto>? LastRows { get; private set; }

        public byte[] Render(DocumentsEtablisQuery query, IReadOnlyList<DocumentEtabliListItemDto> rows)
        {
            LastRows = rows;
            return [1, 2, 3];
        }
    }

    private sealed class FakePdfMerger : IDocumentsEtablisPdfMerger
    {
        public byte[] Merge(IReadOnlyList<byte[]> documents)
            => documents.Count == 1 ? documents[0] : documents.SelectMany(x => x).ToArray();
    }
}
