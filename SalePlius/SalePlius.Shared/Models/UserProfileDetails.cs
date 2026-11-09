using System.Text.Json.Serialization;

namespace SalePlius.Shared.Models;

/// <summary>
/// A user's profile page, built from several entities at once:
/// the user, their programs, workouts and exercises, ratings on their workouts and their saved items.
/// </summary>
public class UserProfileDetails
{
    public UserProfile User { get; set; } = new();
    public ProfileStats Stats { get; set; } = new();

    /// <summary>Newest programs the user created (up to 6).</summary>
    public List<TrainingProgram> Programs { get; set; } = new();

    /// <summary>Newest workouts the user created (up to 6).</summary>
    public List<Workout> Workouts { get; set; } = new();

    /// <summary>Only filled for the user themselves or an admin.</summary>
    public SavedCounts? Saved { get; set; }

    [JsonPropertyName("_links")]
    public List<Link> Links { get; set; } = new();
}

public class ProfileStats
{
    public int Programs { get; set; }
    public int Workouts { get; set; }
    public int Exercises { get; set; }

    /// <summary>Total ratings other people gave the user's workouts.</summary>
    public int RatingsReceived { get; set; }

    /// <summary>Average of those ratings (0 when there are none).</summary>
    public double AverageRating { get; set; }
}

public class SavedCounts
{
    public int Programs { get; set; }
    public int Workouts { get; set; }
    public int Exercises { get; set; }
}
