using System.Text.Json.Serialization;

namespace SalePlius.Shared.Models;

/// <summary>
/// An allowed value for a classifying field, managed by administrators:
/// muscle groups (exercises, workouts), equipment (exercises) and goals (programs).
/// Other resources may only use names that exist here.
/// </summary>
public class Category
{
    public string Id { get; set; } = string.Empty;

    /// <summary>One of <see cref="CategoryTypes"/>.</summary>
    public string Type { get; set; } = CategoryTypes.MuscleGroup;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Hypermedia links, filled in by the API per request (not stored).</summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Link>? Links { get; set; }
}

public static class CategoryTypes
{
    public const string MuscleGroup = "MuscleGroup";
    public const string Equipment = "Equipment";
    public const string Goal = "Goal";

    /// <summary>For validation attributes.</summary>
    public const string Pattern = "^(MuscleGroup|Equipment|Goal)$";

    public static readonly string[] All = { MuscleGroup, Equipment, Goal };
}

/// <summary>Fixed difficulty levels (not a category: the app relies on exactly these three).</summary>
public static class Difficulties
{
    public const string Pattern = "^(Beginner|Intermediate|Advanced)$";
    public const string Message = "Sudėtingumas gali būti tik: Beginner, Intermediate arba Advanced";
}
