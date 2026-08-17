namespace BudgetWeb.Domain.Entities;

public class ExerciceBudgetaire
{
    public long IdExercice { get; set; }
    public short Annee { get; set; }
    public string Statut { get; set; } = string.Empty;
    public DateOnly? DateOuverture { get; set; }
    public DateOnly? DateCloture { get; set; }

    public ICollection<VersionBudgetaire> VersionsBudgetaires { get; set; } = new List<VersionBudgetaire>();
}
