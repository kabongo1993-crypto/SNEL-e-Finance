namespace BudgetWeb.Domain.Entities;

public class RepartitionMensuelle
{
    public long IdRepartition { get; set; }
    public long FK_PrevisionBudgetaire { get; set; }
    public byte Mois { get; set; }
    public decimal Montant { get; set; }

    public PrevisionBudgetaire PrevisionBudgetaire { get; set; } = null!;
}
