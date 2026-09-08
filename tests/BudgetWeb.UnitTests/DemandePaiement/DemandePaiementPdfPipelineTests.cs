using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementPdfPipelineTests
{
    [Fact]
    public async Task Cas1_DpmValide_GenererPdf_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        repo.ResetPdfPipelineCounters();

        var pdf = await svc.GenererDocumentPdfAsync(created.IdDemandePaiement);

        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetDemandePaiementPdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas2_EnteteInvalide_RetourneErreurMetier_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        var demande = repo.Demandes.First(d => d.IdDemandePaiement == created.IdDemandePaiement);
        demande.Objet = "   ";

        repo.ResetPdfPipelineCounters();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenererDocumentPdfAsync(created.IdDemandePaiement));

        Assert.Contains("objet", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetDemandePaiementPdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas3_DpmInaccessible_RefuseAcces_SansLecturePdfData()
    {
        var repo = new FakeDemandePaiementRepo();
        var owner = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser { UserId = 1, Permissions = AppPermissions.ServiceDemandeur });
        var created = await owner.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        var intrus = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser { UserId = 99, Permissions = AppPermissions.ServiceDemandeur });

        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            intrus.GenererDocumentPdfAsync(created.IdDemandePaiement));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetDemandePaiementPdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas4_DpmInexistante_NotFound_SansRequetePdfData()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.ServiceDemandeur);
        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenererDocumentPdfAsync(999_999));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetDemandePaiementPdfDataAsyncCallCount);
    }

    [Fact]
    public void MapDocument_ProduitMemeDtoDepuisPdfData()
    {
        var pdfData = new DemandePaiementPdfData(
            50,
            "DP-2026-00050",
            new DateOnly(2026, 3, 15),
            "Kinshasa",
            "Objet test",
            1500.50m,
            "CDF",
            TypeBudgetCode.DepensesCourantes,
            "Item test",
            ModePaiementDpm.Caisse,
            "Section-01",
            StatutDemandePaiement.Brouillon,
            null,
            1,
            1,
            "Demandeur test",
            "Cas dossier test",
            false,
            [
                new DemandePaiementPdfBeneficiaireRow(
                    TypeBeneficiaireDemandePaiement.Agent,
                    "Jean Dupont",
                    "M123",
                    "Agent",
                    null,
                    null,
                    null,
                    null,
                    true,
                    1),
            ],
            [
                new DemandePaiementPdfValidationRow(
                    1,
                    1,
                    StatutValidationEntite.EnAttente,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
            ]);

        var fromPdf = InvokeMapDocument(pdfData);

        Assert.Equal(pdfData.IdDemandePaiement, fromPdf.IdDemandePaiement);
        Assert.Equal(pdfData.Reference, fromPdf.Reference);
        Assert.Equal(pdfData.Objet, fromPdf.Objet);
        Assert.Equal(pdfData.MontantBrut, fromPdf.MontantBrut);
        Assert.Empty(fromPdf.PiecesJustificatives);
        Assert.Single(fromPdf.Beneficiaires);
        Assert.Single(fromPdf.ValidationsEntite);
    }

    private static DemandePaiementDocumentDto InvokeMapDocument(DemandePaiementPdfData data)
    {
        var method = typeof(DemandePaiementService).GetMethod(
            "MapDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(DemandePaiementPdfData)],
            null);
        Assert.NotNull(method);
        return (DemandePaiementDocumentDto)method!.Invoke(null, [data])!;
    }
}
