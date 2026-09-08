using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Pipeline PDF pièce caisse — requêtes dédiées sans DetailQuery.</summary>
public class PieceCaissePdfPipelineTests
{
    [Fact]
    public async Task Cas1_PieceEtablie_GenererPdf_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);
        await charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest());

        repo.ResetPdfPipelineCounters();

        var pdf = await charge.GenererPieceCaissePdfAsync(created.IdDemandePaiement);

        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetPieceCaissePdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas2_PieceNonEtablie_RetourneErreurMetier_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        repo.ResetPdfPipelineCounters();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.GenererPieceCaissePdfAsync(created.IdDemandePaiement));

        Assert.Contains("pas encore été établie", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetPieceCaissePdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas3_DpmInaccessible_RefuseAcces_SansLectureInstrument()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);

        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, y1, id);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(y1, id);
        await y1.EtablirPieceCaisseAsync(id, new EtablirPieceCaisseRequest());

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y2.GenererPieceCaissePdfAsync(id));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetPieceCaissePdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas4_DpmInexistante_NotFound_SansRequeteInstrument()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);
        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            charge.GenererPieceCaissePdfAsync(999_999));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetPieceCaissePdfDataAsyncCallCount);
    }

    [Fact]
    public void MapPieceCaisseDocument_ProduitMemeDtoDepuisPdfData()
    {
        var pdfData = new PieceCaissePdfData(
            "PC-2026-00001",
            new DateOnly(2026, 3, 15),
            "DP-2026-00050",
            "Objet test",
            "Facture #1",
            "Jean Dupont",
            "M123",
            "Jean Dupont",
            1500.50m,
            "MILLE CINQ CENTS FRANCS",
            "Reçu SNEL",
            "SR-01",
            "CG-01",
            "CP-01",
            "CPA-01",
            "APP-01",
            "PC-2026-00001 · DPM-000050",
            new PdfUserDisplayName("Kabongo", "Paul", "p.kabongo"));

        var entity = new Domain.Entities.PieceCaisse
        {
            NumeroPiece = pdfData.NumeroPiece,
            DatePiece = pdfData.DatePiece,
            ReferenceDemande = pdfData.ReferenceDemande,
            Motif = pdfData.Motif,
            PieceJustificative = pdfData.PieceJustificative,
            BeneficiaireAffichage = pdfData.BeneficiaireAffichage,
            BeneficiaireMatricule = pdfData.BeneficiaireMatricule,
            BeneficiaireIdentite = pdfData.BeneficiaireIdentite,
            MontantFc = pdfData.MontantFc,
            MontantEnLettres = pdfData.MontantEnLettres,
            RecuSnel = pdfData.RecuSnel,
            Sr = pdfData.Sr,
            ComptabiliteGenerale = pdfData.ComptabiliteGenerale,
            Cp = pdfData.Cp,
            Cpa = pdfData.Cpa,
            NumeroAppariement = pdfData.NumeroAppariement,
            IdentifiantVerification = pdfData.IdentifiantVerification,
            UtilisateurEtabli = new Domain.Entities.Utilisateur
            {
                Nom = "Kabongo",
                Prenom = "Paul",
                NomUtilisateur = "p.kabongo",
            },
        };

        var fromPdf = InvokeMapPieceCaisseDocument(pdfData);
        var fromEntity = InvokeMapPieceCaisseDocument(entity);

        Assert.Equal(fromEntity with { DateImpression = fromPdf.DateImpression }, fromPdf with { DateImpression = fromPdf.DateImpression });
        Assert.Equal(fromEntity.TitreDocument, fromPdf.TitreDocument);
        Assert.Equal(fromEntity.NumeroPiece, fromPdf.NumeroPiece);
        Assert.Equal(fromEntity.MontantFc, fromPdf.MontantFc);
        Assert.Equal(fromEntity.EtabliPar, fromPdf.EtabliPar);
    }

    private static PieceCaisseDocumentDto InvokeMapPieceCaisseDocument(PieceCaissePdfData data)
    {
        var method = typeof(DemandePaiementService).GetMethod(
            "MapPieceCaisseDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(PieceCaissePdfData)],
            null);
        Assert.NotNull(method);
        return (PieceCaisseDocumentDto)method!.Invoke(null, [data])!;
    }

    private static PieceCaisseDocumentDto InvokeMapPieceCaisseDocument(Domain.Entities.PieceCaisse piece)
    {
        var method = typeof(DemandePaiementService).GetMethod(
            "MapPieceCaisseDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(Domain.Entities.PieceCaisse)],
            null);
        Assert.NotNull(method);
        return (PieceCaisseDocumentDto)method!.Invoke(null, [piece])!;
    }
}
