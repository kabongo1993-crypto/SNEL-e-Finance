using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class BilletConversionPdfPipelineTests
{
    [Fact]
    public async Task Cas1_BilletEtabli_GenererPdf_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "USD",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);
        await charge.EtablirBilletConversionAsync(
            created.IdDemandePaiement,
            new EtablirBilletConversionRequest());

        repo.ResetPdfPipelineCounters();

        var pdf = await charge.GenererBilletConversionPdfAsync(created.IdDemandePaiement);

        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetBilletConversionPdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas2_BilletNonEtabli_RetourneErreurMetier_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "USD",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        repo.ResetPdfPipelineCounters();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.GenererBilletConversionPdfAsync(created.IdDemandePaiement));

        Assert.Contains("pas encore été établi", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetBilletConversionPdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas3_DpmInaccessible_RefuseAcces_SansLectureInstrument()
    {
        var repo = new FakeDemandePaiementRepo();

        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, y1, id);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(y1, id);

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y2.GenererBilletConversionPdfAsync(id));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetBilletConversionPdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas4_DpmInexistante_NotFound_SansRequeteInstrument()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);
        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            charge.GenererBilletConversionPdfAsync(999_999));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetBilletConversionPdfDataAsyncCallCount);
    }

    [Fact]
    public void MapBilletDocument_ProduitMemeDtoDepuisPdfData()
    {
        var pdfData = new BilletConversionPdfData(
            50,
            "DP-2026-00050",
            new PdfBeneficiairePrincipal("ACME SARL", "Jean Dupont", true, 1),
            1000m,
            "USD",
            2290m,
            new DateOnly(2026, 3, 15),
            "CHQ-001",
            "Banque centrale",
            2_290_000m,
            500m,
            new PdfUserDisplayName("Kabongo", "Paul", "p.kabongo"),
            new PdfUserDisplayName("Mukendi", "Marie", "m.mukendi"),
            new PdfUserDisplayName("Tshilombo", "Jean", "j.tshilombo"));

        var demande = new Domain.Entities.DemandePaiement
        {
            IdDemandePaiement = 50,
            Reference = pdfData.ReferenceDemande,
            Beneficiaires =
            [
                new Domain.Entities.DemandePaiementBeneficiaire
                {
                    RaisonSociale = "ACME SARL",
                    NomComplet = "Jean Dupont",
                    EstPrincipal = true,
                    Ordre = 1,
                },
            ],
        };

        var billet = new Domain.Entities.BilletConversion
        {
            MontantDeviseOrigine = pdfData.MontantDeviseOrigine,
            DeviseOrigine = pdfData.DeviseOrigine,
            TauxApplique = pdfData.TauxApplique,
            DateConversion = pdfData.DateConversion,
            DemandeChequeNumero = pdfData.DemandeChequeNumero,
            CoursEchangeBanque = pdfData.CoursEchangeBanque,
            MontantCdf = pdfData.MontantCdf,
            SoldeAPayerDevise = pdfData.SoldeAPayerDevise,
            UtilisateurEtabli = new Domain.Entities.Utilisateur { Nom = "Kabongo", Prenom = "Paul", NomUtilisateur = "p.kabongo" },
            UtilisateurApprouve = new Domain.Entities.Utilisateur { Nom = "Mukendi", Prenom = "Marie", NomUtilisateur = "m.mukendi" },
            UtilisateurVisa = new Domain.Entities.Utilisateur { Nom = "Tshilombo", Prenom = "Jean", NomUtilisateur = "j.tshilombo" },
        };

        var fromPdf = InvokeMapBilletDocument(pdfData);
        var fromEntity = InvokeMapBilletDocument(demande, billet);

        Assert.Equal(fromEntity with { DateImpression = fromPdf.DateImpression }, fromPdf with { DateImpression = fromPdf.DateImpression });
        Assert.Equal(fromEntity.Reference, fromPdf.Reference);
        Assert.Equal(fromEntity.Beneficiaire, fromPdf.Beneficiaire);
        Assert.Equal(fromEntity.MontantDeviseOrigine, fromPdf.MontantDeviseOrigine);
    }

    private static BilletConversionDocumentDto InvokeMapBilletDocument(BilletConversionPdfData data)
    {
        var method = typeof(DemandePaiementService).GetMethod(
            "MapBilletDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(BilletConversionPdfData)],
            null);
        Assert.NotNull(method);
        return (BilletConversionDocumentDto)method!.Invoke(null, [data])!;
    }

    private static BilletConversionDocumentDto InvokeMapBilletDocument(
        Domain.Entities.DemandePaiement demande,
        Domain.Entities.BilletConversion billet)
    {
        var method = typeof(DemandePaiementService).GetMethod(
            "MapBilletDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(Domain.Entities.DemandePaiement), typeof(Domain.Entities.BilletConversion)],
            null);
        Assert.NotNull(method);
        return (BilletConversionDocumentDto)method!.Invoke(null, [demande, billet])!;
    }
}
