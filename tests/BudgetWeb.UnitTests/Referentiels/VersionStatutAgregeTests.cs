using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class VersionStatutAgregeTests
{
    [Theory]
    [InlineData(new[] { "REJETEE", "VALIDEE" }, StatutVersionBudgetaire.Rejetee)]
    [InlineData(new[] { "VALIDEE", "VALIDEE", "VALIDEE" }, StatutVersionBudgetaire.Validee)]
    [InlineData(new[] { "CONTROLEE", "CONTROLEE" }, StatutVersionBudgetaire.Controlee)]
    [InlineData(new[] { "CONTROLEE", "VALIDEE" }, StatutVersionBudgetaire.Controlee)]
    [InlineData(new[] { "SOUMISE", "SOUMISE" }, StatutVersionBudgetaire.Soumise)]
    [InlineData(new[] { "SOUMISE", "CONTROLEE", "VALIDEE" }, StatutVersionBudgetaire.Soumise)]
    [InlineData(new[] { "BROUILLON", "SOUMISE" }, StatutVersionBudgetaire.Brouillon)]
    [InlineData(new[] { "BROUILLON" }, StatutVersionBudgetaire.Brouillon)]
    public void Calculer_RespecteLaPriorite(string[] input, string attendu)
        => Assert.Equal(attendu, VersionStatutAgrege.Calculer(input));
}
