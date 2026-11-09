using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

/// <summary>
/// Exercise entries inside a workout — the third level of Program → Workout → Exercise entry.
/// Entries are stored as an array on the workout document; each has its own ID so it can be
/// addressed as /api/programs/{p}/workouts/{w}/exercises/{entryId}.
/// </summary>
public partial class FirestoreService
{
    private static string NewEntryId() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>Serializes a workout's exercise list, giving any entry without an ID a new one.</summary>
    private static List<Dictionary<string, object>> ExercisesToFirestore(IEnumerable<WorkoutExerciseItem>? items) =>
        (items ?? Enumerable.Empty<WorkoutExerciseItem>()).Select(e =>
        {
            if (string.IsNullOrWhiteSpace(e.Id)) e.Id = NewEntryId();
            return new Dictionary<string, object>
            {
                { "id", e.Id },
                { "exerciseId", e.ExerciseId ?? string.Empty },
                { "exerciseName", e.ExerciseName ?? string.Empty },
                { "sets", e.Sets },
                { "reps", e.Reps },
                { "notes", e.Notes ?? string.Empty }
            };
        }).ToList();

    /// <summary>
    /// Entries sent with only an exerciseId (e.g. when creating a workout with its exercises in one request)
    /// get the exercise's name from the library, so lists and pages always have something to show.
    /// </summary>
    private async Task FillExerciseNamesAsync(IEnumerable<WorkoutExerciseItem>? items)
    {
        foreach (var item in (items ?? Enumerable.Empty<WorkoutExerciseItem>())
                     .Where(i => string.IsNullOrWhiteSpace(i.ExerciseName) && !string.IsNullOrWhiteSpace(i.ExerciseId)))
        {
            item.ExerciseName = (await GetExerciseByIdAsync(item.ExerciseId))?.Name ?? string.Empty;
        }
    }

    private Task SaveExercisesAsync(string workoutId, List<WorkoutExerciseItem> items) =>
        _db.Collection("workouts").Document(workoutId).UpdateAsync("exercises", ExercisesToFirestore(items));

    /// <summary>Adds an entry; returns the new entry, or null when the workout or library exercise doesn't exist.</summary>
    public async Task<WorkoutExerciseItem?> AddWorkoutEntryAsync(string workoutId, AddWorkoutExerciseDto dto)
    {
        var workout = await GetWorkoutByIdAsync(workoutId);
        var exercise = await GetExerciseByIdAsync(dto.ExerciseId);
        if (workout == null || exercise == null) return null;

        var entry = new WorkoutExerciseItem
        {
            Id = NewEntryId(),
            ExerciseId = exercise.Id,
            ExerciseName = exercise.Name,
            Sets = dto.Sets,
            Reps = dto.Reps,
            Notes = dto.Notes?.Trim() ?? string.Empty
        };

        workout.Exercises.Add(entry);
        await SaveExercisesAsync(workoutId, workout.Exercises);
        return entry;
    }

    /// <summary>Replaces an entry's exercise, sets, reps and notes. Null when the entry doesn't exist.</summary>
    public async Task<WorkoutExerciseItem?> UpdateWorkoutEntryAsync(string workoutId, string entryId, AddWorkoutExerciseDto dto)
    {
        var workout = await GetWorkoutByIdAsync(workoutId);
        var entry = workout?.Exercises.FirstOrDefault(e => e.Id == entryId);
        if (workout == null || entry == null) return null;

        var exercise = await GetExerciseByIdAsync(dto.ExerciseId);
        if (exercise == null) return null;

        entry.ExerciseId = exercise.Id;
        entry.ExerciseName = exercise.Name;
        entry.Sets = dto.Sets;
        entry.Reps = dto.Reps;
        entry.Notes = dto.Notes?.Trim() ?? string.Empty;

        await SaveExercisesAsync(workoutId, workout.Exercises);
        return entry;
    }

    public async Task<bool> DeleteWorkoutEntryAsync(string workoutId, string entryId)
    {
        var workout = await GetWorkoutByIdAsync(workoutId);
        if (workout == null || workout.Exercises.RemoveAll(e => e.Id == entryId) == 0) return false;

        await SaveExercisesAsync(workoutId, workout.Exercises);
        return true;
    }
}
