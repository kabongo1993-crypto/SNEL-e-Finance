namespace BudgetWeb.Domain.Entities;

/// <summary>
/// Registre métier des paires de change canoniques (dpm.PAIRE_TAUX_CHANGE).
/// L'orientation (DeviseBase → DeviseQuote) est configurable sans redéploiement.
/// </summary>
public class PaireTauxChange
{
    public long IdPaireTauxChange { get; set; }
    public string DeviseBase { get; set; } = string.Empty;
    public string DeviseQuote { get; set; } = string.Empty;
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; }
}
