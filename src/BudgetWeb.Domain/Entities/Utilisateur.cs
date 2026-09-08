namespace BudgetWeb.Domain.Entities;

public class Utilisateur
{
    public long IdUtilisateur { get; set; }
    public string Matricule { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string? Postnom { get; set; }
    public string? Prenom { get; set; }
    public string NomUtilisateur { get; set; } = string.Empty;
    public string MotDePasseHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool Actif { get; set; }
    public DateTime DateCreation { get; set; }
    public DateTime? DateDerniereConnexion { get; set; }

    /// <summary>Affectation administrative — Structure / Entité (≠ périmètre sécurité).</summary>
    public long? FK_StructureOrganisationnelle { get; set; }

    /// <summary>Affectation administrative — Département principal (≠ périmètre sécurité).</summary>
    public long? FK_DepartementPrincipal { get; set; }

    /// <summary>Affectation administrative — Service (structure de type Service).</summary>
    public long? FK_StructureService { get; set; }

    public StructureOrganisationnelle? StructureOrganisationnelle { get; set; }
    public Departement? DepartementPrincipal { get; set; }
    public StructureOrganisationnelle? StructureService { get; set; }

    public ICollection<Autorisation> Autorisations { get; set; } = new List<Autorisation>();
    public ICollection<ProfilUtilisateur> Profils { get; set; } = new List<ProfilUtilisateur>();
    public ICollection<PermissionUtilisateur> PermissionsIndividuelles { get; set; } = new List<PermissionUtilisateur>();
    public PerimetreUtilisateur? Perimetre { get; set; }
    public ICollection<JournalAudit> JournalAudits { get; set; } = new List<JournalAudit>();
    public ICollection<PrevisionBudgetaire> PrevisionsCreees { get; set; } = new List<PrevisionBudgetaire>();
    public ICollection<PrevisionBudgetaire> PrevisionsModifiees { get; set; } = new List<PrevisionBudgetaire>();
    public ICollection<VersionBudgetaire> VersionsCreees { get; set; } = new List<VersionBudgetaire>();
    public ICollection<VersionBudgetaire> VersionsValidees { get; set; } = new List<VersionBudgetaire>();
    public ICollection<VersionBudgetaire> VersionsSoumises { get; set; } = new List<VersionBudgetaire>();
    public ICollection<VersionBudgetaire> VersionsControlees { get; set; } = new List<VersionBudgetaire>();
    public ICollection<VersionBudgetaire> VersionsRejetees { get; set; } = new List<VersionBudgetaire>();
    public ICollection<OperationReduction> OperationsReductionCreees { get; set; } = new List<OperationReduction>();
    public ICollection<TransfertBudgetaire> TransfertsCrees { get; set; } = new List<TransfertBudgetaire>();
}
