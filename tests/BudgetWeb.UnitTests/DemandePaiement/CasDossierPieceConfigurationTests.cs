using System.Text;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public sealed class CasDossierPieceConfigurationTests
{
    [Fact]
    public async Task PieceActiveObligatoire_Absente_EstManquante()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var manquantes = await svc.GetPiecesManquantesAsync(created.IdDemandePaiement);

        Assert.Single(manquantes);
        Assert.Equal("Facture fournisseur", manquantes[0].Libelle);
    }

    [Fact]
    public async Task PieceActiveFacultative_Absente_NestPasManquante()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceFacultative();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);

        var manquantes = await svc.GetPiecesManquantesAsync(created.IdDemandePaiement);
        Assert.Empty(manquantes);
    }

    [Fact]
    public async Task PieceDesactivee_NestPasAttenduePourNouveauDossier()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceDesactivee();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var actives = await repo.GetPiecesActivesAsync(created.IdCasDossier);

        Assert.Single(actives);
        Assert.Equal("FACTURE", actives[0].CodeTypePiece);
    }

    [Fact]
    public async Task PieceDesactivee_NePeutPasEtreAjoutee()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceDesactivee();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 sample");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddPieceAsync(
                created.IdDemandePaiement,
                new UploadDemandePaiementPieceMetadata(3, "ANCIEN", "Ancien document"),
                new MemoryStream(bytes),
                "ancien.pdf",
                "application/pdf"));

        Assert.Contains("n'est pas active", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DesactiverPieceObligatoire_ForceFacultative()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var casRepo = new FakeCasDossierRepo(repo);
        var svc = new CasDossierService(casRepo, new FakeUser { Permissions = ["referentiels.ecrire"] });

        var updated = await svc.UpdatePieceAsync(
            1,
            1,
            new UpdateCasDossierPieceRequest("Libellé", 1, Actif: false, Obligatoire: true));

        Assert.NotNull(updated);
        Assert.False(updated!.Actif);
        Assert.False(updated.Obligatoire);
    }

    [Fact]
    public async Task ModificationConfiguration_NeSupprimePasPiecesJointes()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);

        repo.PiecesObligatoires[0].Libelle = "Libellé modifié admin";

        var pieces = await svc.GetPiecesAsync(created.IdDemandePaiement);
        Assert.Single(pieces);
        Assert.Equal("Facture fournisseur", pieces[0].Libelle);
    }

    [Fact]
    public async Task Backend_IgnoreEstObligatoireClient_Facultative()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceFacultative();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 sample");

        var piece = await svc.AddPieceAsync(
            created.IdDemandePaiement,
            new UploadDemandePaiementPieceMetadata(2, "RAPPORT", "Rapport de mission"),
            new MemoryStream(bytes),
            "rapport.pdf",
            "application/pdf");

        Assert.False(piece.EstObligatoire);
    }

    [Fact]
    public async Task MigrationCompatibilite_ToutesPiecesActivesEtObligatoires()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);

        var manquantes = await svc.GetPiecesManquantesAsync(created.IdDemandePaiement);
        Assert.Empty(manquantes);

        var pieces = await svc.GetPiecesAsync(created.IdDemandePaiement);
        Assert.Single(pieces);
        Assert.True(pieces[0].EstObligatoire);

        Assert.All(repo.PiecesObligatoires, p =>
        {
            Assert.True(p.Actif);
            Assert.True(p.Obligatoire);
        });
    }
}
