using BudgetWeb.API.Controllers.V1;
using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BudgetWeb.UnitTests.Rapports;

public class RapportPrevisionBiApiTests
{
    private sealed class StubBiService : IRapportPrevisionBiService
    {
        public Func<RapportBiQuery, Task<RapportBiDto>>? OnGet { get; set; }
        public Func<RapportBiQuery, Task<byte[]>>? OnPdf { get; set; }

        public Task<RapportBiDto> GetAsync(RapportBiQuery query, CancellationToken cancellationToken = default)
            => OnGet!(query);

        public Task<byte[]> GetPdfAsync(RapportBiQuery query, CancellationToken cancellationToken = default)
            => OnPdf!(query);
    }

    private static RapportBiDto EmptyDto()
        => new(
            new RapportBiEnTeteDto(
                "BUDGET DÉTAILLÉ DES INVESTISSEMENTS — EXERCICE 2026 EN USD",
                null, 2026, 4, 1, "V1", "ENTITE",
                80, "AC", "AC", null, null, null, null, null, null,
                0, 0, 0, "USD", "VALIDEE", DateTime.UtcNow, "RAPPORT-BI/AC/2026/V01/ENTITE"),
            RapportDcLayoutColonnes.Annuel,
            [],
            null);

    [Fact]
    public async Task Parametres_Invalides_Retourne_400()
    {
        var svc = new StubBiService
        {
            OnGet = _ => throw new ArgumentException("IdEntite est obligatoire."),
        };
        var ctrl = new RapportsPrevisionsBiController(svc);
        var result = await ctrl.Get(4, "ENTITE", null, null, null, null, null, CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task Version_Inexistante_Retourne_404()
    {
        var svc = new StubBiService
        {
            OnGet = _ => throw new InvalidOperationException("Version budgétaire introuvable."),
        };
        var ctrl = new RapportsPrevisionsBiController(svc);
        var result = await ctrl.Get(999, "ENTITE", 80, null, null, null, null, CancellationToken.None);
        var nf = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, nf.StatusCode);
    }

    [Fact]
    public async Task Ub_Inexistante_Retourne_404()
    {
        var svc = new StubBiService
        {
            OnGet = _ => throw new InvalidOperationException("Unité budgétaire introuvable."),
        };
        var ctrl = new RapportsPrevisionsBiController(svc);
        var result = await ctrl.Get(4, "UB", 80, 91, null, 999, null, CancellationToken.None);
        var nf = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, nf.StatusCode);
    }

    [Fact]
    public async Task Aucun_Resultat_Retourne_200_Vide()
    {
        var empty = EmptyDto();
        var svc = new StubBiService { OnGet = _ => Task.FromResult(empty) };
        var ctrl = new RapportsPrevisionsBiController(svc);
        var result = await ctrl.Get(4, "ENTITE", 80, null, null, null, null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var dto = Assert.IsType<RapportBiDto>(ok.Value);
        Assert.Empty(dto.Departements);
        Assert.Equal(0, dto.EnTete.MontantTotalBi);
    }

    [Fact]
    public async Task Pdf_ContentType_ApplicationPdf()
    {
        var empty = EmptyDto();
        var pdfBytes = "%PDF-1.4 stub"u8.ToArray();
        var svc = new StubBiService
        {
            OnGet = _ => Task.FromResult(empty),
            OnPdf = _ => Task.FromResult(pdfBytes),
        };
        var ctrl = new RapportsPrevisionsBiController(svc);
        var result = await ctrl.Pdf(4, "ENTITE", 80, null, null, null, null, CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(pdfBytes, file.FileContents);
    }

    [Fact]
    public void ValidateScope_Rejette_Parametres_Manquants()
    {
        Assert.Throws<ArgumentException>(() =>
            RapportPrevisionBiService.ValidateScope(
                RapportBudgetaireNiveau.Entite, new RapportBiQuery(1, "ENTITE")));
        Assert.Throws<ArgumentException>(() =>
            RapportPrevisionBiService.ValidateScope(
                RapportBudgetaireNiveau.Departement, new RapportBiQuery(1, "DEPARTEMENT", 80)));
        Assert.Throws<ArgumentException>(() =>
            RapportPrevisionBiService.ValidateScope(
                RapportBudgetaireNiveau.Ub, new RapportBiQuery(1, "UB", 80, 91)));
    }
}
