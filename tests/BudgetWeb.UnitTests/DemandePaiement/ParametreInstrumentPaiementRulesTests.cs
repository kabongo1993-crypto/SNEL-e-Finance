using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class ParametreInstrumentPaiementRulesTests
{
    [Fact]
    public void ExigerParametreComplet_SansLigne_Refuse()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ParametreInstrumentPaiementRules.ExigerParametreComplet(null, TypeInstrumentPaiement.PieceCaisse));
        Assert.Contains("pièce de caisse", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExigerParametreComplet_ChampsVides_Refuse()
    {
        var parametre = new ParametreInstrumentPaiement
        {
            TypeInstrument = TypeInstrumentPaiement.MinuteCheque,
            Actif = true,
            CompteGeneral = "TEST",
            CpCa = "",
            Ls = "TEST-LS",
            SuiviExtraComptable = "TEST-SEC",
            NumeroAppariement = "TEST-APP",
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ParametreInstrumentPaiementRules.ExigerParametreComplet(parametre, TypeInstrumentPaiement.MinuteCheque));
        Assert.Contains("minute de chèque", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExigerParametreComplet_MinuteCheque_MontantSuiviFacultatif()
    {
        var parametre = new ParametreInstrumentPaiement
        {
            TypeInstrument = TypeInstrumentPaiement.MinuteCheque,
            Actif = true,
            CompteGeneral = "TEST-CG",
            CpCa = "TEST-CPCA",
            Ls = "TEST-LS",
            SuiviExtraComptable = "TEST-SEC",
            NumeroAppariement = "TEST-APP",
            MontantSuiviExtraComptable = null,
        };

        var result = ParametreInstrumentPaiementRules.ExigerParametreComplet(
            parametre,
            TypeInstrumentPaiement.MinuteCheque);

        Assert.Equal("TEST-CG", result.CompteGeneral);
    }
}
