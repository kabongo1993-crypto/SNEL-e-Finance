namespace BudgetWeb.Domain.Entities;

/// <summary>
/// Document officiel lié à un événement workflow prévision (métadonnées + snapshot).
/// Ne remplace pas WORKFLOW_PREVISION_UB ni PREVISION_BUDGETAIRE.
/// </summary>
public class DocumentPrevision
{
    public long IdDocument { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string TypeDocument { get; set; } = string.Empty;
    public long? IdAudit { get; set; }
    public long IdVersion { get; set; }
    public short AnneeExercice { get; set; }
    public int NumeroVersion { get; set; }
    public long IdDepartement { get; set; }
    public string CodeDepartement { get; set; } = string.Empty;
    public string LibelleDepartement { get; set; } = string.Empty;
    public long? IdUB { get; set; }
    public string? CodeUB { get; set; }
    public string? LibelleUB { get; set; }
    public string Portee { get; set; } = string.Empty;
    public int NbUbConcernees { get; set; }
    public long IdUtilisateurAuteur { get; set; }
    public string NomUtilisateurAuteur { get; set; } = string.Empty;
    public DateTime DateEvenement { get; set; }
    public string? StatutAvant { get; set; }
    public string? StatutApres { get; set; }
    public string? Motif { get; set; }
    public decimal MontantDC { get; set; }
    public decimal MontantAE { get; set; }
    public decimal MontantBI { get; set; }
    public decimal MontantTotal { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string CheminFichier { get; set; } = string.Empty;
    public string HashSha256 { get; set; } = string.Empty;
    public long TailleOctets { get; set; }
    public DateTime DateGeneration { get; set; }
}

/// <summary>Compteur atomique pour la référence SNEL/.../TYPE/nnnnn.</summary>
public class DocumentPrevisionSequence
{
    public long IdSequence { get; set; }
    public short Annee { get; set; }
    public long IdDepartement { get; set; }
    public int NumeroVersion { get; set; }
    public string TypeDocument { get; set; } = string.Empty;
    public int DernierNumero { get; set; }
}
