using SalePlius.Shared.Models;

namespace SalePlius.Client.Services;

/// <summary>Sort options shared by the workout and exercise lists.</summary>
public static class WorkoutSort
{
    public const string Rating = "rating";
    public const string Newest = "newest";
    public const string Name = "name";

    public static IEnumerable<Workout> Apply(IEnumerable<Workout> items, string sort) => sort switch
    {
        Newest => items.OrderByDescending(w => w.CreatedAt),
        Name => items.OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase),
        // Highest average first; among equal averages, the one with more votes wins.
        _ => items.OrderByDescending(w => w.AverageRating).ThenByDescending(w => w.RatingsCount).ThenByDescending(w => w.CreatedAt)
    };

    public static IEnumerable<Exercise> Apply(IEnumerable<Exercise> items, string sort) => sort switch
    {
        Newest => items.OrderByDescending(e => e.CreatedAt),
        Name => items.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase),
        _ => items.OrderByDescending(e => e.Rating).ThenByDescending(e => e.RatingCount).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
    };
}
