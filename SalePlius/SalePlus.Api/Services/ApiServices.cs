using System.Net.Http.Json;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlius.Client.Services;

public class ApiService(HttpClient http)
{
    public async Task<List<Exercise>> GetExercisesAsync(string? search = null, string? muscleGroup = null, string? difficulty = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(muscleGroup)) q.Add($"muscleGroup={Uri.EscapeDataString(muscleGroup)}");
        if (!string.IsNullOrWhiteSpace(difficulty)) q.Add($"difficulty={Uri.EscapeDataString(difficulty)}");

        var url = "api/exercises" + (q.Count > 0 ? "?" + string.Join("&", q) : "");
        return await http.GetFromJsonAsync<List<Exercise>>(url) ?? [];
    }

    public async Task<bool> CreateExerciseAsync(CreateExerciseDto dto)
        => (await http.PostAsJsonAsync("api/exercises", dto)).IsSuccessStatusCode;

    public async Task<bool> DeleteExerciseAsync(string id)
        => (await http.DeleteAsync($"api/exercises/{id}")).IsSuccessStatusCode;

    public async Task<List<Workout>> GetWorkoutsAsync(string? search = null)
    {
        var url = "api/workouts";
        if (!string.IsNullOrWhiteSpace(search)) url += $"?search={Uri.EscapeDataString(search)}";
        return await http.GetFromJsonAsync<List<Workout>>(url) ?? [];
    }
}