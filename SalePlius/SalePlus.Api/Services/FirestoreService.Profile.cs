using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

/// <summary>
/// The profile endpoint's data: one response assembled from several entities
/// (users, programs, workouts + their ratings, exercises, saved items).
/// </summary>
public partial class FirestoreService
{
    /// <param name="includePrivate">True for the user themselves or an admin: adds email and saved counts.</param>
    public async Task<UserProfileDetails?> GetUserProfileDetailsAsync(string userId, bool includePrivate)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null) return null;

        // Load the independent pieces in parallel.
        var programsTask = GetProgramsAsync(null, null, null);
        var workoutsTask = _db.Collection("workouts").WhereEqualTo("createdByUserId", userId).GetSnapshotAsync();
        var exercisesTask = _db.Collection("exercises").WhereEqualTo("createdByUserId", userId).GetSnapshotAsync();
        await Task.WhenAll(programsTask, workoutsTask, exercisesTask);

        var programs = programsTask.Result.Where(p => p.CreatedByUserId == userId).ToList();
        var workouts = workoutsTask.Result.Documents.Select(MapDocumentToWorkout).OrderByDescending(w => w.CreatedAt).ToList();

        // Average of all ratings on this user's workouts, weighted by how many ratings each has.
        var ratingsReceived = workouts.Sum(w => w.RatingsCount);
        var averageRating = ratingsReceived == 0
            ? 0
            : Math.Round(workouts.Sum(w => w.AverageRating * w.RatingsCount) / ratingsReceived, 2);

        if (!includePrivate)
        {
            user.Email = string.Empty;
        }

        var details = new UserProfileDetails
        {
            User = user,
            Stats = new ProfileStats
            {
                Programs = programs.Count,
                Workouts = workouts.Count,
                Exercises = exercisesTask.Result.Count,
                RatingsReceived = ratingsReceived,
                AverageRating = averageRating
            },
            Programs = programs.Take(6).ToList(),
            Workouts = workouts.Take(6).ToList()
        };

        if (includePrivate)
        {
            var savedWorkouts = await GetSavedWorkoutsForUserAsync(userId);
            var savedPrograms = await GetSavedItemsAsync(userId, SavedItemType.Program);
            var savedExercises = await GetSavedItemsAsync(userId, SavedItemType.Exercise);
            details.Saved = new SavedCounts
            {
                Programs = savedPrograms.Count,
                Workouts = savedWorkouts.Count,
                Exercises = savedExercises.Count
            };
        }

        return details;
    }
}
