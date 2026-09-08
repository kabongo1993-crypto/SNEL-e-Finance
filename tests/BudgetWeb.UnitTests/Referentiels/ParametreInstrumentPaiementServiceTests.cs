using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Security;
using BudgetWeb.UnitTests.DemandePaiement;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class ParametreInstrumentPaiementServiceTests
{
    [Fact]
    public async Task UpsertAsync_PieceCaisse_Complet_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        var service = CreateService(repo);

        var saved = await service.UpsertAsync(
            TypeInstrumentPaiement.PieceCaisse,
            new UpsertParametreInstrumentRequest(
                Sr: "SR-01",
                ComptabiliteGenerale: "CG-01",
                Cp: "CP-01",
                Cpa: "CPA-01",
                CompteGeneral: null,
                CompteParticulier: null,
                CpCa: null,
                Ls: null,
                SuiviExtraComptable: null,
                MontantSuiviExtraComptable: null,
                NumeroAppariement: "APP-01",
                RecuInstitutionnel: "RECU-01",
                Actif: true));

        Assert.True(saved.EstConfigure);
        Assert.Equal("SR-01", saved.Sr);
    }

    [Fact]
    public async Task UpsertAsync_PieceCaisse_Incomplet_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpsertAsync(
                TypeInstrumentPaiement.PieceCaisse,
                new UpsertParametreInstrumentRequest(
                    Sr: "SR-01",
                    ComptabiliteGenerale: null,
                    Cp: "CP-01",
                    Cpa: "CPA-01",
                    CompteGeneral: null,
                    CompteParticulier: null,
                    CpCa: null,
                    Ls: null,
                    SuiviExtraComptable: null,
                    MontantSuiviExtraComptable: null,
                    NumeroAppariement: "APP-01",
                    RecuInstitutionnel: "RECU-01",
                    Actif: true)));

        Assert.Contains("pièce de caisse", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListAsync_RetourneTroisTypes()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);
        var service = CreateService(repo);

        var rows = await service.ListAsync();
        Assert.Equal(3, rows.Count);
        Assert.True(rows.Single(r => r.TypeInstrument == TypeInstrumentPaiement.PieceCaisse).EstConfigure);
        Assert.False(rows.Single(r => r.TypeInstrument == TypeInstrumentPaiement.MinuteCheque).EstConfigure);
    }

    private static ParametreInstrumentPaiementService CreateService(FakeDemandePaiementRepo repo)
        => new(repo, new ReferentielFakeUser([AppPermissions.ReferentielsEcrire]));

    private sealed class ReferentielFakeUser(IReadOnlyList<string> permissions) : ICurrentUserService
    {
        public long? UserId => 1;
        public string? Username => "test";
        public string? DisplayName => "Test";
        public IReadOnlyList<string> Roles { get; } = [];
        public IReadOnlyList<string> Permissions { get; } = permissions;
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission) =>
            Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));
        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException();
    }
}
