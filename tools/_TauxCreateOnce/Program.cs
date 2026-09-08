using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

var factory = new WebApplicationFactory<Program>();
await using var scope = factory.Services.CreateAsyncScope();
var authRepo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
var utilisateur = await authRepo.FindByIdAsync(4)
    ?? throw new Exception("User 4 introuvable");
var profils = await authRepo.ListProfilsAsync(utilisateur.IdUtilisateur);
var individuelles = await authRepo.ListPermissionsIndividuellesAsync(utilisateur.IdUtilisateur);
var authUser = AuthService.MapUser(utilisateur, profils, individuelles);
var (token, _) = tokenService.CreateToken(authUser);
Console.WriteLine("USER=" + utilisateur.NomUtilisateur);

using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5257") };
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// List current EUR related rates
var list = await client.GetAsync("/api/v1/taux-change?deviseBase=USD&deviseQuote=EUR");
Console.WriteLine("LIST_STATUS=" + (int)list.StatusCode);
Console.WriteLine("LIST=" + await list.Content.ReadAsStringAsync());

var list2 = await client.GetAsync("/api/v1/taux-change?deviseBase=EUR&deviseQuote=USD");
Console.WriteLine("LIST2=" + await list2.Content.ReadAsStringAsync());

var paires = await client.GetAsync("/api/v1/taux-change/paires");
Console.WriteLine("PAIRES=" + await paires.Content.ReadAsStringAsync());

var body = new {
  deviseBase = "EUR",
  deviseQuote = "USD",
  tauxReference = 1.245m,
  dateEffet = "2026-09-03"
};
var resp = await client.PostAsJsonAsync("/api/v1/taux-change", body);
var text = await resp.Content.ReadAsStringAsync();
Console.WriteLine("CREATE_STATUS=" + (int)resp.StatusCode);
Console.WriteLine("CREATE=" + text);

var appl = await client.GetAsync("/api/v1/taux-change/applicable?deviseSource=EUR&deviseCible=USD&dateReference=2026-09-03");
Console.WriteLine("APPLICABLE_STATUS=" + (int)appl.StatusCode);
Console.WriteLine("APPLICABLE=" + await appl.Content.ReadAsStringAsync());