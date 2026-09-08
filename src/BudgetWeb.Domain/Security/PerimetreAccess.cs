namespace BudgetWeb.Domain.Security;

/// <summary>Snapshot de périmètre pour évaluation runtime (sans I/O).</summary>
public sealed record PerimetreUtilisateurSnapshot(
    bool TousDepartements,
    bool ToutesUnitesBudgetaires,
    IReadOnlyList<long> IdDepartements,
    IReadOnlyList<long> IdUnitesBudgetaires);

/// <summary>
/// Règles pures de périmètre utilisateur (Département / UB) — sans I/O.
/// </summary>
public static class PerimetreAccess
{
    /// <summary>
    /// True si un périmètre réel est exploitable (flags « tous » ou au moins une ligne).
    /// Un enregistrement vide (false/false/[]/[]) n'est PAS configuré → fallback historique.
    /// </summary>
    public static bool EstConfigure(PerimetreUtilisateurSnapshot? perimetre)
    {
        if (perimetre is null)
            return false;

        return perimetre.TousDepartements
               || perimetre.ToutesUnitesBudgetaires
               || perimetre.IdDepartements.Count > 0
               || perimetre.IdUnitesBudgetaires.Count > 0;
    }

    public static bool PeutAccederDepartement(
        bool tousDepartements,
        IReadOnlyCollection<long> departementsAutorises,
        long idDepartement)
    {
        if (tousDepartements)
            return true;
        return departementsAutorises.Contains(idDepartement);
    }

    public static bool PeutAccederUb(
        bool toutesUb,
        bool tousDepartements,
        IReadOnlyCollection<long> departementsAutorises,
        IReadOnlyCollection<long> ubsAutorisees,
        long idUb,
        long idDepartementUb)
    {
        if (toutesUb)
            return true;

        if (!PeutAccederDepartement(tousDepartements, departementsAutorises, idDepartementUb))
            return false;

        // Si des UB sont listées : l'UB doit être dans la liste.
        // Si aucune UB listée mais départements sélectionnés : accès à toutes les UB du département.
        if (ubsAutorisees.Count == 0)
            return true;

        return ubsAutorisees.Contains(idUb);
    }

    public static bool PeutAccederUb(
        PerimetreUtilisateurSnapshot perimetre,
        long idUb,
        long idDepartementUb)
        => PeutAccederUb(
            perimetre.ToutesUnitesBudgetaires,
            perimetre.TousDepartements,
            perimetre.IdDepartements,
            perimetre.IdUnitesBudgetaires,
            idUb,
            idDepartementUb);
}
