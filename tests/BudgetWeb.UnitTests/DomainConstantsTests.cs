using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests;

public class DomainConstantsTests
{
    [Fact]
    public void TypeBudgetCode_ContientLesTroisTypesValides()
    {
        Assert.Equal("DC", TypeBudgetCode.DepensesCourantes);
        Assert.Equal("AE", TypeBudgetCode.ActionsExploitation);
        Assert.Equal("BI", TypeBudgetCode.BudgetInvestissement);
    }

    [Fact]
    public void ModePrevisionCode_ContientLesDeuxModesValides()
    {
        Assert.Equal("ANNUEL", ModePrevisionCode.Annuel);
        Assert.Equal("MENSUEL", ModePrevisionCode.Mensuel);
    }
}
