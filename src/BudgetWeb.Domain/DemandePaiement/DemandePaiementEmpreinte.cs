using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>
/// Empreinte métier SHA-256 d'une DPM pour le circuit de validation Entité (N1/N2).
/// Inclut l'en-tête sollicité, les bénéficiaires, les pièces justificatives (hors DOCUMENT_DPM_SIGNE)
/// et les imputations éventuellement déjà présentes (sans conversion Budget).
/// Exclut statut, audit, document signé et champs de traitement/conversion Budget.
/// </summary>
public static class DemandePaiementEmpreinte
{
    public static string Calculer(Entities.DemandePaiement demande)
    {
        ArgumentNullException.ThrowIfNull(demande);

        var sb = new StringBuilder(2048);

        AppendDemande(sb, demande);
        AppendBeneficiaires(sb, demande.Beneficiaires);
        AppendPiecesJustificatives(sb, demande.PiecesJointes);
        AppendImputations(sb, demande.Imputations);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    public static bool EstPieceJustificativeMetier(PieceJointe piece)
        => !string.Equals(
            piece.CodeTypePiece,
            TypePieceJointeDpm.DocumentDpmSigne,
            StringComparison.OrdinalIgnoreCase);

    private static void AppendDemande(StringBuilder sb, Entities.DemandePaiement demande)
    {
        AppendField(sb, "D", "DateEmission", demande.DateEmission.ToString("O", CultureInfo.InvariantCulture));
        AppendField(sb, "D", "LieuEmission", demande.LieuEmission);
        AppendField(sb, "D", "FK_Demandeur", demande.FK_Demandeur);
        AppendField(sb, "D", "FK_CasDossier", demande.FK_CasDossier);
        AppendField(sb, "D", "TypeBudgetSollicite", demande.TypeBudgetSollicite);
        AppendField(sb, "D", "ItemSollicite", demande.ItemSollicite);
        AppendField(sb, "D", "Objet", demande.Objet);
        AppendField(sb, "D", "CompteSection", demande.CompteSection);
        AppendField(sb, "D", "MontantBrut", demande.MontantBrut.ToString(CultureInfo.InvariantCulture));
        AppendField(sb, "D", "Devise", demande.Devise);
        AppendField(sb, "D", "FK_Devise", demande.FK_Devise);
        AppendField(sb, "D", "ModePaiementSollicite", demande.ModePaiementSollicite);
    }

    private static void AppendBeneficiaires(StringBuilder sb, IEnumerable<DemandePaiementBeneficiaire> beneficiaires)
    {
        foreach (var b in beneficiaires.OrderBy(x => x.Ordre).ThenBy(x => x.IdBeneficiaire))
        {
            sb.Append("|B|")
                .Append(b.Ordre).Append('|')
                .Append(N(b.TypeBeneficiaire)).Append('|')
                .Append(N(b.NomComplet)).Append('|')
                .Append(N(b.Matricule)).Append('|')
                .Append(N(b.Fonction)).Append('|')
                .Append(N(b.RaisonSociale)).Append('|')
                .Append(N(b.Rccm)).Append('|')
                .Append(N(b.Adresse)).Append('|')
                .Append(N(b.Banque)).Append('|')
                .Append(N(b.NumeroCompte)).Append('|')
                .Append(b.EstPrincipal ? '1' : '0')
                .Append('\n');
        }
    }

    private static void AppendPiecesJustificatives(StringBuilder sb, IEnumerable<PieceJointe> pieces)
    {
        var justificatifs = pieces
            .Where(EstPieceJustificativeMetier)
            .OrderBy(p => p.CodeTypePiece, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.FK_PieceObligatoire ?? 0)
            .ThenBy(p => p.HashSha256, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Libelle, StringComparer.OrdinalIgnoreCase);

        foreach (var p in justificatifs)
        {
            sb.Append("|P|")
                .Append(N(p.CodeTypePiece)).Append('|')
                .Append(p.FK_PieceObligatoire?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
                .Append(N(p.Libelle)).Append('|')
                .Append(p.EstObligatoire ? '1' : '0').Append('|')
                .Append(N(p.HashSha256)).Append('|')
                .Append(p.TailleOctets.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }
    }

    /// <summary>
    /// Imputations incluses si déjà présentes (habituellement aucune au stade Entité).
    /// Champs d'identification et de montant sollicité uniquement — pas TauxConversion ni MontantUsd
    /// (renseignés lors du traitement Budget).
    /// </summary>
    private static void AppendImputations(StringBuilder sb, IEnumerable<DemandePaiementImputation> imputations)
    {
        foreach (var i in imputations.OrderBy(x => x.Ordre).ThenBy(x => x.IdImputation))
        {
            sb.Append("|I|")
                .Append(i.Ordre).Append('|')
                .Append(i.FK_TypeBudget).Append('|')
                .Append(i.FK_UniteBudgetaire).Append('|')
                .Append(i.FK_ExerciceBudgetaire).Append('|')
                .Append(i.FK_RubriqueBudgetaire?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
                .Append(i.Mois?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
                .Append(N(i.LibelleItemAE)).Append('|')
                .Append(i.FK_GroupeItemAE?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
                .Append(i.FK_ItemBI?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
                .Append(N(i.DetailBI)).Append('|')
                .Append(i.FK_BudgetLigne?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
                .Append(i.MontantBrut.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(N(i.Devise)).Append('|')
                .Append(i.NumeroFicheSuivi?.ToString(CultureInfo.InvariantCulture) ?? "")
                .Append('\n');
        }
    }

    private static void AppendField(StringBuilder sb, string section, string name, object? value)
    {
        sb.Append('|').Append(section).Append('|').Append(name).Append('|')
            .Append(N(value?.ToString())).Append('\n');
    }

    private static string N(string? value) => (value ?? string.Empty).Trim();
}
