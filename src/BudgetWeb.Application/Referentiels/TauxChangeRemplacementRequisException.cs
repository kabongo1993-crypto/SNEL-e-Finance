using BudgetWeb.Application.DTOs.Referentiels;

namespace BudgetWeb.Application.Referentiels;

public sealed class TauxChangeRemplacementRequisException : Exception
{
    public TauxChangeRemplacementProposeDto Proposition { get; }

    public TauxChangeRemplacementRequisException(TauxChangeRemplacementProposeDto proposition)
        : base(proposition.Message)
    {
        Proposition = proposition;
    }
}