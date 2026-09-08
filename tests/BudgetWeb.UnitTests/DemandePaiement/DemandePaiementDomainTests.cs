using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementDomainTests
{
    private static readonly DateTime NowUtc = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreerBrouillon_CreeUneDemandeEnStatutBrouillon()
    {
        var demande = DemandePaiementFactory.CreerBrouillon(
            reference: "DP-2026-00001",
            dateEmission: new DateOnly(2026, 3, 1),
            fkExerciceBudgetaire: 1,
            fkUniteBudgetaire: 10,
            fkDemandeur: 5,
            fkCasDossier: 1,
            objet: "Frais de mission",
            montantSollicite: 229_000m,
            deviseSollicitee: "CDF",
            typeBudgetSollicite: TypeBudgetCode.DepensesCourantes,
            itemSollicite: null,
            modePaiementSollicite: ModePaiementDpm.Caisse,
            fkUtilisateurCreation: 1,
            dateCreationUtc: NowUtc);

        Assert.Equal(StatutDemandePaiement.Brouillon, demande.Statut);
        Assert.Equal("DP-2026-00001", demande.Reference);
        Assert.Equal(5, demande.FK_Demandeur);
        Assert.Equal(10, demande.FK_UniteBudgetaire);
        Assert.Equal(229_000m, demande.MontantBrut);
        Assert.Equal("CDF", demande.Devise);
        Assert.Null(demande.MontantUsd);
        Assert.Null(demande.TauxConversion);
        Assert.Equal(ModePaiementDpm.Caisse, demande.ModePaiementSollicite);
        Assert.Equal(TypeBudgetCode.DepensesCourantes, demande.TypeBudgetSollicite);
        Assert.Null(demande.ItemSollicite);
        Assert.Null(demande.FK_TypeBudget);
    }

    [Fact]
    public void CreerBrouillon_Refuse_UbIncoherente()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DemandePaiementFactory.CreerBrouillon(
                "DP-2026-00099",
                new DateOnly(2026, 3, 1),
                1, 10, 5, 1,
                "Test",
                100m, "USD",
                TypeBudgetCode.DepensesCourantes,
                null,
                ModePaiementDpm.Caisse,
                1, NowUtc,
                fkUbProposeeParClient: 99));

        Assert.Contains("demandeur", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DemandeurRules_CodeUnique_Normalise()
    {
        Assert.Equal("DDK/DKC/DG", DemandeurRules.NormaliserCode(" ddk/dkc/dg "));
        Assert.Throws<ArgumentException>(() => DemandeurRules.NormaliserCode(" "));
    }

    [Fact]
    public void DemandeurRules_Inactif_Refuse()
    {
        Assert.Throws<InvalidOperationException>(() => DemandeurRules.ExigerActif(false));
    }

    [Fact]
    public void VerifierStatutInitial_RefuseSoumise()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DemandePaiementFactory.VerifierStatutInitial(StatutDemandePaiement.Soumise));

        Assert.Contains("BROUILLON", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImputationDc_Valide()
    {
        var imputation = ImputationDc(mois: 3);

        var exception = Record.Exception(() => DemandePaiementImputationRules.ValiderStructure(imputation));

        Assert.Null(exception);
        Assert.Equal(FormeImputationBudgetaire.DepensesCourantes, DemandePaiementImputationRules.DetecterForme(imputation));
    }

    [Fact]
    public void ImputationDc_SansMois_Invalide()
    {
        var imputation = ImputationDc(mois: null);

        Assert.Throws<ArgumentException>(() => DemandePaiementImputationRules.ValiderStructure(imputation));
    }

    [Fact]
    public void ImputationAe_Valide()
    {
        var imputation = ImputationAe(libelleItemAe: "Travaux réseau MT");

        var exception = Record.Exception(() => DemandePaiementImputationRules.ValiderStructure(imputation));

        Assert.Null(exception);
        Assert.Equal(FormeImputationBudgetaire.ActionsExploitation, DemandePaiementImputationRules.DetecterForme(imputation));
    }

    [Fact]
    public void ImputationAe_AvecMoisVentilation_Valide()
    {
        var imputation = ImputationAe(libelleItemAe: "Transport Maniema");
        imputation.Mois = 9;

        var exception = Record.Exception(() => DemandePaiementImputationRules.ValiderStructure(imputation));

        Assert.Null(exception);
        Assert.Equal(FormeImputationBudgetaire.ActionsExploitation, DemandePaiementImputationRules.DetecterForme(imputation));
    }

    [Fact]
    public void ImputationAe_SansItemAe_Invalide()
    {
        var imputation = ImputationAe(libelleItemAe: null);

        Assert.Throws<ArgumentException>(() => DemandePaiementImputationRules.ValiderStructure(imputation));
    }

    [Fact]
    public void ImputationBi_Valide()
    {
        var imputation = ImputationBi(fkItemBi: 5, detailBi: "Ligne A — transformateur");

        var exception = Record.Exception(() => DemandePaiementImputationRules.ValiderStructure(imputation));

        Assert.Null(exception);
        Assert.Equal(FormeImputationBudgetaire.BudgetInvestissement, DemandePaiementImputationRules.DetecterForme(imputation));
    }

    [Fact]
    public void ImputationBi_SansItem_Invalide()
    {
        var imputation = ImputationBi(fkItemBi: null, detailBi: "Détail seul");

        Assert.Throws<ArgumentException>(() => DemandePaiementImputationRules.ValiderStructure(imputation));
    }

    [Fact]
    public void ImputationBi_AvecMoisVentilation_Valide()
    {
        var imputation = ImputationBi(fkItemBi: 5, detailBi: "Installation électrique", mois: 9);

        var exception = Record.Exception(() => DemandePaiementImputationRules.ValiderStructure(imputation));

        Assert.Null(exception);
    }

    [Fact]
    public void Imputation_SansFkBudgetLigne_Valide()
    {
        var imputation = ImputationDc(mois: 6, fkBudgetLigne: null);

        var exception = Record.Exception(() => DemandePaiementImputationRules.ValiderStructure(imputation));

        Assert.Null(exception);
        Assert.Null(imputation.FK_BudgetLigne);
    }

    [Fact]
    public void Montants_TauxNonPositif_Invalide()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            DemandePaiementMontants.Valider(100m, "USD", 0m, 100m));

        Assert.Contains("taux", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Montants_MontantNegatif_Invalide()
    {
        Assert.Throws<ArgumentException>(() =>
            DemandePaiementMontants.Valider(-1m, "USD", 1m, -1m));
    }

    [Fact]
    public void Workflow_TransitionsConformes()
    {
        var demande = DemandePaiementFactory.CreerBrouillon(
            "DP-2026-00002",
            new DateOnly(2026, 3, 1),
            1, 10, 5, 1,
            "Test workflow",
            100m, "USD",
            TypeBudgetCode.DepensesCourantes,
            null,
            ModePaiementDpm.Caisse,
            1, NowUtc);

        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.EnValidationN1);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.EnValidationN2);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.ValideeEntite);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.Soumise);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.EnTraitementDpm);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.EnControleBudgetaire);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.ViseeBudgetairement);

        Assert.Equal(StatutDemandePaiement.ViseeBudgetairement, demande.Statut);
    }

    [Fact]
    public void Statut_EstProgressionApres_Detecte_Conflit()
    {
        Assert.True(StatutDemandePaiement.EstProgressionApres(
            StatutDemandePaiement.Brouillon,
            StatutDemandePaiement.EnValidationN1));
        Assert.True(StatutDemandePaiement.EstProgressionApres(
            StatutDemandePaiement.ValideeEntite,
            StatutDemandePaiement.Soumise));
        Assert.False(StatutDemandePaiement.EstProgressionApres(
            StatutDemandePaiement.ValideeEntite,
            StatutDemandePaiement.EnValidationN1));
        Assert.False(StatutDemandePaiement.EstProgressionApres(
            StatutDemandePaiement.Brouillon,
            StatutDemandePaiement.Brouillon));
    }

    [Fact]
    public void Workflow_TransitionInterdite()
    {
        var demande = DemandePaiementFactory.CreerBrouillon(
            "DP-2026-00003",
            new DateOnly(2026, 3, 1),
            1, 10, 5, 1,
            "Test transition interdite",
            50m, "USD",
            TypeBudgetCode.DepensesCourantes,
            null,
            ModePaiementDpm.Caisse,
            1, NowUtc);

        Assert.Throws<InvalidOperationException>(() =>
            DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.ViseeBudgetairement));
    }

    [Fact]
    public void Workflow_ACorriger_RetourBrouillon()
    {
        var demande = DemandePaiementFactory.CreerBrouillon(
            "DP-2026-00004",
            new DateOnly(2026, 3, 1),
            1, 10, 5, 1,
            "Test retour correction",
            50m, "USD",
            TypeBudgetCode.DepensesCourantes,
            null,
            ModePaiementDpm.Caisse,
            1, NowUtc);

        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.EnValidationN1);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.EnValidationN2);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.ValideeEntite);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.Soumise);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.EnTraitementDpm);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.ACorriger);
        DemandePaiementWorkflow.Transitionner(demande, StatutDemandePaiement.Brouillon);

        Assert.Equal(StatutDemandePaiement.Brouillon, demande.Statut);
    }

    [Fact]
    public void Imputation_CoherenceTypeBudget_Applicative()
    {
        var imputation = ImputationDc(mois: 4);

        var ex = Assert.Throws<ArgumentException>(() =>
            DemandePaiementImputationRules.ValiderCoherenceTypeBudget(TypeBudgetCode.ActionsExploitation, imputation));

        Assert.Contains("incohérence", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DemandePaiementImputation ImputationDc(byte? mois, long? fkBudgetLigne = null)
        => new()
        {
            FK_DemandePaiement = 1,
            Ordre = 1,
            FK_TypeBudget = 1,
            FK_UniteBudgetaire = 10,
            FK_ExerciceBudgetaire = 1,
            FK_RubriqueBudgetaire = 100,
            Mois = mois,
            FK_BudgetLigne = fkBudgetLigne,
            MontantBrut = 229_000m,
            Devise = "CDF",
            TauxConversion = 2_290m,
            MontantUsd = 100m,
            FK_UtilisateurCreation = 1,
            DateImputation = NowUtc
        };

    private static DemandePaiementImputation ImputationAe(string? libelleItemAe, long? fkGroupeItemAe = null)
        => new()
        {
            FK_DemandePaiement = 1,
            Ordre = 1,
            FK_TypeBudget = 2,
            FK_UniteBudgetaire = 10,
            FK_ExerciceBudgetaire = 1,
            FK_RubriqueBudgetaire = 200,
            LibelleItemAE = libelleItemAe,
            FK_GroupeItemAE = fkGroupeItemAe,
            MontantBrut = 500m,
            Devise = "USD",
            TauxConversion = 1m,
            MontantUsd = 500m,
            FK_UtilisateurCreation = 1,
            DateImputation = NowUtc
        };

    private static DemandePaiementImputation ImputationBi(
        long? fkItemBi,
        string? detailBi,
        byte? mois = null)
        => new()
        {
            FK_DemandePaiement = 1,
            Ordre = 1,
            FK_TypeBudget = 3,
            FK_UniteBudgetaire = 10,
            FK_ExerciceBudgetaire = 1,
            FK_ItemBI = fkItemBi,
            DetailBI = detailBi,
            Mois = mois,
            MontantBrut = 1_000m,
            Devise = "USD",
            TauxConversion = 1m,
            MontantUsd = 1_000m,
            FK_UtilisateurCreation = 1,
            DateImputation = NowUtc
        };
}
