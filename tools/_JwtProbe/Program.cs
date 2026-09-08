using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

static class Program
{
    static int Main()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("DEV-ONLY-BudgetWeb-SNEL-JwtSigningKey-ChangeInProd-64chars!!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new Claim[]
        {
            new(JwtRegisteredClaimNames.Sub, "4"),
            new("uid", "4"),
            new("uname", "admin.snel"),
            new(ClaimTypes.NameIdentifier, "4"),
            new(ClaimTypes.Name, "admin.snel"),
            new(ClaimTypes.Role, "User Admin Full"),
            new("permission", "admin.all"),
            new("permission", "ajustements.lire"),
            new("permission", "ajustements.ecrire"),
            new("permission", "ajustements.valider"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        var token = new JwtSecurityToken(
            "BudgetWeb-SNEL",
            "BudgetWeb-Client",
            claims,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddHours(2),
            creds);
        Console.Out.Write(new JwtSecurityTokenHandler().WriteToken(token));
        return 0;
    }
}
