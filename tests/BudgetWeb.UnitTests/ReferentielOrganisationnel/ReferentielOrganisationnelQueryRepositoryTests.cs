using BudgetWeb.Infrastructure.Repositories;
using Xunit;

namespace BudgetWeb.UnitTests.ReferentielOrganisationnel;

public class ReferentielOrganisationnelQueryRepositoryTests
{
    [Theory]
    [InlineData("AC", "AC")]
    [InlineData("E.AC.DP.DDI", "DDI")]
    [InlineData("E.DDK.DP.DEC.DV.GCC", "GCC")]
    [InlineData("E.DDK.DP.DEC.DV.GCE", "GCE")]
    public void ExtraireCodeAffichage_RetourneLeSegmentMetier(string technique, string attendu)
    {
        Assert.Equal(attendu, ReferentielOrganisationnelQueryRepository.ExtraireCodeAffichage(technique));
    }
}
