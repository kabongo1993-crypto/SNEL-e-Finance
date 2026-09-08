using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IBilletConversionDocumentRenderer
{
    byte[] Render(BilletConversionDocumentDto payload);
}
