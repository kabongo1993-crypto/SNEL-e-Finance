namespace BudgetWeb.Domain.Entities;

public class VPrevisions
{
    public long IdPrevision { get; set; }
    public short Annee { get; set; }
    public long IdVersion { get; set; }
    public int NumeroVersion { get; set; }
    public string LibelleVersion { get; set; } = string.Empty;
    public string StatutVersion { get; set; } = string.Empty;
    public string CodeType { get; set; } = string.Empty;
    public string TypeBudget { get; set; } = string.Empty;
    public string CodeUB { get; set; } = string.Empty;
    public string LibelleUB { get; set; } = string.Empty;
    public string? CodeRB { get; set; }
    public string? LibelleRB { get; set; }
    public string? CodeItemBI { get; set; }
    public string? LibelleItemBI { get; set; }
    public string? GroupeItemAE { get; set; }
    public string? LibelleItemAE { get; set; }
    public string? DetailBI { get; set; }
    public string CodeMode { get; set; } = string.Empty;
    public string ModePrevision { get; set; } = string.Empty;
    public decimal MontantAnnuel { get; set; }
    public decimal MontantVentile { get; set; }
    public decimal? MontantNonVentile { get; set; }
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }
}
