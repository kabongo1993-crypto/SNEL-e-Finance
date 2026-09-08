using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

/// <summary>Rendu PDF minimal d'une demande de paiement (document provisoire).</summary>
public interface IDemandePaiementDocumentRenderer
{
    byte[] Render(DemandePaiementDocumentDto payload);
}
