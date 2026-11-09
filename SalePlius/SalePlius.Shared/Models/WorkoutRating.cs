namespace SalePlius.Shared.Models;

public class WorkoutRating
{
    public string Id { get; set; } = string.Empty;
    public string WorkoutId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int Score { get; set; } = 5; // 1 to 5
    public string Review { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
