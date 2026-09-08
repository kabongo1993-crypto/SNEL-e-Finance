using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementControleBudgetaireTests
{
    [Fact]
    public async Task ControleDc_Credit_Mensuel_Insuffisant_Est_Ok_Avec_Depassement()
    {
        var repo = new FakeDemandePaiementRepo();
        var prevision = DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        repo.Previsions[prevision.IdPrevision] = prevision;
        SeedEngagementVisé(
            repo,
            DemandePaiementTestData.ImputationDc(montantUsd: 9_500m, mois: 3, idBudgetLigne: prevision.IdPrevision));

        var demande = BuildDemandeEnControle(
            DemandePaiementTestData.ImputationDc(montantUsd: 1_000m, mois: 3, idBudgetLigne: prevision.IdPrevision));

        var controle = await DemandePaiementControleBudgetaire.ControlerDemandeAsync(demande, repo);

        Assert.True(controle.EstValide);
        var dc = controle.Imputations.Single();
        Assert.True(dc.EstValide);
        Assert.Null(dc.MotifRejet);
        Assert.True(dc.CreditDisponibleMensuel < 0m);
        Assert.True(dc.DepassementMensuel);
    }

    [Fact]
    public async Task ControleDc_Credit_Annuel_Insuffisant_Est_Ok_Avec_Depassement()
    {
        var repo = new FakeDemandePaiementRepo();
        var prevision = DemandePaiementTestData.PrevisionDc(montantAnnuel: 5_000m);
        repo.Previsions[prevision.IdPrevision] = prevision;

        var demande = BuildDemandeEnControle(
            DemandePaiementTestData.ImputationDc(montantUsd: 6_000m, idBudgetLigne: prevision.IdPrevision));

        var controle = await DemandePaiementControleBudgetaire.ControlerDemandeAsync(demande, repo);

        Assert.True(controle.EstValide);
        Assert.True(controle.Imputations[0].EstValide);
        Assert.True(controle.Imputations[0].CreditDisponibleAnnuel < 0m);
        Assert.True(controle.Imputations[0].DepassementAnnuel);
    }

    [Fact]
    public async Task ControleDc_Encours_Agrege_Les_Lignes_De_La_Meme_Cle()
    {
        var repo = new FakeDemandePaiementRepo();
        var prevision = DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        repo.Previsions[prevision.IdPrevision] = prevision;

        var a = ToEntity(DemandePaiementTestData.ImputationDc(montantUsd: 40m, mois: 3, idBudgetLigne: prevision.IdPrevision));
        a.IdImputation = 1;
        a.Ordre = 1;
        var b = ToEntity(DemandePaiementTestData.ImputationDc(montantUsd: 60m, mois: 3, idBudgetLigne: prevision.IdPrevision));
        b.IdImputation = 2;
        b.Ordre = 2;

        var demande = BuildDemandeEnControle(
            DemandePaiementTestData.ImputationDc(montantUsd: 100m, mois: 3, idBudgetLigne: prevision.IdPrevision));
        demande.Imputations = [a, b];
        foreach (var row in demande.Imputations)
        {
            row.TypeBudget = new TypeBudget
            {
                IdTypeBudget = 1,
                CodeType = TypeBudgetCode.DepensesCourantes,
                Libelle = "DC",
            };
        }

        var controle = await DemandePaiementControleBudgetaire.ControlerDemandeAsync(demande, repo);

        Assert.True(controle.EstValide);
        Assert.Equal(2, controle.Imputations.Count);
        Assert.All(controle.Imputations, c => Assert.Equal(100m, c.EngagementEnCours));
        Assert.Equal(9_900m, controle.Imputations[0].CreditDisponibleMensuel);
    }

    [Fact]
    public async Task ControleAe_Credit_Annuel_Insuffisant_Est_Ko()
    {
        var repo = new FakeDemandePaiementRepo();
        var prevision = DemandePaiementTestData.PrevisionAe(montantAnnuel: 3_000m);
        repo.Previsions[prevision.IdPrevision] = prevision;
        SeedEngagementVisé(
            repo,
            DemandePaiementTestData.ImputationAe(montantUsd: 1_000m, idBudgetLigne: prevision.IdPrevision));

        var demande = BuildDemandeEnControle(
            DemandePaiementTestData.ImputationAe(montantUsd: 2_500m, idBudgetLigne: prevision.IdPrevision),
            montantDemandeUsd: 2_500m);

        var controle = await DemandePaiementControleBudgetaire.ControlerDemandeAsync(demande, repo);

        Assert.False(controle.EstValide);
        Assert.Equal(TypeBudgetCode.ActionsExploitation, controle.Imputations[0].CodeTypeBudget);
        Assert.Contains("annuel", controle.Imputations[0].MotifRejet ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ControleBi_Credit_Annuel_Insuffisant_Est_Ko()
    {
        var repo = new FakeDemandePaiementRepo();
        var prevision = DemandePaiementTestData.PrevisionBi(montantAnnuel: 4_000m);
        repo.Previsions[prevision.IdPrevision] = prevision;
        SeedEngagementVisé(
            repo,
            DemandePaiementTestData.ImputationBi(montantUsd: 1_500m, idBudgetLigne: prevision.IdPrevision));

        var demande = BuildDemandeEnControle(
            DemandePaiementTestData.ImputationBi(montantUsd: 3_000m, idBudgetLigne: prevision.IdPrevision),
            montantDemandeUsd: 3_000m);

        var controle = await DemandePaiementControleBudgetaire.ControlerDemandeAsync(demande, repo);

        Assert.False(controle.EstValide);
        Assert.Equal(TypeBudgetCode.BudgetInvestissement, controle.Imputations[0].CodeTypeBudget);
        Assert.Contains("annuel", controle.Imputations[0].MotifRejet ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ControleDc_Credit_Suffisant_Est_Ok()
    {
        var repo = new FakeDemandePaiementRepo();
        var prevision = DemandePaiementTestData.PrevisionDc(montantAnnuel: 120_000m);
        repo.Previsions[prevision.IdPrevision] = prevision;

        var demande = BuildDemandeEnControle(
            DemandePaiementTestData.ImputationDc(montantUsd: 100m, idBudgetLigne: prevision.IdPrevision));

        var controle = await DemandePaiementControleBudgetaire.ControlerDemandeAsync(demande, repo);

        Assert.True(controle.EstValide);
        Assert.Null(controle.MotifRejet);
    }

    [Fact]
    public async Task SumEngageDc_Retourne_Zero_Sans_Demandes_Visees()
    {
        var repo = new FakeDemandePaiementRepo();
        var demande = BuildDemandeEnControle(
            DemandePaiementTestData.ImputationDc(montantUsd: 100m, idBudgetLigne: 100));

        var mensuel = await repo.SumEngageDcMensuelAsync(1, 10, 100, 3, demande.IdDemandePaiement);
        var annuel = await repo.SumEngageDcAnnuelAsync(1, 10, 100, demande.IdDemandePaiement);

        Assert.Equal(0m, mensuel);
        Assert.Equal(0m, annuel);
    }

    [Fact]
    public async Task SumEngageDc_Compte_Uniquement_Demandes_Visees()
    {
        var repo = new FakeDemandePaiementRepo();
        SeedEngagementVisé(
            repo,
            DemandePaiementTestData.ImputationDc(montantUsd: 250m, mois: 3, idBudgetLigne: 100));

        var engage = await repo.SumEngageDcMensuelAsync(1, 10, 100, 3, excludeDemandeId: null);
        Assert.Equal(250m, engage);
    }

    [Fact]
    public async Task ControleImputation_Sans_Prevision_Avec_Montant_Nul_Est_Ok()
    {
        var repo = new FakeDemandePaiementRepo();
        var imputation = ToEntity(
            DemandePaiementTestData.ImputationDc(montantUsd: 0m, idBudgetLigne: null));
        imputation.MontantUsd = 0m;

        var controle = await DemandePaiementControleBudgetaire.ControleImputationAsync(
            imputation,
            TypeBudgetCode.DepensesCourantes,
            prevision: null,
            excludeDemandeId: 1,
            repo);

        Assert.True(controle.EstValide);
        Assert.Equal(0m, controle.BudgetAnnuel);
    }

    private static DemandePaiementEntity BuildDemandeEnControle(
        CreateImputationRequest imputationRequest,
        decimal montantDemandeUsd = 100m)
    {
        var imputation = ToEntity(imputationRequest);
        imputation.TypeBudget = new TypeBudget
        {
            IdTypeBudget = imputation.FK_TypeBudget,
            CodeType = imputation.FK_TypeBudget switch
            {
                2 => TypeBudgetCode.ActionsExploitation,
                3 => TypeBudgetCode.BudgetInvestissement,
                _ => TypeBudgetCode.DepensesCourantes,
            },
            Libelle = "Type",
        };

        var demande = DemandePaiementFactory.CreerBrouillon(
            "DP-2026-00099",
            new DateOnly(2026, 3, 1),
            1,
            10,
            5,
            1,
            "Contrôle budgétaire",
            montantDemandeUsd,
            "USD",
            TypeBudgetCode.DepensesCourantes,
            null,
            ModePaiementDpm.Caisse,
            1,
            DateTime.UtcNow);

        demande.IdDemandePaiement = 99;
        demande.FK_TypeBudget = imputation.FK_TypeBudget;
        demande.MontantUsd = montantDemandeUsd;
        demande.TauxConversion = 1m;
        demande.ModePaiementSollicite = ModePaiementSollicite.Caisse;
        demande.Statut = StatutDemandePaiement.EnControleBudgetaire;
        demande.FK_VersionBudgetaire = 4;
        demande.Imputations = [imputation];
        return demande;
    }

    private static void SeedEngagementVisé(FakeDemandePaiementRepo repo, CreateImputationRequest request)
    {
        var imputation = ToEntity(request);
        imputation.TypeBudget = new TypeBudget
        {
            IdTypeBudget = imputation.FK_TypeBudget,
            CodeType = imputation.FK_TypeBudget switch
            {
                2 => TypeBudgetCode.ActionsExploitation,
                3 => TypeBudgetCode.BudgetInvestissement,
                _ => TypeBudgetCode.DepensesCourantes,
            },
            Libelle = "Type",
        };

        var demande = DemandePaiementFactory.CreerBrouillon(
            $"DP-2026-V{repo.Demandes.Count + 1:D5}",
            new DateOnly(2026, 3, 1),
            1,
            10,
            5,
            1,
            "Engagement visé",
            imputation.MontantUsd,
            "USD",
            TypeBudgetCode.DepensesCourantes,
            null,
            ModePaiementDpm.Caisse,
            1,
            DateTime.UtcNow);

        demande.IdDemandePaiement = repo.Demandes.Count + 100;
        demande.FK_TypeBudget = imputation.FK_TypeBudget;
        demande.MontantUsd = imputation.MontantUsd;
        demande.TauxConversion = 1m;
        demande.ModePaiementSollicite = ModePaiementSollicite.Caisse;
        demande.Statut = StatutDemandePaiement.ViseeBudgetairement;
        demande.Imputations = [imputation];
        imputation.FK_DemandePaiement = demande.IdDemandePaiement;
        repo.Demandes.Add(demande);
    }

    private static DemandePaiementImputation ToEntity(CreateImputationRequest request)
        => new()
        {
            IdImputation = request.Ordre,
            FK_DemandePaiement = 99,
            Ordre = request.Ordre,
            FK_TypeBudget = request.IdTypeBudget,
            FK_UniteBudgetaire = request.IdUB,
            FK_ExerciceBudgetaire = request.IdExercice,
            FK_RubriqueBudgetaire = request.IdRubriqueBudgetaire,
            Mois = request.Mois,
            LibelleItemAE = request.LibelleItemAE,
            FK_GroupeItemAE = request.IdGroupeItemAE,
            FK_ItemBI = request.IdItemBI,
            DetailBI = request.DetailBI,
            FK_BudgetLigne = request.IdBudgetLigne,
            MontantBrut = request.MontantBrut,
            Devise = request.Devise,
            TauxConversion = 1m,
            MontantUsd = request.MontantBrut,
            FK_UtilisateurCreation = 1,
            DateImputation = DateTime.UtcNow,
        };
}
