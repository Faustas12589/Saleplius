namespace SalePlus.Api.Services;

/// <summary>
/// The ?muscleGroup= filter on list endpoints. Several groups can be given, either repeated
/// (?muscleGroup=Krūtinė&amp;muscleGroup=Tricepsas) or comma-separated (?muscleGroup=Krūtinė,Tricepsas).
/// An item matches when it trains ALL of them.
/// </summary>
public static class MuscleFilter
{
    public static List<string> Parse(string[]? values) =>
        (values ?? [])
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>True when <paramref name="trained"/> contains every wanted group (case-insensitive).</summary>
    public static bool TrainsAll(this IEnumerable<string> trained, IReadOnlyCollection<string> wanted) =>
        wanted.Count == 0 || wanted.All(w => trained.Contains(w, StringComparer.OrdinalIgnoreCase));
}
