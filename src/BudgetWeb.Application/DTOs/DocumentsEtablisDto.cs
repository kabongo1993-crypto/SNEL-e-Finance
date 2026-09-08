using BudgetWeb.Domain.DemandePaiement;

namespace BudgetWeb.Application.DTOs;

public record DocumentsEtablisQuery(
    DateOnly DateDebut,
    DateOnly DateFin,
    string? TypeDocument = null,
    long? IdExercice = null,
    long? IdUB = null,
    long? IdDepartement = null,
    IReadOnlyList<DocumentEtabliSelectionDto>? Selection = null);

public record DocumentEtabliSelectionDto(
    long IdDemandePaiement,
    string TypeDocument,
    string NumeroDocument)
{
    public string Cle()
        => Cle(IdDemandePaiement, TypeDocument, NumeroDocument);

    public static string Cle(long idDemande, string typeDocument, string? numeroDocument)
        => $"{idDemande}|{TypeDocumentEtabli.Normaliser(typeDocument)}|{numeroDocument?.Trim() ?? string.Empty}";

    public static IReadOnlyList<DocumentEtabliSelectionDto>? ParseList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var list = new List<DocumentEtabliSelectionDto>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split('|');
            if (bits.Length < 2 || !long.TryParse(bits[0], out var id) || string.IsNullOrWhiteSpace(bits[1]))
            {
                throw new ArgumentException("Sélection de documents invalide.");
            }

            list.Add(new DocumentEtabliSelectionDto(
                id,
                bits[1],
                bits.Length >= 3 ? bits[2] : string.Empty));
        }

        return list.Count == 0 ? null : list;
    }
}

public record DocumentEtabliListItemDto(
    long IdDemandePaiement,
    string Reference,
    string TypeDocument,
    string LibelleTypeDocument,
    string NumeroDocument,
    DateTime DateEtabli,
    DateOnly DateDocument,
    decimal Montant,
    string Devise,
    string BeneficiaireAffichage,
    string? NomUtilisateurEtabli,
    string PdfRouteSegment,
    long IdUB);
