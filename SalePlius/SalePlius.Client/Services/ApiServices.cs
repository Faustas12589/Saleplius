using System.Net.Http.Json;
using System.Text.Json;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlius.Client.Services;

public class ApiService(HttpClient http, CustomAuthStateProvider auth)
{
    // ==================== AUTH ====================
    /// <summary>
    /// Logs out on the server (the access token and this session's refresh token stop working at once),
    /// then forgets the session in the browser. The local part happens even if the server is unreachable.
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            if (auth.IsAuthenticated)
            {
                await http.PostAsJsonAsync("api/auth/logout", new LogoutDto { RefreshToken = auth.RefreshToken });
            }
        }
        catch
        {
        }
        finally
        {
            await auth.LogoutAsync();
        }
    }

    public async Task<(bool Success, string? Error, AuthResponseDto? Data)> LoginAsync(LoginDto dto)
    {
        try
        {
            var res = await http.PostAsJsonAsync("api/auth/login", dto);
            if (res.IsSuccessStatusCode)
            {
                var data = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
                return (true, null, data);
            }

            var errBody = await res.Content.ReadAsStringAsync();
            return (false, ParseErrorMessage(errBody, "Prisijungti nepavyko. Patikrinkite duomenis."), null);
        }
        catch (Exception ex)
        {
            return (false, $"Ryšio klaida: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string? Error, AuthResponseDto? Data)> RegisterAsync(RegisterDto dto)
    {
        try
        {
            var res = await http.PostAsJsonAsync("api/auth/register", dto);
            if (res.IsSuccessStatusCode)
            {
                var data = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
                return (true, null, data);
            }

            var errBody = await res.Content.ReadAsStringAsync();
            return (false, ParseErrorMessage(errBody, "Registracija nepavyko."), null);
        }
        catch (Exception ex)
        {
            return (false, $"Ryšio klaida: {ex.Message}", null);
        }
    }

    // ==================== EXERCISES ====================
    /// <summary>Every exercise matching the filters (walks through all pages).</summary>
    public Task<List<Exercise>> GetExercisesAsync(string? search = null, string? muscleGroup = null, string? difficulty = null)
        => GetAllPagesAsync<Exercise>(Url("api/exercises", new() { ["search"] = search, ["muscleGroup"] = muscleGroup, ["difficulty"] = difficulty }));

    public async Task<bool> CreateExerciseAsync(CreateExerciseDto dto)
        => (await CreateExerciseAndReturnAsync(dto)).Exercise != null;

    /// <summary>Creates an exercise and returns it (or the API's error message).</summary>
    public async Task<(Exercise? Exercise, string? Error)> CreateExerciseAndReturnAsync(CreateExerciseDto dto)
    {
        try
        {
            var res = await http.PostAsJsonAsync("api/exercises", dto);
            if (res.IsSuccessStatusCode)
            {
                return (await res.Content.ReadFromJsonAsync<Exercise>(), null);
            }

            return (null, ParseErrorMessage(await res.Content.ReadAsStringAsync(), "Nepavyko sukurti pratimo."));
        }
        catch (Exception ex)
        {
            return (null, $"Ryšio klaida: {ex.Message}");
        }
    }

    /// <summary>Changes title, description, difficulty and target muscles. Exercises and days stay as they are (left null).</summary>
    public async Task<(bool Success, string? Error)> UpdateWorkoutAsync(string id, UpdateWorkoutDto dto)
        => await Outcome(await http.PutAsJsonAsync($"api/workouts/{id}", dto), "Nepavyko atnaujinti treniruotės.");

    public async Task<(bool Success, string? Error)> UpdateExerciseAsync(string id, UpdateExerciseDto dto)
        => await Outcome(await http.PutAsJsonAsync($"api/exercises/{id}", dto), "Nepavyko atnaujinti pratimo.");

    public async Task<bool> DeleteExerciseAsync(string id)
        => (await http.DeleteAsync($"api/exercises/{id}")).IsSuccessStatusCode;

    // ==================== PROGRAMS (Program → Workouts → Exercises) ====================
    /// <summary>Every program matching the filters (walks through all pages).</summary>
    public Task<List<TrainingProgram>> GetProgramsAsync(string? search = null, string? workoutId = null)
        => GetAllPagesAsync<TrainingProgram>(Url("api/programs", new() { ["search"] = search, ["workoutId"] = workoutId }));

    public async Task<TrainingProgram?> GetProgramAsync(string id)
    {
        try
        {
            return await http.GetFromJsonAsync<TrainingProgram>($"api/programs/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<(TrainingProgram? Program, string? Error)> CreateProgramAsync(CreateProgramDto dto)
        => await ReadProgram(await http.PostAsJsonAsync("api/programs", dto), "Nepavyko sukurti programos.");

    public async Task<(TrainingProgram? Program, string? Error)> UpdateProgramAsync(string id, UpdateProgramDto dto)
        => await ReadProgram(await http.PutAsJsonAsync($"api/programs/{id}", dto), "Nepavyko išsaugoti programos.");

    public async Task<bool> DeleteProgramAsync(string id)
        => (await http.DeleteAsync($"api/programs/{id}")).IsSuccessStatusCode;

    private static async Task<(TrainingProgram?, string?)> ReadProgram(HttpResponseMessage res, string fallback)
    {
        if (res.IsSuccessStatusCode)
        {
            return (await res.Content.ReadFromJsonAsync<TrainingProgram>(), null);
        }

        return (null, ParseErrorMessage(await res.Content.ReadAsStringAsync(), fallback));
    }

    // ==================== CATEGORIES (admin-managed allowed values) ====================
    public Task<List<Category>> GetCategoriesAsync(string? type = null)
        => GetAllPagesAsync<Category>(Url("api/categories", new() { ["type"] = type }));

    public async Task<(bool Success, string? Error)> CreateCategoryAsync(CreateCategoryDto dto)
        => await Outcome(await http.PostAsJsonAsync("api/categories", dto), "Nepavyko sukurti kategorijos.");

    public async Task<(bool Success, string? Error)> UpdateCategoryAsync(string id, UpdateCategoryDto dto)
        => await Outcome(await http.PutAsJsonAsync($"api/categories/{id}", dto), "Nepavyko pervadinti kategorijos.");

    public async Task<(bool Success, string? Error)> DeleteCategoryAsync(string id)
        => await Outcome(await http.DeleteAsync($"api/categories/{id}"), "Nepavyko ištrinti kategorijos.");

    // ==================== HIERARCHY: Program → Workout → Exercise entry ====================
    /// <summary>A workout read through its program; null if it doesn't belong to that program.</summary>
    public async Task<Workout?> GetProgramWorkoutAsync(string programId, string workoutId)
    {
        var res = await http.GetAsync($"api/programs/{programId}/workouts/{workoutId}");
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<Workout>() : null;
    }

    public async Task<WorkoutExerciseItem?> GetWorkoutEntryAsync(string programId, string workoutId, string entryId)
    {
        var res = await http.GetAsync($"api/programs/{programId}/workouts/{workoutId}/exercises/{entryId}");
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<WorkoutExerciseItem>() : null;
    }

    public async Task<(bool Success, string? Error)> AddWorkoutEntryAsync(string programId, string workoutId, AddWorkoutExerciseDto dto)
        => await Outcome(await http.PostAsJsonAsync($"api/programs/{programId}/workouts/{workoutId}/exercises", dto), "Nepavyko pridėti pratimo.");

    public async Task<(bool Success, string? Error)> UpdateWorkoutEntryAsync(string programId, string workoutId, string entryId, AddWorkoutExerciseDto dto)
        => await Outcome(await http.PutAsJsonAsync($"api/programs/{programId}/workouts/{workoutId}/exercises/{entryId}", dto), "Nepavyko išsaugoti pratimo.");

    public async Task<(bool Success, string? Error)> DeleteWorkoutEntryAsync(string programId, string workoutId, string entryId)
        => await Outcome(await http.DeleteAsync($"api/programs/{programId}/workouts/{workoutId}/exercises/{entryId}"), "Nepavyko pašalinti pratimo.");

    public async Task<Exercise?> GetExerciseAsync(string id)
    {
        var res = await http.GetAsync($"api/exercises/{id}");
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<Exercise>() : null;
    }

    private static async Task<(bool, string?)> Outcome(HttpResponseMessage res, string fallback) =>
        res.IsSuccessStatusCode ? (true, null) : (false, ParseErrorMessage(await res.Content.ReadAsStringAsync(), fallback));

    // ==================== WORKOUTS ====================
    /// <summary>Every workout matching the filter (walks through all pages).</summary>
    public Task<List<Workout>> GetWorkoutsAsync(string? search = null)
        => GetAllPagesAsync<Workout>(Url("api/workouts", new() { ["search"] = search }));

    public async Task<Workout?> GetWorkoutByIdAsync(string id)
    {
        try
        {
            return await http.GetFromJsonAsync<Workout>($"api/workouts/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CreateWorkoutAsync(CreateWorkoutDto dto)
        => (await http.PostAsJsonAsync("api/workouts", dto)).IsSuccessStatusCode;

    public async Task<bool> DeleteWorkoutAsync(string id)
        => (await http.DeleteAsync($"api/workouts/{id}")).IsSuccessStatusCode;

    public async Task<bool> AddExerciseToWorkoutAsync(string workoutId, AddWorkoutExerciseDto dto)
        => (await http.PostAsJsonAsync($"api/workouts/{workoutId}/exercises", dto)).IsSuccessStatusCode;

    public async Task<bool> RemoveExerciseFromWorkoutAsync(string workoutId, string exerciseId)
        => (await http.DeleteAsync($"api/workouts/{workoutId}/exercises/{exerciseId}")).IsSuccessStatusCode;

    public async Task<string> GetWorkoutSummaryTextAsync(string workoutId)
    {
        try
        {
            return await http.GetStringAsync($"api/workouts/{workoutId}/summary");
        }
        catch (Exception ex)
        {
            return $"Nepavyko gauti suvestinės: {ex.Message}";
        }
    }

    public async Task<bool> AddRatingToWorkoutAsync(string workoutId, CreateRatingDto dto)
        => (await http.PostAsJsonAsync($"api/workouts/{workoutId}/ratings", dto)).IsSuccessStatusCode;

    public async Task<List<WorkoutRating>> GetWorkoutRatingsAsync(string workoutId)
    {
        try
        {
            return await http.GetFromJsonAsync<List<WorkoutRating>>($"api/workouts/{workoutId}/ratings") ?? [];
        }
        catch
        {
            return [];
        }
    }

    // ==================== WORKOUT COMMENTS ====================
    public async Task<List<Comment>> GetWorkoutCommentsAsync(string workoutId)
    {
        try
        {
            return await http.GetFromJsonAsync<List<Comment>>($"api/workouts/{workoutId}/comments") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> AddWorkoutCommentAsync(string workoutId, CreateCommentDto dto)
        => (await http.PostAsJsonAsync($"api/workouts/{workoutId}/comments", dto)).IsSuccessStatusCode;

    public async Task<bool> DeleteWorkoutCommentAsync(string workoutId, string commentId)
        => (await http.DeleteAsync($"api/workouts/{workoutId}/comments/{commentId}")).IsSuccessStatusCode;

    // ==================== SAVED WORKOUTS ====================
    public async Task<List<SavedWorkout>> GetSavedWorkoutsAsync(string userId)
    {
        try
        {
            return await http.GetFromJsonAsync<List<SavedWorkout>>($"api/users/{userId}/saved-workouts") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> SaveWorkoutAsync(string userId, string workoutId)
        => (await http.PostAsync($"api/users/{userId}/saved-workouts/{workoutId}", null)).IsSuccessStatusCode;

    public async Task<bool> UnsaveWorkoutAsync(string userId, string workoutId)
        => (await http.DeleteAsync($"api/users/{userId}/saved-workouts/{workoutId}")).IsSuccessStatusCode;

    // ==================== SAVED ITEMS (programs, workouts, exercises) ====================
    /// <summary>IDs of everything of this kind the user saved. <paramref name="kind"/> is a <see cref="SavedKind"/> value.</summary>
    public async Task<HashSet<string>> GetSavedIdsAsync(string userId, string kind)
    {
        var res = await http.GetAsync($"api/users/{userId}/saved-{kind}");
        if (!res.IsSuccessStatusCode) return [];

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        // Saved workouts expose "workoutId", saved programs/exercises expose "itemId".
        return doc.RootElement.EnumerateArray()
            .Select(e => e.TryGetProperty("workoutId", out var w) ? w.GetString() : e.TryGetProperty("itemId", out var i) ? i.GetString() : null)
            .OfType<string>()
            .ToHashSet();
    }

    public async Task<bool> SaveItemAsync(string userId, string kind, string itemId)
        => (await http.PostAsync($"api/users/{userId}/saved-{kind}/{itemId}", null)).IsSuccessStatusCode;

    public async Task<bool> UnsaveItemAsync(string userId, string kind, string itemId)
        => (await http.DeleteAsync($"api/users/{userId}/saved-{kind}/{itemId}")).IsSuccessStatusCode;

    // ==================== TICKETS / REPORTS (ADMIN) ====================
    /// <summary>Throws with a readable message when the request fails, so the page can show it.</summary>
    public Task<List<ViolationReport>> GetReportsAsync(string? status = null)
        => GetAllPagesAsync<ViolationReport>(Url("api/reports", new() { ["status"] = status }));

    // ==================== USERS (ADMIN) ====================
    public Task<List<UserProfile>> GetUsersAsync() => GetAllPagesAsync<UserProfile>("api/users");

    // ==================== PROFILE (one resource built from several entities) ====================
    public async Task<UserProfileDetails?> GetProfileAsync(string userId)
    {
        var res = await http.GetAsync($"api/users/{userId}/profile");
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<UserProfileDetails>() : null;
    }

    // ==================== PAGING + HYPERMEDIA ====================
    /// <summary>One page of a list endpoint. <paramref name="url"/> can be a "next"/"prev" link from a previous page.</summary>
    public async Task<PagedResult<T>> GetPageAsync<T>(string url)
    {
        var res = await http.GetAsync(url.TrimStart('/'));
        await EnsureOk(res);
        return await res.Content.ReadFromJsonAsync<PagedResult<T>>() ?? new PagedResult<T>();
    }

    /// <summary>Loads page 1, then keeps following the "next" link the API returns until there is none.</summary>
    public async Task<List<T>> GetAllPagesAsync<T>(string url)
    {
        var all = new List<T>();
        string? next = Url(url, new() { ["pageSize"] = "50" });
        while (next != null)
        {
            var page = await GetPageAsync<T>(next);
            all.AddRange(page.Items);
            next = page.Links.Href("next");
        }
        return all;
    }

    /// <summary>Adds non-empty query parameters to a path.</summary>
    public static string Url(string path, Dictionary<string, string?> query)
    {
        var parts = query.Where(q => !string.IsNullOrWhiteSpace(q.Value))
            .Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value!)}").ToList();
        if (parts.Count == 0) return path;
        return path + (path.Contains('?') ? "&" : "?") + string.Join("&", parts);
    }

    public async Task<UserProfile?> GetUserAsync(string id)
    {
        try
        {
            return await http.GetFromJsonAsync<UserProfile>($"api/users/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string? Error)> UpdateUserRoleAsync(string id, string role)
    {
        var res = await http.PatchAsJsonAsync($"api/users/{id}/role", new UpdateUserRoleDto { Role = role });
        return res.IsSuccessStatusCode ? (true, null) : (false, ParseErrorMessage(await res.Content.ReadAsStringAsync(), "Nepavyko pakeisti rolės."));
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(string id, bool deleteContent)
    {
        var res = await http.DeleteAsync($"api/users/{id}?deleteContent={(deleteContent ? "true" : "false")}");
        return res.IsSuccessStatusCode ? (true, null) : (false, ParseErrorMessage(await res.Content.ReadAsStringAsync(), "Nepavyko ištrinti vartotojo."));
    }

    private static async Task EnsureOk(HttpResponseMessage res)
    {
        if (res.IsSuccessStatusCode) return;
        var message = res.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Sesija baigėsi. Prisijunkite iš naujo.",
            System.Net.HttpStatusCode.Forbidden => "Neturite teisių šiam veiksmui.",
            _ => ParseErrorMessage(await res.Content.ReadAsStringAsync(), $"Serverio klaida ({(int)res.StatusCode}).")
        };
        throw new HttpRequestException(message, null, res.StatusCode);
    }

    public async Task<bool> UpdateReportStatusAsync(string id, string status)
    {
        var dto = new UpdateReportStatusDto { Status = status };
        var res = await http.PatchAsJsonAsync($"api/reports/{id}/status", dto);
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> CreateReportAsync(CreateReportDto dto)
        => (await http.PostAsJsonAsync("api/reports", dto)).IsSuccessStatusCode;

    // ==================== HELPERS ====================
    private static string ParseErrorMessage(string body, string defaultError)
    {
        if (string.IsNullOrWhiteSpace(body)) return defaultError;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m))
            {
                return m.GetString() ?? defaultError;
            }
        }
        catch
        {
        }
        return body.Length < 200 ? body : defaultError;
    }
}
