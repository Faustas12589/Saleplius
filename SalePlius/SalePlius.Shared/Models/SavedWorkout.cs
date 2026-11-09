namespace SalePlius.Shared.Models;

public class SavedWorkout
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string WorkoutId { get; set; } = string.Empty;
    public string WorkoutTitle { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}
