using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IPieceCaisseDocumentRenderer
{
    byte[] Render(PieceCaisseDocumentDto payload);
}

public interface IBonProvisoireDocumentRenderer
{
    byte[] Render(BonProvisoireDocumentDto payload);
}

public interface IMinuteChequeDocumentRenderer
{
    byte[] Render(MinuteChequeDocumentDto payload);
}
