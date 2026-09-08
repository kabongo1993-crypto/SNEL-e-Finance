namespace BudgetWeb.Domain.Entities;

/// <summary>
/// En-tête de périmètre de sécurité d'un utilisateur (dpm.PERIMETRE_UTILISATEUR).
/// « Tous » est représenté par un flag — pas une ligne par département/UB.
/// </summary>
public class PerimetreUtilisateur
{
    public long IdPerimetreUtilisateur { get; set; }
    public long FK_Utilisateur { get; set; }
    public bool TousDepartements { get; set; }
    public bool ToutesUnitesBudgetaires { get; set; }
    public DateTime DateModification { get; set; }

    public Utilisateur Utilisateur { get; set; } = null!;
    public ICollection<PerimetreDepartement> Departements { get; set; } = new List<PerimetreDepartement>();
    public ICollection<PerimetreUniteBudgetaire> UnitesBudgetaires { get; set; } = new List<PerimetreUniteBudgetaire>();
}

public class PerimetreDepartement
{
    public long IdPerimetreDepartement { get; set; }
    public long FK_PerimetreUtilisateur { get; set; }
    public long FK_Departement { get; set; }

    public PerimetreUtilisateur Perimetre { get; set; } = null!;
    public Departement Departement { get; set; } = null!;
}

public class PerimetreUniteBudgetaire
{
    public long IdPerimetreUniteBudgetaire { get; set; }
    public long FK_PerimetreUtilisateur { get; set; }
    public long FK_UniteBudgetaire { get; set; }

    public PerimetreUtilisateur Perimetre { get; set; } = null!;
    public UniteBudgetaire UniteBudgetaire { get; set; } = null!;
}
