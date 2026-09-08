namespace BudgetWeb.Domain.Entities;

/// <summary>
/// Paramétrage courant des valeurs comptables pour les documents instrument de paiement.
/// Les documents établis figent ces valeurs — ne pas reconstruire depuis ce paramétrage.
/// </summary>
public class ParametreInstrumentPaiement
{
    public long IdParametreInstrumentPaiement { get; set; }
    public string TypeInstrument { get; set; } = string.Empty;

    // Pièce de caisse
    public string? Sr { get; set; }
    public string? ComptabiliteGenerale { get; set; }
    public string? Cp { get; set; }
    public string? Cpa { get; set; }

    // Bon provisoire / minute (compte général partagé sémantiquement)
    public string? CompteGeneral { get; set; }
    public string? CompteParticulier { get; set; }

    // Minute de chèque
    public string? CpCa { get; set; }
    public string? Ls { get; set; }
    public string? SuiviExtraComptable { get; set; }
    public decimal? MontantSuiviExtraComptable { get; set; }

    // Commun
    public string? NumeroAppariement { get; set; }
    public string? RecuInstitutionnel { get; set; }

    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }
    public long FK_UtilisateurCreation { get; set; }
    public long? FK_UtilisateurModification { get; set; }
}
