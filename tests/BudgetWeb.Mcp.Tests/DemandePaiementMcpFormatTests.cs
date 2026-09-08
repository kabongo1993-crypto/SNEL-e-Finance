using System.Text.Json;
using BudgetWeb.Mcp.Client;
using BudgetWeb.Mcp.Tools;
using Xunit;

namespace BudgetWeb.Mcp.Tests;

public sealed class DemandePaiementMcpFormatTests
{
    [Fact]
    public void Search_SurJsonPlusGrandQueTruncate_ResteExploitable()
    {
        var items = Enumerable.Range(1, 80).Select(i => new
        {
            idDemandePaiement = i,
            reference = $"DPM-{i:0000}",
            dateCreation = DateTime.UtcNow.AddDays(-i),
            objet = new string('x', 400) + i,
            montantBrut = 1000 + i,
            devise = "USD",
            codeUB = "UB10",
            libelleUB = "Test",
            statut = "BROUILLON"
        });
        var json = JsonSerializer.Serialize(items);
        Assert.True(json.Length > 24_000);

        var truncated = BudgetWebApiClient.Truncate(json);
        Assert.Contains("tronquée", truncated, StringComparison.Ordinal);
        Assert.True(truncated.Length < json.Length);

        var compact = DemandePaiementMcpFormat.Search(json, 0, 5);
        using var doc = JsonDocument.Parse(compact);
        Assert.Equal(80, doc.RootElement.GetProperty("pagination").GetProperty("total").GetInt32());
        Assert.Equal(5, doc.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(1, doc.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.True(compact.Length < 24_000);
    }

    [Fact]
    public void Search_PagesSuccessives_SontDistinctesEtTriesDesc()
    {
        var items = Enumerable.Range(1, 12).Select(i => new
        {
            idDemandePaiement = i,
            reference = $"DPM-{i}",
            dateCreation = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i),
            montantBrut = i,
            devise = "USD",
            statut = "BROUILLON"
        });
        var json = JsonSerializer.Serialize(items);
        using var p1 = JsonDocument.Parse(DemandePaiementMcpFormat.Search(json, 0, 3));
        using var p2 = JsonDocument.Parse(DemandePaiementMcpFormat.Search(json, 3, 3));
        var ids1 = p1.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();
        var ids2 = p2.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();
        Assert.Equal(new long[] { 12, 11, 10 }, ids1);
        Assert.Equal(new long[] { 9, 8, 7 }, ids2);
        Assert.Empty(ids1.Intersect(ids2));
    }

    [Fact]
    public void Latest_Vide_FoundFalse()
    {
        var json = DemandePaiementMcpFormat.Latest("[]", null);
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("found").GetBoolean());
    }

    [Fact]
    public void Latest_PrendDateCreationLaPlusRecente()
    {
        var json = """
        [
          {"idDemandePaiement":2,"reference":"DPM-2","dateCreation":"2026-01-01T00:00:00Z","montantBrut":1,"devise":"USD","statut":"BROUILLON"},
          {"idDemandePaiement":9,"reference":"DPM-9","dateCreation":"2026-09-08T15:00:00Z","montantBrut":50,"devise":"USD","codeUB":"UB1","libelleUB":"Siège","statut":"SOUMISE"}
        ]
        """;
        var detail = """{"beneficiaires":[{"nomComplet":"Jean Nkulu","estPrincipal":true}]}""";
        using var doc = JsonDocument.Parse(DemandePaiementMcpFormat.Latest(json, detail));
        Assert.True(doc.RootElement.GetProperty("found").GetBoolean());
        var dpm = doc.RootElement.GetProperty("demandePaiement");
        Assert.Equal(9, dpm.GetProperty("id").GetInt64());
        Assert.Equal("DPM-9", dpm.GetProperty("reference").GetString());
        Assert.Equal("Jean Nkulu", dpm.GetProperty("beneficiaire").GetString());
        Assert.Contains("2026-09-08", dpm.GetProperty("dateEnregistrement").GetString(), StringComparison.Ordinal);
        Assert.Equal("Siège", dpm.GetProperty("uniteBudgetaire").GetString()!.Split("—").Last().Trim());
    }

    [Fact]
    public void Latest_MessageApiNonJson_EstRenvoyeTelQuel()
    {
        var msg = "Vous n'avez pas accès à cette unité budgétaire.";
        Assert.Equal(msg, DemandePaiementMcpFormat.Latest(msg, null));
        Assert.Equal(msg, DemandePaiementMcpFormat.Search(msg, 0, 5));
    }
}
