using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementEmpreinteTests
{
    [Fact]
    public void Empreinte_Stable_Sans_Changement_Metier()
    {
        var d1 = SampleDemande();
        var d2 = SampleDemande();
        d2.Reference = "AUTRE-REF";
        d2.Statut = StatutDemandePaiement.Soumise;
        d2.DateModification = DateTime.UtcNow;
        d2.FK_UtilisateurModification = 99;

        Assert.Equal(DemandePaiementEmpreinte.Calculer(d1), DemandePaiementEmpreinte.Calculer(d2));
    }

    [Fact]
    public void Empreinte_Change_Quand_Montant_Modifie()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.MontantBrut = 2000m;
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Theory]
    [InlineData(nameof(DemandePaiementEntity.Objet), "Objet modifié")]
    [InlineData(nameof(DemandePaiementEntity.TypeBudgetSollicite), "AE")]
    [InlineData(nameof(DemandePaiementEntity.ItemSollicite), "42")]
    [InlineData(nameof(DemandePaiementEntity.Devise), "CDF")]
    [InlineData(nameof(DemandePaiementEntity.ModePaiementSollicite), "BANQUE")]
    public void Empreinte_Change_Quand_Donnee_Demande_Modifiee(string property, string value)
    {
        var before = SampleDemande();
        var after = SampleDemande();
        typeof(DemandePaiementEntity).GetProperty(property)!.SetValue(after, value);
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Change_Quand_Beneficiaire_Nom_Modifie()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.Beneficiaires.First().NomComplet = "Autre nom";
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Change_Quand_Beneficiaire_Banque_Modifiee()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.Beneficiaires.First().Banque = "RAWBANK";
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Change_Quand_Beneficiaire_Compte_Modifie()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.Beneficiaires.First().NumeroCompte = "000999";
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Change_Quand_Beneficiaire_Type_Modifie()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.Beneficiaires.First().TypeBeneficiaire = "FOURNISSEUR";
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Change_Quand_Beneficiaire_Fonction_Rccm_Adresse_Modifies()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.Beneficiaires.First().Fonction = "Directeur";
        after.Beneficiaires.First().Rccm = "RCCM-001";
        after.Beneficiaires.First().Adresse = "Kinshasa";
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Change_Quand_Piece_Ajoutee()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.PiecesJointes.Add(SamplePiece("FACTURE", "abc123"));
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Change_Quand_Piece_Supprimee()
    {
        var before = SampleDemande();
        before.PiecesJointes.Add(SamplePiece("FACTURE", "abc123"));
        var after = SampleDemande();
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Change_Quand_Piece_Remplacee()
    {
        var before = SampleDemande();
        before.PiecesJointes.Add(SamplePiece("FACTURE", "abc123"));
        var after = SampleDemande();
        after.PiecesJointes.Add(SamplePiece("FACTURE", "def456"));
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Inchangee_Quand_Document_Signe_Ajoute()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.PiecesJointes.Add(new PieceJointe
        {
            CodeTypePiece = TypePieceJointeDpm.DocumentDpmSigne,
            Libelle = "Document signé",
            EstObligatoire = false,
            HashSha256 = "signe123",
            TailleOctets = 100,
        });
        Assert.Equal(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Change_Quand_Imputation_Presente()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.Imputations.Add(SampleImputation());
        Assert.NotEqual(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Inchangee_Quand_TauxConversion_Et_MontantUsd_Demande_Modifies()
    {
        var before = SampleDemande();
        var after = SampleDemande();
        after.TauxConversion = 2500m;
        after.MontantUsd = 0.4m;
        Assert.Equal(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void Empreinte_Inchangee_Quand_Imputation_TauxConversion_Et_MontantUsd_Modifies()
    {
        var before = SampleDemande();
        before.Imputations.Add(SampleImputation(tauxConversion: 1m, montantUsd: 500m));
        var after = SampleDemande();
        after.Imputations.Add(SampleImputation(tauxConversion: 2800m, montantUsd: 0.18m));
        Assert.Equal(DemandePaiementEmpreinte.Calculer(before), DemandePaiementEmpreinte.Calculer(after));
    }

    [Fact]
    public void InvaliderValidationsSiEmpreinteObsolete_Reinitialise_Apres_Modification()
    {
        var demande = SampleDemande();
        SeedValidations(demande);
        demande.ValidationsEntite.First(v => v.Niveau == 1).EmpreinteDonnees =
            DemandePaiementEmpreinte.Calculer(demande);

        demande.Objet = "Objet modifié après validation";

        ValidationEntiteRules.InvaliderValidationsSiEmpreinteObsolete(demande);

        Assert.All(demande.ValidationsEntite, v =>
        {
            Assert.Equal(StatutValidationEntite.EnAttente, v.Statut);
            Assert.Null(v.EmpreinteDonnees);
            Assert.Null(v.ModeValidation);
        });
    }

    [Fact]
    public void InvaliderValidationsSiEmpreinteObsolete_Ne_Fait_Rien_Sans_Changement()
    {
        var demande = SampleDemande();
        SeedValidations(demande);
        var empreinte = DemandePaiementEmpreinte.Calculer(demande);
        foreach (var v in demande.ValidationsEntite)
        {
            v.Statut = StatutValidationEntite.Validee;
            v.EmpreinteDonnees = empreinte;
            v.ModeValidation = ModeValidationEntite.Electronique;
        }

        ValidationEntiteRules.InvaliderValidationsSiEmpreinteObsolete(demande);

        Assert.All(demande.ValidationsEntite, v =>
            Assert.Equal(StatutValidationEntite.Validee, v.Statut));
    }

    [Fact]
    public async Task UpdateBrouillon_Invalide_Validations_Obsoletes()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        var entity = await repo.GetTrackedAsync(created.IdDemandePaiement)
            ?? throw new InvalidOperationException("Demande introuvable.");
        entity.Statut = StatutDemandePaiement.ACorriger;
        SeedValidations(entity);
        entity.ValidationsEntite.First(v => v.Niveau == 1).Statut = StatutValidationEntite.Validee;
        entity.ValidationsEntite.First(v => v.Niveau == 1).EmpreinteDonnees =
            DemandePaiementEmpreinte.Calculer(entity);

        await svc.UpdateBrouillonAsync(
            created.IdDemandePaiement,
            new UpdateDemandePaiementRequest(
                entity.DateEmission,
                entity.LieuEmission,
                "Objet corrigé",
                entity.CompteSection,
                entity.MontantBrut,
                entity.Devise,
                entity.TypeBudgetSollicite!,
                entity.ItemSollicite,
                entity.ModePaiementSollicite!,
                null,
                entity.FK_Devise));

        var reloaded = await repo.GetTrackedAsync(created.IdDemandePaiement);
        Assert.NotNull(reloaded);
        Assert.All(reloaded!.ValidationsEntite, v =>
            Assert.Equal(StatutValidationEntite.EnAttente, v.Statut));
    }

    private static DemandePaiementEntity SampleDemande()
    {
        var d = new DemandePaiementEntity
        {
            IdDemandePaiement = 1,
            Reference = "DP-2026-00001",
            DateEmission = new DateOnly(2026, 3, 1),
            LieuEmission = "Kinshasa",
            FK_Demandeur = 5,
            FK_CasDossier = 1,
            TypeBudgetSollicite = "DC",
            ItemSollicite = null,
            Objet = "Achat fournitures",
            CompteSection = "61",
            MontantBrut = 1000m,
            Devise = "USD",
            FK_Devise = 1,
            ModePaiementSollicite = "CAISSE",
            Statut = StatutDemandePaiement.Brouillon,
            Beneficiaires =
            [
                new DemandePaiementBeneficiaire
                {
                    Ordre = 1,
                    TypeBeneficiaire = "AGENT",
                    NomComplet = "Jean Dupont",
                    Matricule = "M001",
                    Fonction = "Agent",
                    RaisonSociale = null,
                    Rccm = null,
                    Adresse = "Gombe",
                    Banque = "BCDC",
                    NumeroCompte = "001122",
                    EstPrincipal = true,
                },
            ],
        };
        return d;
    }

    private static PieceJointe SamplePiece(string code, string hash)
        => new()
        {
            CodeTypePiece = code,
            Libelle = "Pièce test",
            EstObligatoire = true,
            FK_PieceObligatoire = 1,
            HashSha256 = hash,
            TailleOctets = 1024,
        };

    private static DemandePaiementImputation SampleImputation(
        decimal tauxConversion = 1m,
        decimal montantUsd = 500m)
        => new()
        {
            Ordre = 1,
            FK_TypeBudget = 1,
            FK_UniteBudgetaire = 10,
            FK_ExerciceBudgetaire = 2026,
            FK_BudgetLigne = 100,
            MontantBrut = 500,
            Devise = "USD",
            TauxConversion = tauxConversion,
            MontantUsd = montantUsd,
        };

    private static void SeedValidations(DemandePaiementEntity demande)
    {
        demande.ValidationsEntite =
        [
            new DemandePaiementValidation
            {
                Niveau = ValidationEntiteNiveau.N1,
                Ordre = 1,
                Statut = StatutValidationEntite.Validee,
            },
            new DemandePaiementValidation
            {
                Niveau = ValidationEntiteNiveau.N2,
                Ordre = 2,
                Statut = StatutValidationEntite.EnAttente,
            },
        ];
    }
}
