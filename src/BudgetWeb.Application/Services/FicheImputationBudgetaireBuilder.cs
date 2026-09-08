using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

/// <summary>Assemble le modèle de fiche d'imputation à partir du domaine existant.</summary>
public static class FicheImputationBudgetaireBuilder
{
    public static async Task<FicheImputationBudgetaireDto> BuildTravailAsync(
        DemandePaiementEntity demande,
        IDemandePaiementRepository repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        ArgumentNullException.ThrowIfNull(repository);

        if (demande.Imputations.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucune imputation enregistrée — la fiche de travail ne peut pas être générée.");
        }

        var controle = await DemandePaiementControleBudgetaire.ControlerDemandeAsync(
            demande,
            repository,
            cancellationToken);

        var imputations = demande.Imputations.OrderBy(i => i.Ordre).ToList();
        var controlesParId = controle.Imputations.ToDictionary(c => c.IdImputation);
        var lignes = new List<FicheImputationLigneDto>(imputations.Count);
        var item = 1;

        foreach (var imputation in imputations)
        {
            if (!controlesParId.TryGetValue(imputation.IdImputation, out var ctrl))
            {
                throw new InvalidOperationException(
                    $"Contrôle budgétaire introuvable pour l'imputation {imputation.Ordre}.");
            }

            var codeType = imputation.TypeBudget?.CodeType ?? ctrl.CodeTypeBudget;
            var engagementAnnuel = ResoudreEngagementEnCoursAnnuel(imputation, demande, codeType);
            var engagementMensuel = EstDc(codeType) ? (decimal?)imputation.MontantUsd : null;

            lignes.Add(new FicheImputationLigneDto(
                item++,
                ResoudreCodeUb(imputation, demande),
                FormaterNumeroSuivi(imputation.NumeroFicheSuivi),
                ResoudreRubriqueAffichage(imputation),
                BudgetMensuel: null,
                CreditEngageMensuel: null,
                EngagementEnCoursMensuel: engagementMensuel,
                CreditDisponibleMensuel: null,
                BudgetAnnuel: null,
                CreditEngageAnnuel: null,
                EngagementEnCoursAnnuel: engagementAnnuel,
                CreditDisponibleAnnuel: null));
        }

        return AssemblerFiche(
            FicheImputationMode.Travail,
            demande,
            lignes,
            null,
            ResoudreJunior(demande),
            ResoudreSenior(demande),
            ResoudreChefDivision(demande, definitive: false),
            demande.Imputations.Sum(i => i.MontantUsd));
    }

    public static Task<FicheImputationBudgetaireDto> BuildDefinitiveAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(demande);

        if (!string.Equals(
                StatutDemandePaiement.Normaliser(demande.Statut),
                StatutDemandePaiement.ViseeBudgetairement,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "La fiche définitive n'est disponible qu'après visa budgétaire.");
        }

        if (demande.Imputations.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucune imputation — la fiche définitive ne peut pas être générée.");
        }

        var imputations = demande.Imputations.OrderBy(i => i.Ordre).ToList();
        var lignes = new List<FicheImputationLigneDto>(imputations.Count);
        var item = 1;

        foreach (var imputation in imputations)
        {
            var snapshot = imputation.Snapshots
                .OrderByDescending(s => s.DateSnapshot)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"Snapshot budgétaire introuvable pour l'imputation {imputation.Ordre}.");

            var codeType = imputation.TypeBudget?.CodeType
                ?? throw new InvalidOperationException(
                    $"Type budget introuvable pour l'imputation {imputation.Ordre}.");

            var engagementAnnuel = ResoudreEngagementEnCoursAnnuel(imputation, demande, codeType);
            var engagementMensuel = EstDc(codeType) ? (decimal?)snapshot.MontantUsd : null;

