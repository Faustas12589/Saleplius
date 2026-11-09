using System.Globalization;
using System.Text;
using Google.Cloud.Firestore;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

/// <summary>Why a category change was refused.</summary>
public enum CategoryError
{
    None,
    NotFound,   // 404
    Duplicate,  // 409 – that name already exists in this type
    InUse       // 409 – still used by exercises/workouts/programs
}

public record CategoryResult(Category? Category, CategoryError Error = CategoryError.None, string? Message = null);

/// <summary>
/// Admin-managed categories (muscle groups, equipment, program goals) and the checks that make
/// every other resource use only those names.
/// </summary>
public partial class FirestoreService
{
    private CollectionReference Categories => _db.Collection("categories");

    public async Task<List<Category>> GetCategoriesAsync(string? type)
    {
        Query query = Categories;
        if (!string.IsNullOrWhiteSpace(type)) query = query.WhereEqualTo("type", type);

        var snap = await query.GetSnapshotAsync();
        return snap.Documents.Select(MapDocumentToCategory)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name, StringComparer.Create(new CultureInfo("lt-LT"), true))
            .ToList();
    }

    public async Task<Category?> GetCategoryByIdAsync(string id)
    {
        var doc = await Categories.Document(id).GetSnapshotAsync();
        return doc.Exists ? MapDocumentToCategory(doc) : null;
    }

    public async Task<CategoryResult> CreateCategoryAsync(CreateCategoryDto dto)
    {
        var name = dto.Name.Trim();
        if (await FindCategoryAsync(dto.Type, name) is { } existing)
        {
            return new CategoryResult(null, CategoryError.Duplicate, $"Kategorija „{existing.Name}“ jau egzistuoja.");
        }

        var docRef = Categories.Document();
        await docRef.SetAsync(new Dictionary<string, object>
        {
            { "type", dto.Type },
            { "name", name },
            { "description", dto.Description?.Trim() ?? string.Empty },
            { "createdAt", Timestamp.FromDateTime(DateTime.UtcNow) }
        });
        return new CategoryResult(MapDocumentToCategory(await docRef.GetSnapshotAsync()));
    }

    /// <summary>Renaming also updates every exercise, workout and program that uses the old name.</summary>
    public async Task<CategoryResult> UpdateCategoryAsync(string id, UpdateCategoryDto dto)
    {
        var current = await GetCategoryByIdAsync(id);
        if (current == null) return new CategoryResult(null, CategoryError.NotFound);

        var newName = dto.Name.Trim();
        if (await FindCategoryAsync(current.Type, newName) is { } clash && clash.Id != id)
        {
            return new CategoryResult(null, CategoryError.Duplicate, $"Kategorija „{clash.Name}“ jau egzistuoja.");
        }

        await Categories.Document(id).UpdateAsync(new Dictionary<string, object>
        {
            { "name", newName },
            { "description", dto.Description?.Trim() ?? string.Empty }
        });

        if (newName != current.Name)
        {
            await RenameCategoryUsagesAsync(current.Type, current.Name, newName);
        }

        return new CategoryResult(await GetCategoryByIdAsync(id));
    }

    /// <summary>A category still in use cannot be deleted (409): rename it or change those items first.</summary>
    public async Task<CategoryResult> DeleteCategoryAsync(string id)
    {
        var current = await GetCategoryByIdAsync(id);
        if (current == null) return new CategoryResult(null, CategoryError.NotFound);

        var uses = await CountCategoryUsagesAsync(current.Type, current.Name);
        if (uses > 0)
        {
            return new CategoryResult(current, CategoryError.InUse, $"Kategorija „{current.Name}“ naudojama {uses} kartų(-us), jos ištrinti negalima.");
        }

        await Categories.Document(id).DeleteAsync();
        return new CategoryResult(current);
    }

    /// <summary>
    /// Checks names against the categories of one type. A name that differs only by case or accents
    /// ("krutine") is corrected to the stored one ("Krūtinė"); anything else is an error listing the allowed names.
    /// </summary>
    public async Task<(List<string> Names, string? Error)> MatchCategoriesAsync(string type, IEnumerable<string?>? names)
    {
        var allowed = await GetCategoriesAsync(type);
        var result = new List<string>();

        foreach (var raw in names ?? Enumerable.Empty<string?>())
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var match = allowed.FirstOrDefault(c => Fold(c.Name) == Fold(raw));
            if (match == null)
            {
                return (result, $"„{raw.Trim()}“ nėra leidžiama reikšmė ({TypeLabel(type)}). Galimos: {string.Join(", ", allowed.Select(c => c.Name))}.");
            }
            if (!result.Contains(match.Name)) result.Add(match.Name);
        }

        return (result, null);
    }

    /// <summary>Every exercise referenced by a workout must exist in the library.</summary>
    public async Task<string?> CheckExerciseReferencesAsync(IEnumerable<WorkoutExerciseItem>? items)
    {
        foreach (var id in (items ?? Enumerable.Empty<WorkoutExerciseItem>()).Select(i => i.ExerciseId).Distinct())
        {
            if (await GetExerciseByIdAsync(id) == null) return $"Pratimas su ID '{id}' bibliotekoje nerastas.";
        }
        return null;
    }

    private async Task<Category?> FindCategoryAsync(string type, string name) =>
        (await GetCategoriesAsync(type)).FirstOrDefault(c => Fold(c.Name) == Fold(name));

    private async Task<int> CountCategoryUsagesAsync(string type, string name)
    {
        switch (type)
        {
            case CategoryTypes.MuscleGroup:
            {
                var main = await _db.Collection("exercises").WhereEqualTo("muscleGroup", name).GetSnapshotAsync();
                var secondary = await _db.Collection("exercises").WhereArrayContains("secondaryMuscleGroups", name).GetSnapshotAsync();
                var workouts = await _db.Collection("workouts").WhereArrayContains("targetMuscleGroups", name).GetSnapshotAsync();
                return main.Count + secondary.Count + workouts.Count;
            }
            case CategoryTypes.Equipment:
                return (await _db.Collection("exercises").WhereEqualTo("equipment", name).GetSnapshotAsync()).Count;
            case CategoryTypes.Goal:
                return (await Programs.WhereEqualTo("goal", name).GetSnapshotAsync()).Count;
            default:
                return 0;
        }
    }

    private async Task RenameCategoryUsagesAsync(string type, string oldName, string newName)
    {
        List<string> Swap(IEnumerable<object> list) => list.Select(x => x?.ToString() == oldName ? newName : x?.ToString() ?? "").Distinct().ToList();

        if (type == CategoryTypes.MuscleGroup)
        {
            foreach (var doc in (await _db.Collection("exercises").WhereEqualTo("muscleGroup", oldName).GetSnapshotAsync()).Documents)
                await doc.Reference.UpdateAsync("muscleGroup", newName);

            foreach (var doc in (await _db.Collection("exercises").WhereArrayContains("secondaryMuscleGroups", oldName).GetSnapshotAsync()).Documents)
                await doc.Reference.UpdateAsync("secondaryMuscleGroups", Swap(doc.GetValue<List<object>>("secondaryMuscleGroups")));

            foreach (var doc in (await _db.Collection("workouts").WhereArrayContains("targetMuscleGroups", oldName).GetSnapshotAsync()).Documents)
                await doc.Reference.UpdateAsync("targetMuscleGroups", Swap(doc.GetValue<List<object>>("targetMuscleGroups")));
        }
        else if (type == CategoryTypes.Equipment)
        {
            foreach (var doc in (await _db.Collection("exercises").WhereEqualTo("equipment", oldName).GetSnapshotAsync()).Documents)
                await doc.Reference.UpdateAsync("equipment", newName);
        }
        else if (type == CategoryTypes.Goal)
        {
            foreach (var doc in (await Programs.WhereEqualTo("goal", oldName).GetSnapshotAsync()).Documents)
                await doc.Reference.UpdateAsync("goal", newName);
        }
    }

    private static string TypeLabel(string type) => type switch
    {
        CategoryTypes.MuscleGroup => "raumenų grupė",
        CategoryTypes.Equipment => "įranga",
        CategoryTypes.Goal => "tikslas",
        _ => type
    };

    /// <summary>"Krūtinė " → "krutine": case-, accent- and space-insensitive comparison key.</summary>
    private static string Fold(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var c in s.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static Category MapDocumentToCategory(DocumentSnapshot doc)
    {
        var dict = doc.ToDictionary();
        string Str(string key) => dict.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";
        return new Category
        {
            Id = doc.Id,
            Type = Str("type"),
            Name = Str("name"),
            Description = Str("description"),
            CreatedAt = dict.TryGetValue("createdAt", out var ca) && ca is Timestamp ts ? ts.ToDateTime() : DateTime.UtcNow
        };
    }
}
