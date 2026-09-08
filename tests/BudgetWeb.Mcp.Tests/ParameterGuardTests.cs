using BudgetWeb.Mcp.Client;
using BudgetWeb.Mcp.Security;
using Xunit;

namespace BudgetWeb.Mcp.Tests;

public class ParameterGuardTests
{
    [Fact]
    public void ClampTake_BorneA50()
    {
        Assert.Equal(50, ParameterGuard.ClampTake(500));
        Assert.Equal(1, ParameterGuard.ClampTake(0));
        Assert.Equal(20, ParameterGuard.ClampTake(null));
    }

    [Fact]
    public void SanitizeSearch_RejetteSql()
    {
        Assert.Throws<ArgumentException>(() => ParameterGuard.SanitizeSearch("SELECT * FROM UTILISATEUR"));
        Assert.Throws<ArgumentException>(() => ParameterGuard.SanitizeSearch("1; DROP TABLE X"));
    }

    [Fact]
    public void SanitizeSearch_AccepteTexteMetier()
    {
        Assert.Equal("DPM-2026-001", ParameterGuard.SanitizeSearch("DPM-2026-001"));
    }

    [Fact]
    public void RequirePositive_RejetteNul()
    {
        Assert.Throws<ArgumentException>(() => ParameterGuard.RequirePositive(null, "idUB"));
        Assert.Throws<ArgumentException>(() => ParameterGuard.RequirePositive(0, "idUB"));
    }
}

public class ApiErrorMapperTests
{
    [Fact]
    public void MasqueSqlEtStack()
    {
        Assert.Equal("Erreur interne Budget Web.", ApiErrorMapper.Sanitize("SqlException SELECT * FROM"));
        Assert.Equal("Erreur interne Budget Web.", ApiErrorMapper.Sanitize("at Foo.Bar() in C:\\src\\File.cs:line 12"));
        Assert.Equal("Erreur interne Budget Web.", ApiErrorMapper.Sanitize("Bearer eyJhbGciOiJIUzI1NiJ9.aaa.bbb"));
        Assert.Equal("Erreur interne Budget Web.", ApiErrorMapper.Sanitize("Jwt:SecretKey leaked"));
        Assert.Equal("Erreur interne Budget Web.", ApiErrorMapper.Sanitize("password=secret"));
    }

    [Fact]
    public void LitMessageJsonPascalOuCamel()
    {
        var camel = ApiErrorMapper.ToUserMessage(System.Net.HttpStatusCode.Unauthorized, """{"message":"Jeton invalide."}""");
        var pascal = ApiErrorMapper.ToUserMessage(System.Net.HttpStatusCode.Unauthorized, """{"Message":"Compte désactivé."}""");
        Assert.Equal("Jeton invalide.", camel);
        Assert.Equal("Compte désactivé.", pascal);
    }
}