            lignes.Add(new FicheImputationLigneDto(
                item++,
                ResoudreCodeUb(imputation, demande),
                FormaterNumeroSuivi(imputation.NumeroFicheSuivi),
                ResoudreRubriqueAffichage(imputation),
                BudgetMensuel: snapshot.BudgetMensuel,
                CreditEngageMensuel: snapshot.CreditEngageMensuel,
                EngagementEnCoursMensuel: engagementMensuel,
                CreditDisponibleMensuel: snapshot.CreditDisponibleMensuelAvantVisa,
                BudgetAnnuel: snapshot.BudgetAnnuel,
                CreditEngageAnnuel: snapshot.CreditEngageAnnuel,
                EngagementEnCoursAnnuel: engagementAnnuel,
                CreditDisponibleAnnuel: snapshot.CreditDisponibleAnnuelAvantVisa));
        }

        return Task.FromResult(AssemblerFiche(
            FicheImputationMode.Definitive,
            demande,
            lignes,
            demande.DateVisa,
            ResoudreJunior(demande),
            ResoudreSenior(demande),
            ResoudreChefDivision(demande, definitive: true),
            imputations.Sum(i => i.MontantUsd)));
    }

    private static FicheImputationBudgetaireDto AssemblerFiche(
        FicheImputationMode mode,
        DemandePaiementEntity demande,
        IReadOnlyList<FicheImputationLigneDto> lignes,
        DateTime? dateEngagement,
        FicheImputationSignataireDto junior,
        FicheImputationSignataireDto senior,
        FicheImputationSignataireDto chef,
        decimal totalEngagementAnnuelDemande)
    {
        var totaux = CalculerTotaux(mode, lignes, totalEngagementAnnuelDemande);
        var codeType = demande.TypeBudget?.CodeType ?? demande.TypeBudgetSollicite;

        return new FicheImputationBudgetaireDto(
            mode,
            demande.IdDemandePaiement,
            demande.Reference,
            demande.ExerciceBudgetaire.Annee,
            codeType?.Trim().ToUpperInvariant(),
            dateEngagement,
            DateTime.Now,
            lignes,
            totaux,
            junior,
            senior,
            chef);
    }

    public static FicheImputationTotauxDto CalculerTotaux(
        FicheImputationMode mode,
        IReadOnlyList<FicheImputationLigneDto> lignes,
        decimal totalEngagementAnnuelDemande)
    {
        if (mode == FicheImputationMode.Travail)
        {
            return new FicheImputationTotauxDto(
                TotalBudgetMensuel: null,
                TotalCreditEngageMensuel: null,
                TotalEngagementEnCoursMensuel: Somme(lignes, l => l.EngagementEnCoursMensuel),
                TotalCreditDisponibleMensuel: null,
                TotalBudgetAnnuel: null,
                TotalCreditEngageAnnuel: null,
                TotalEngagementEnCoursAnnuel: lignes.Count == 0 ? null : totalEngagementAnnuelDemande,
                TotalCreditDisponibleAnnuel: null);
        }

        return new FicheImputationTotauxDto(
            TotalBudgetMensuel: Somme(lignes, l => l.BudgetMensuel),
            TotalCreditEngageMensuel: Somme(lignes, l => l.CreditEngageMensuel),
            TotalEngagementEnCoursMensuel: Somme(lignes, l => l.EngagementEnCoursMensuel),
            TotalCreditDisponibleMensuel: Somme(lignes, l => l.CreditDisponibleMensuel),
            TotalBudgetAnnuel: Somme(lignes, l => l.BudgetAnnuel),
            TotalCreditEngageAnnuel: Somme(lignes, l => l.CreditEngageAnnuel),
            TotalEngagementEnCoursAnnuel: lignes.Count == 0 ? null : totalEngagementAnnuelDemande,
            TotalCreditDisponibleAnnuel: Somme(lignes, l => l.CreditDisponibleAnnuel));
    }

    private static decimal? Somme(
        IReadOnlyList<FicheImputationLigneDto> lignes,
        Func<FicheImputationLigneDto, decimal?> selecteur)
    {
        if (lignes.All(l => selecteur(l) is null))
            return null;

        return lignes.Sum(l => selecteur(l) ?? 0m);
    }

    private static decimal ResoudreEngagementEnCoursAnnuel(
        DemandePaiementImputation imputation,
        DemandePaiementEntity demande,
        string codeType)
    {
        var code = codeType.Trim().ToUpperInvariant();

        if (EstDc(code))
        {
            return demande.Imputations
                .Where(x => x.FK_ExerciceBudgetaire == imputation.FK_ExerciceBudgetaire
                            && x.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                            && x.FK_RubriqueBudgetaire == imputation.FK_RubriqueBudgetaire
                            && x.Mois != null)
                .Sum(x => x.MontantUsd);
        }

        if (string.Equals(code, TypeBudgetCode.ActionsExploitation, StringComparison.Ordinal))
        {
            var item = (imputation.LibelleItemAE ?? string.Empty).Trim();
            return demande.Imputations
                .Where(x => x.FK_ExerciceBudgetaire == imputation.FK_ExerciceBudgetaire
                            && x.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                            && x.FK_RubriqueBudgetaire == imputation.FK_RubriqueBudgetaire
                            && string.Equals(
                                (x.LibelleItemAE ?? string.Empty).Trim(),
                                item,
                                StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.MontantUsd);
        }

        if (string.Equals(code, TypeBudgetCode.BudgetInvestissement, StringComparison.Ordinal))
        {
            var detail = (imputation.DetailBI ?? string.Empty).Trim();
            return demande.Imputations
                .Where(x => x.FK_ExerciceBudgetaire == imputation.FK_ExerciceBudgetaire
                            && x.FK_UniteBudgetaire == imputation.FK_UniteBudgetaire
                            && x.FK_ItemBI == imputation.FK_ItemBI)
                .Where(x => string.Equals(
                    (x.DetailBI ?? string.Empty).Trim(),
                    detail,
                    StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.MontantUsd);
        }

        return imputation.MontantUsd;
    }

    private static string ResoudreCodeUb(DemandePaiementImputation imputation, DemandePaiementEntity demande)
        => imputation.UniteBudgetaire?.CodeUB
           ?? demande.UniteBudgetaire?.CodeUB
           ?? throw new InvalidOperationException(
               $"UB introuvable pour l'imputation {imputation.Ordre}.");

    private static string? ResoudreRubriqueAffichage(DemandePaiementImputation imputation)
    {
        if (!string.IsNullOrWhiteSpace(imputation.RubriqueBudgetaire?.CodeRB))
            return imputation.RubriqueBudgetaire.CodeRB.Trim();

        if (!string.IsNullOrWhiteSpace(imputation.ItemBI?.CodeItem))
            return imputation.ItemBI.CodeItem.Trim();

        return null;
    }

    private static string? FormaterNumeroSuivi(int? numero)
        => numero.HasValue ? numero.Value.ToString() : null;

    private static bool EstDc(string codeType)
        => string.Equals(codeType.Trim(), TypeBudgetCode.DepensesCourantes, StringComparison.Ordinal);

    private static FicheImputationSignataireDto ResoudreJunior(DemandePaiementEntity demande)
    {
        var imputation = demande.Imputations
            .OrderByDescending(i => i.DateImputation)
            .ThenByDescending(i => i.Ordre)
            .FirstOrDefault();

        if (imputation?.UtilisateurCreation is not { } user)
        {
            return new FicheImputationSignataireDto(null, "Gestionnaire Junior", null);
        }

        return new FicheImputationSignataireDto(
            FormaterNom(user.Nom, user.Prenom, user.NomUtilisateur),
            LibelleFonctionJunior(demande),
            imputation.DateImputation);
    }

    private static FicheImputationSignataireDto ResoudreSenior(DemandePaiementEntity demande)
    {
        if (demande.UtilisateurControle is not { } user)
        {
            return new FicheImputationSignataireDto(null, "Gestionnaire Senior", null);
        }

        var juniorId = demande.Imputations
            .OrderByDescending(i => i.DateImputation)
            .Select(i => i.FK_UtilisateurCreation)
            .FirstOrDefault();

        if (juniorId == user.IdUtilisateur)
        {
            return new FicheImputationSignataireDto(null, "Gestionnaire Senior", null);
        }

        return new FicheImputationSignataireDto(
            FormaterNom(user.Nom, user.Prenom, user.NomUtilisateur),
            AppPermissions.LibelleProfil(AppRoles.GestionnaireSenior) ?? "Gestionnaire Senior",
            demande.DateControle);
    }

    private static FicheImputationSignataireDto ResoudreChefDivision(
        DemandePaiementEntity demande,
        bool definitive)
    {
        if (!definitive || demande.UtilisateurVisa is not { } user)
        {
            return new FicheImputationSignataireDto(null, "Chef de Division", null);
        }

        var fonction = AppPermissions.LibelleProfil(AppRoles.ChefDivision)
            ?? AppPermissions.LibelleProfil(AppRoles.DirecteurBudgets)
            ?? "Chef de Division";

        return new FicheImputationSignataireDto(
            FormaterNom(user.Nom, user.Prenom, user.NomUtilisateur),
            fonction,
            demande.DateVisa);
    }

    private static string? LibelleFonctionJunior(DemandePaiementEntity demande)
    {
        var code = demande.TypeBudget?.CodeType ?? demande.TypeBudgetSollicite;
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            TypeBudgetCode.DepensesCourantes =>
                AppPermissions.LibelleProfil(AppRoles.GestionnaireJuniorDc) ?? "Gestionnaire Junior",
            TypeBudgetCode.ActionsExploitation =>
                AppPermissions.LibelleProfil(AppRoles.GestionnaireJuniorAe) ?? "Gestionnaire Junior",
            TypeBudgetCode.BudgetInvestissement =>
                AppPermissions.LibelleProfil(AppRoles.GestionnaireJuniorBi) ?? "Gestionnaire Junior",
            _ => AppPermissions.LibelleProfil(AppRoles.GestionnaireJunior) ?? "Gestionnaire Junior",
        };
    }

    private static string? FormaterNom(string? nom, string? prenom, string? nomUtilisateur)
    {
        var parts = new[] { prenom, nom }.Where(s => !string.IsNullOrWhiteSpace(s));
        var joined = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(joined) ? nomUtilisateur : joined;
    }
}
