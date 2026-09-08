namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Conflit de concurrence sur une demande de paiement (statut modifié entre-temps).</summary>
public sealed class DemandePaiementConcurrencyException : InvalidOperationException
{
    public const string MessageUtilisateur =
        "La demande a été modifiée entre-temps. Actualisez la demande avant de poursuivre.";

    public DemandePaiementConcurrencyException()
        : base(MessageUtilisateur)
    {
    }

    public DemandePaiementConcurrencyException(string message)
        : base(message)
    {
    }
}
