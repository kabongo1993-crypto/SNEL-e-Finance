using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Entities;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class CompteImportRulesTests
{
    [Fact]
    public void AliasDevises_EuroEtFcfaVersCodesExistants()
    {
        var devises = new List<Devise>
        {
            new() { IdDevise = 1, Code = "EUR", Libelle = "Euro", Actif = true },
            new() { IdDevise = 2, Code = "CFA", Libelle = "Franc CFA", Actif = true },
            new() { IdDevise = 3, Code = "CDF", Libelle = "Franc congolais", Actif = true },
        };

        Assert.Equal("EUR", CompteImportRules.ResolveDevise("EURO", devises)!.Code);
        Assert.Equal("CFA", CompteImportRules.ResolveDevise("FCFA", devises)!.Code);
        Assert.Equal("CDF", CompteImportRules.ResolveDevise("CDF", devises)!.Code);
        Assert.Null(CompteImportRules.ResolveDevise("XYZ", devises));
    }

    [Fact]
    public void ParseIdCompte_AccepteEntierDecimalExcel()
    {
        Assert.Equal(29, CompteImportRules.ParseIdCompte("29"));
        Assert.Equal(29, CompteImportRules.ParseIdCompte("29.0"));
    }

    [Fact]
    public void ParseExcelDate_Serial36526_Est2000_01_01()
    {
        var dt = CompteImportRules.ParseExcelDate("36526");
        Assert.NotNull(dt);
        Assert.Equal(new DateTime(2000, 1, 1), dt!.Value.Date);
    }

    [Fact]
    public void EstDateSentinelle_Serial1()
    {
        Assert.True(CompteImportRules.EstDateSentinelle("1"));
        Assert.True(CompteImportRules.EstDateSentinelle(""));
        Assert.False(CompteImportRules.EstDateSentinelle("36526"));
    }

    [Fact]
    public void InterpretEtat_AccessHistorique_CompteActif()
    {
        var (actif, cloture) = CompteImportRules.InterpretEtat("0", "1");
        Assert.True(actif);
        Assert.Null(cloture);
    }

    [Fact]
    public void InterpretEtat_VraieCloture_CompteInactif()
    {
        var (actif, cloture) = CompteImportRules.InterpretEtat("0", "44927");
        Assert.False(actif);
        Assert.NotNull(cloture);
        Assert.True(cloture!.Value.Year >= 1990);
    }

    [Fact]
    public void ResolveTypeCompte_PrefixeEtLibelle()
    {
        var types = new List<TypeCompte>
        {
            new() { Code = "MAINTENANCE", Libelle = "Maintenance", Actif = true },
            new() { Code = "CAISSE", Libelle = "CAISSE DG", Actif = true },
            new() { Code = "FCT", Libelle = "Fonctionnement", Actif = true },
        };

        Assert.Equal("MAINTENANCE", CompteImportRules.ResolveTypeCompte("MAINTENANC", types)!.Code);
        Assert.Equal("CAISSE", CompteImportRules.ResolveTypeCompte("CAISSE DG", types)!.Code);
        Assert.Equal("FCT", CompteImportRules.ResolveTypeCompte("FCT", types)!.Code);
        Assert.Null(CompteImportRules.ResolveTypeCompte("INCONNU", types));
    }

    [Fact]
    public void ResolveBanque_ExactId()
    {
        var banques = new List<Banque>
        {
            new() { IdBanque = "RAWBANK", LibelleBanque = "Rawbank", Actif = true },
        };
        Assert.Equal("RAWBANK", CompteImportRules.ResolveBanque("RAWBANK", banques)!.IdBanque);
        Assert.Null(CompteImportRules.ResolveBanque("INCONNUE", banques));
    }

    [Fact]
    public void ResolveProvince_ExactId()
    {
        var provinces = new List<Province>
        {
            new() { IdProvince = "KIN", Libelle = "Kinshasa", Actif = true },
        };
        Assert.Equal("KIN", CompteImportRules.ResolveProvince("KIN", provinces)!.IdProvince);
        Assert.Null(CompteImportRules.ResolveProvince("ZZZ", provinces));
    }
}
