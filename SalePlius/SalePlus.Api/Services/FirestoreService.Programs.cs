using Google.Cloud.Firestore;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

/// <summary>Why a program's schedule could not be saved.</summary>
public enum ScheduleError
{
    None,
    WorkoutNotFound,   // 400 – the request points at a workout that doesn't exist
    NotYourWorkout,    // 403 – you can only put your own workouts into a program
    InAnotherProgram   // 409 – a workout belongs to one program at a time
}

public record ScheduleResult(TrainingProgram? Program, ScheduleError Error = ScheduleError.None, string? Message = null);

/// <summary>
/// Training programs — the top of the hierarchy Program → Workout → Exercise entry.
/// A program owns its workouts (one-to-many): each workout stores "programId" and the week
/// "days" it runs on. The program's weekly schedule is built from those workouts when read.
/// </summary>
public partial class FirestoreService
{
    private CollectionReference Programs => _db.Collection("programs");
    private CollectionReference Workouts => _db.Collection("workouts");

    public async Task<List<TrainingProgram>> GetProgramsAsync(string? search, string? author, string? workoutId)
    {
        var programsTask = Programs.GetSnapshotAsync();
        // Only workouts that belong to some program (documents without "programId" are skipped by the query).
        var workoutsTask = Workouts.WhereGreaterThan("programId", "").GetSnapshotAsync();
        await Task.WhenAll(programsTask, workoutsTask);

        var workoutsByProgram = workoutsTask.Result.Documents.Select(MapDocumentToWorkout).ToLookup(w => w.ProgramId);
        var programs = programsTask.Result.Documents
            .Select(d => WithSchedule(MapDocumentToProgram(d), workoutsByProgram[d.Id]));

        if (!string.IsNullOrWhiteSpace(workoutId))
        {
            programs = programs.Where(p => p.Workouts.Any(w => w.WorkoutId == workoutId));
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            programs = programs.Where(p => p.CreatedByUserName.Contains(author, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            programs = programs.Where(p =>
                p.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return programs.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<TrainingProgram?> GetProgramByIdAsync(string id)
    {
        var snapshot = await Programs.Document(id).GetSnapshotAsync();
        if (!snapshot.Exists) return null;

        return WithSchedule(MapDocumentToProgram(snapshot), await GetProgramWorkoutsAsync(id) ?? new List<Workout>());
    }

    /// <summary>The workouts that belong to a program, or null when the program doesn't exist.</summary>
    public async Task<List<Workout>?> GetProgramWorkoutsAsync(string programId)
    {
        if (!(await Programs.Document(programId).GetSnapshotAsync()).Exists) return null;

        var snap = await Workouts.WhereEqualTo("programId", programId).GetSnapshotAsync();
        return snap.Documents.Select(MapDocumentToWorkout)
            .OrderBy(w => w.Days.DefaultIfEmpty(8).Min())
            .ThenBy(w => w.CreatedAt)
            .ToList();
    }

    /// <summary>Creates a new workout directly inside a program (POST /api/programs/{id}/workouts).</summary>
    public Task<Workout> CreateWorkoutInProgramAsync(string programId, CreateWorkoutDto dto)
    {
        dto.ProgramId = programId;
        return CreateWorkoutAsync(dto);
    }

    /// <param name="canUse">Whether the caller may put this workout into the program (owner or admin).</param>
    public async Task<ScheduleResult> CreateProgramAsync(CreateProgramDto dto, Func<Workout, bool> canUse)
    {
        var docRef = Programs.Document();
        var (plan, error, message) = await PlanScheduleAsync(docRef.Id, dto.Workouts, canUse);
        if (error != ScheduleError.None) return new ScheduleResult(null, error, message);

        await docRef.SetAsync(new Dictionary<string, object>
        {
            { "title", dto.Title.Trim() },
            { "description", dto.Description.Trim() },
            { "goal", string.IsNullOrWhiteSpace(dto.Goal) ? "Bendras" : dto.Goal.Trim() },
            { "difficulty", dto.Difficulty },
            { "durationWeeks", dto.DurationWeeks },
            { "createdByUserId", dto.CreatedByUserId },
            { "createdByUserName", dto.CreatedByUserName },
            { "createdAt", Timestamp.FromDateTime(DateTime.UtcNow) }
        });

        await ApplyScheduleAsync(docRef.Id, plan, detachOthers: false);
        return new ScheduleResult(await GetProgramByIdAsync(docRef.Id));
    }

    /// <summary>Returns null when the program does not exist.</summary>
    public async Task<ScheduleResult?> UpdateProgramAsync(string id, UpdateProgramDto dto, Func<Workout, bool> canUse)
    {
        var docRef = Programs.Document(id);
        if (!(await docRef.GetSnapshotAsync()).Exists) return null;

        var (plan, error, message) = await PlanScheduleAsync(id, dto.Workouts, canUse);
        if (error != ScheduleError.None) return new ScheduleResult(null, error, message);

        await docRef.UpdateAsync(new Dictionary<string, object>
        {
            { "title", dto.Title.Trim() },
            { "description", dto.Description.Trim() },
            { "goal", string.IsNullOrWhiteSpace(dto.Goal) ? "Bendras" : dto.Goal.Trim() },
            { "difficulty", dto.Difficulty },
            { "durationWeeks", dto.DurationWeeks },
            // Older versions stored the schedule on the program itself; it now lives on the workouts.
            { "workouts", FieldValue.Delete },
            { "workoutIds", FieldValue.Delete }
        });

        await ApplyScheduleAsync(id, plan, detachOthers: true);
        return new ScheduleResult(await GetProgramByIdAsync(id));
    }

    /// <summary>Deletes the program. Its workouts are kept and become standalone.</summary>
    public async Task<bool> DeleteProgramAsync(string id)
    {
        var docRef = Programs.Document(id);
        if (!(await docRef.GetSnapshotAsync()).Exists) return false;

        await DetachWorkoutsAsync(id);
        await docRef.DeleteAsync();
        return true;
    }

    private async Task DeleteProgramsByUserAsync(string userId)
    {
        var snapshot = await Programs.WhereEqualTo("createdByUserId", userId).GetSnapshotAsync();
        foreach (var doc in snapshot.Documents)
        {
            await DetachWorkoutsAsync(doc.Id);
            await doc.Reference.DeleteAsync();
        }
    }

    private async Task DetachWorkoutsAsync(string programId)
    {
        var snap = await Workouts.WhereEqualTo("programId", programId).GetSnapshotAsync();
        foreach (var doc in snap.Documents)
        {
            await doc.Reference.UpdateAsync(new Dictionary<string, object> { { "programId", "" }, { "days", new List<int>() } });
        }
    }

    /// <summary>
    /// Checks every workout in the requested schedule (exists, caller may use it, not in another program)
    /// and groups the requested days per workout. Nothing is written yet.
    /// </summary>
    private async Task<(Dictionary<string, List<int>> Plan, ScheduleError Error, string? Message)> PlanScheduleAsync(
        string programId, List<ProgramWorkoutItem>? items, Func<Workout, bool> canUse)
    {
        var plan = new Dictionary<string, List<int>>();
        foreach (var group in (items ?? new List<ProgramWorkoutItem>()).GroupBy(i => i.WorkoutId))
        {
            var workout = await GetWorkoutByIdAsync(group.Key);
            if (workout == null)
                return (plan, ScheduleError.WorkoutNotFound, $"Treniruotė su ID '{group.Key}' nerasta.");
            if (!canUse(workout))
                return (plan, ScheduleError.NotYourWorkout, $"Į programą galite įtraukti tik savo treniruotes ('{workout.Title}').");
            if (!string.IsNullOrEmpty(workout.ProgramId) && workout.ProgramId != programId)
                return (plan, ScheduleError.InAnotherProgram, $"Treniruotė '{workout.Title}' jau priklauso kitai programai.");

            plan[workout.Id] = NormalizeDays(group.Select(i => i.Day));
        }

        return (plan, ScheduleError.None, null);
    }

    /// <summary>Writes programId + days onto the planned workouts; optionally detaches workouts left out.</summary>
    private async Task ApplyScheduleAsync(string programId, Dictionary<string, List<int>> plan, bool detachOthers)
    {
        if (detachOthers)
        {
            var current = await Workouts.WhereEqualTo("programId", programId).GetSnapshotAsync();
            foreach (var doc in current.Documents.Where(d => !plan.ContainsKey(d.Id)))
            {
                await doc.Reference.UpdateAsync(new Dictionary<string, object> { { "programId", "" }, { "days", new List<int>() } });
            }
        }

        foreach (var (workoutId, days) in plan)
        {
            await Workouts.Document(workoutId).UpdateAsync(new Dictionary<string, object> { { "programId", programId }, { "days", days } });
        }
    }

    /// <summary>Distinct week days within 1–7, in order.</summary>
    private static List<int> NormalizeDays(IEnumerable<int>? days) =>
        (days ?? Enumerable.Empty<int>()).Where(d => d is >= 1 and <= 7).Distinct().OrderBy(d => d).ToList();

    /// <summary>Builds the weekly schedule view (one item per workout per day) from the program's workouts.</summary>
    private static TrainingProgram WithSchedule(TrainingProgram program, IEnumerable<Workout> workouts)
    {
        program.Workouts = workouts
            .SelectMany(w => w.Days.Select(day => new ProgramWorkoutItem
            {
                WorkoutId = w.Id,
                WorkoutTitle = w.Title,
                TargetMuscleGroups = w.TargetMuscleGroups,
                Day = day
            }))
            .OrderBy(i => i.Day)
            .ThenBy(i => i.WorkoutTitle)
            .ToList();
        return program;
    }

    private static TrainingProgram MapDocumentToProgram(DocumentSnapshot doc)
    {
        var dict = doc.ToDictionary();
        string Str(string key, string fallback = "") => dict.TryGetValue(key, out var v) ? v?.ToString() ?? fallback : fallback;

        return new TrainingProgram
        {
            Id = doc.Id,
            Title = Str("title"),
            Description = Str("description"),
            Goal = Str("goal", "Bendras"),
            Difficulty = Str("difficulty", "Intermediate"),
            DurationWeeks = dict.TryGetValue("durationWeeks", out var dw) ? Convert.ToInt32(dw) : 4,
            CreatedByUserId = Str("createdByUserId"),
            CreatedByUserName = Str("createdByUserName"),
            CreatedAt = dict.TryGetValue("createdAt", out var ca) && ca is Timestamp ts ? ts.ToDateTime() : (doc.CreateTime?.ToDateTime() ?? DateTime.UtcNow)
        };
    }
}
