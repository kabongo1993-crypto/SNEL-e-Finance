using System.Text.Json;
using BudgetWeb.Mcp.Security;

namespace BudgetWeb.Mcp.Tools;

public static class JsonSlice
{
    public static string Paginate(string json, int skip, int take, out int total, out int returned)
    {
        total = 0;
        returned = 0;
        skip = ParameterGuard.ClampSkip(skip);
        take = ParameterGuard.ClampTake(take);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
                return SliceArray(root, skip, take, out total, out returned);

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "lignes", "Lignes", "items", "Items" })
                {
                    if (root.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        var sliced = SliceArray(arr, skip, take, out total, out returned);
                        using var slicedDoc = JsonDocument.Parse(sliced);
                        var output = new Dictionary<string, JsonElement>();
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (string.Equals(prop.Name, name, StringComparison.Ordinal))
                                continue;
                            output[prop.Name] = prop.Value.Clone();
                        }

                        return JsonSerializer.Serialize(new
                        {
                            pagination = new { skip, take, total, returned },
                            extra = output,
                            lignes = JsonSerializer.Deserialize<JsonElement>(sliced)
                        });
                    }
                }
            }
        }
        catch (JsonException)
        {
            returned = 0;
            return json;
        }

        returned = 1;
        return json;
    }

    private static string SliceArray(JsonElement array, int skip, int take, out int total, out int returned)
    {
        var list = array.EnumerateArray().Select(e => e.Clone()).ToList();
        total = list.Count;
        var page = list.Skip(skip).Take(take).ToList();
        returned = page.Count;
        return JsonSerializer.Serialize(new
        {
            pagination = new { skip, take, total, returned },
            items = page
        });
    }
}
