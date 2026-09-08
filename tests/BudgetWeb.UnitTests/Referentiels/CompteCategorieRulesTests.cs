using BudgetWeb.Application.Services;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class CompteCategorieRulesTests
{
    [Fact]
    public void Periodes_Adjacentes_NeSeChevauchentPas()
    {
        Assert.False(CompteCategorieRules.PeriodesSeChevauchent(
            new DateOnly(2020, 1, 1), new DateOnly(2024, 12, 31),
            new DateOnly(2025, 1, 1), null));
    }

    [Fact]
    public void Periodes_QuiSeChevauchent_SontDetectees()
    {
        Assert.True(CompteCategorieRules.PeriodesSeChevauchent(
            new DateOnly(2020, 1, 1), new DateOnly(2024, 12, 31),
            new DateOnly(2023, 1, 1), new DateOnly(2025, 12, 31)));
    }

    [Fact]
    public void DeuxActives_SeChevauchent()
    {
        Assert.True(CompteCategorieRules.PeriodesSeChevauchent(
            new DateOnly(2020, 1, 1), null,
            new DateOnly(2025, 1, 1), null));
    }

    [Fact]
    public void DateFin_Anterieure_EstInvalide()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            CompteCategorieRules.ValiderPeriode(new DateOnly(2025, 1, 1), new DateOnly(2024, 12, 1)));
        Assert.Contains("fin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("1900-01-01")]
    [InlineData("1")]
    [InlineData("01/01/1900")]
    public void DateFin_19000101_EstSentinelle(string raw)
    {
        Assert.True(CompteCategorieRules.EstDateFinSentinelle1900(raw));
        Assert.Null(CompteCategorieRules.ParseDateFinImport(raw, out var converted));
        Assert.True(converted);
    }

    [Fact]
    public void DateFin_Vide_NEstPasCompteeCommeSentinelle()
    {
        Assert.False(CompteCategorieRules.EstDateFinSentinelle1900(""));
        Assert.Null(CompteCategorieRules.ParseDateFinImport("", out var converted));
        Assert.False(converted);
    }

    [Fact]
    public void DateFin_Reelle_EstConservee()
    {
        Assert.False(CompteCategorieRules.EstDateFinSentinelle1900("2019-12-31"));
        Assert.Equal(new DateOnly(2019, 12, 31), CompteCategorieRules.ParseDateFinImport("2019-12-31", out var converted));
        Assert.False(converted);
    }
}
