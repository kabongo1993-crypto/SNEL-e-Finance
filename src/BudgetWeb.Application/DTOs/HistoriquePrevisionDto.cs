namespace BudgetWeb.Application.DTOs;

/// <summary>Événement d'historique Version×UB reconstruit depuis JOURNAL_AUDIT.</summary>
public record HistoriquePrevisionEvenementDto(
    long IdAudit,
    DateTime DateHeure,
    long IdExercice,
    int AnneeExercice,
    long IdVersion,
    int NumeroVersion,
    string? LibelleVersion,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    /// <summary>DC / AE / BI — null si l'événement est au niveau UB (workflow).</summary>
    string? TypePrevision,
    string Action,
    string ActionLibelle,
    string Portee,
    string? AncienStatut,
    string? NouveauStatut,
    long IdUtilisateur,
    string NomUtilisateur,
    string? Motif,
    decimal MontantDC,
    decimal MontantAE,
    decimal MontantBI,
    decimal MontantTotal,
    /// <summary>Statut opérationnel actuel (WORKFLOW_PREVISION_UB), distinct de l'historique.</summary>
    string? StatutActuel);

public record HistoriquePrevisionPageDto(
    IReadOnlyList<HistoriquePrevisionEvenementDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool PeutVoirToutes);

public record HistoriquePrevisionTimelineDto(
    long IdVersion,
    int NumeroVersion,
    string? LibelleVersion,
    int AnneeExercice,
    long IdDepartement,
    string CodeDepartement,
    string LibelleDepartement,
    long IdUB,
    string CodeUB,
    string LibelleUB,
    string StatutActuel,
    decimal MontantDC,
    decimal MontantAE,
    decimal MontantBI,
    decimal MontantTotal,
    IReadOnlyList<HistoriquePrevisionEvenementDto> Evenements);

/// <summary>Filtres de consultation — appliqués côté repository.</summary>
public record HistoriquePrevisionQuery(
    long? IdExercice,
    long? IdVersion,
    long? IdDepartement,
    long? IdUB,
    string? Type,
    string? Action,
    string? Statut,
    DateTime? DateDebut,
    DateTime? DateFin,
    string? Search,
    /// <summary>true = mon historique (JWT) ; false = périmètre élargi si permission.</summary>
    bool MonHistorique,
    int Page,
    int PageSize);
