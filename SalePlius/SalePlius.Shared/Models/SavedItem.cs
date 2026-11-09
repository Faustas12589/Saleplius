namespace SalePlius.Shared.Models;

/// <summary>A program or exercise the user bookmarked. (Saved workouts keep their own SavedWorkout model.)</summary>
public class SavedItem
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    /// <summary>"Program" or "Exercise".</summary>
    public string ItemType { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}
