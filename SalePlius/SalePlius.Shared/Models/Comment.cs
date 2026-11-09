namespace SalePlius.Shared.Models;

public class Comment
{
    public string Id { get; set; } = string.Empty;
    public string ExerciseId { get; set; } = string.Empty;
    public string WorkoutId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
