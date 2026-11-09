using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

/// <summary>
/// Checks the category fields of incoming DTOs against the admin-managed categories and replaces
/// near-misses with the stored spelling ("krutine" → "Krūtinė"). Each method returns null when the
/// input is fine, or a message for a 400 response. Controllers call these right after model validation:
/// <c>if (await _service.CheckCategoriesAsync(dto) is { } problem) return BadRequest(new { message = problem });</c>
/// </summary>
public static class CategoryChecks
{
    public static async Task<string?> CheckCategoriesAsync(this IFirestoreService service, CreateExerciseDto dto)
    {
        var (main, e1) = await service.MatchCategoriesAsync(CategoryTypes.MuscleGroup, new[] { dto.MuscleGroup });
        if (e1 != null) return e1;
        var (secondary, e2) = await service.MatchCategoriesAsync(CategoryTypes.MuscleGroup, dto.SecondaryMuscleGroups);
        if (e2 != null) return e2;
        var (equipment, e3) = await service.MatchCategoriesAsync(CategoryTypes.Equipment, new[] { dto.Equipment });
        if (e3 != null) return e3;

        dto.MuscleGroup = main.Single();
        dto.SecondaryMuscleGroups = secondary.Where(s => s != dto.MuscleGroup).ToList();
        dto.Equipment = equipment.Single();
        return null;
    }

    public static async Task<string?> CheckCategoriesAsync(this IFirestoreService service, UpdateExerciseDto dto)
    {
        var (main, e1) = await service.MatchCategoriesAsync(CategoryTypes.MuscleGroup, new[] { dto.MuscleGroup });
        if (e1 != null) return e1;
        var (secondary, e2) = await service.MatchCategoriesAsync(CategoryTypes.MuscleGroup, dto.SecondaryMuscleGroups);
        if (e2 != null) return e2;
        var (equipment, e3) = await service.MatchCategoriesAsync(CategoryTypes.Equipment, new[] { dto.Equipment });
        if (e3 != null) return e3;

        dto.MuscleGroup = main.Single();
        dto.SecondaryMuscleGroups = secondary.Where(s => s != dto.MuscleGroup).ToList();
        dto.Equipment = equipment.Single();
        return null;
    }

    /// <summary>Workouts: target muscle groups must be categories, every exercise must exist in the library.</summary>
    public static async Task<string?> CheckCategoriesAsync(this IFirestoreService service, CreateWorkoutDto dto)
    {
        var (groups, error) = await service.MatchCategoriesAsync(CategoryTypes.MuscleGroup, dto.TargetMuscleGroups);
        if (error != null) return error;
        dto.TargetMuscleGroups = groups;
        return await service.CheckExerciseReferencesAsync(dto.Exercises);
    }

    public static async Task<string?> CheckCategoriesAsync(this IFirestoreService service, UpdateWorkoutDto dto)
    {
        var (groups, error) = await service.MatchCategoriesAsync(CategoryTypes.MuscleGroup, dto.TargetMuscleGroups);
        if (error != null) return error;
        dto.TargetMuscleGroups = groups;
        return await service.CheckExerciseReferencesAsync(dto.Exercises);
    }

    public static async Task<string?> CheckCategoriesAsync(this IFirestoreService service, CreateProgramDto dto)
    {
        var (goal, error) = await service.MatchCategoriesAsync(CategoryTypes.Goal, new[] { dto.Goal });
        if (error != null) return error;
        dto.Goal = goal.Single();
        return null;
    }

    public static async Task<string?> CheckCategoriesAsync(this IFirestoreService service, UpdateProgramDto dto)
    {
        var (goal, error) = await service.MatchCategoriesAsync(CategoryTypes.Goal, new[] { dto.Goal });
        if (error != null) return error;
        dto.Goal = goal.Single();
        return null;
    }
}
