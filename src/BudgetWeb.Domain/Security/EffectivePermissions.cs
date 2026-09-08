namespace BudgetWeb.Domain.Security;

/// <summary>Calcul des permissions effectives (profils + individuelles).</summary>
public static class EffectivePermissions
{
    public static IReadOnlyList<string> FromProfils(IEnumerable<string> profils)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in profils)
        {
            foreach (var p in AppPermissions.PermissionsPourProfil(code))
                set.Add(p);
        }

        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<string> Merge(IEnumerable<string> fromProfils, IEnumerable<string> individuelles)
    {
        var set = new HashSet<string>(fromProfils, StringComparer.OrdinalIgnoreCase);
        foreach (var p in individuelles)
        {
            if (!string.IsNullOrWhiteSpace(p))
                set.Add(p.Trim());
        }

        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<string> Compute(
        IEnumerable<string> profils,
        IEnumerable<string>? permissionsIndividuelles = null)
        => Merge(FromProfils(profils), permissionsIndividuelles ?? []);
}
