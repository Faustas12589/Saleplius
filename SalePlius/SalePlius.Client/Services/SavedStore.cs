namespace SalePlius.Client.Services;

/// <summary>URL segment for each kind of saved item: api/users/{id}/saved-{kind}.</summary>
public static class SavedKind
{
    public const string Programs = "programs";
    public const string Workouts = "workouts";
    public const string Exercises = "exercises";
}

/// <summary>
/// Remembers which programs/workouts/exercises the current user saved, so every
/// save button on a page doesn't fetch the list again. Resets when the user changes.
/// </summary>
public class SavedStore(ApiService api, CustomAuthStateProvider auth)
{
    private readonly Dictionary<string, HashSet<string>> _cache = new();
    private string? _cacheUserId;

    /// <summary>Raised after something is saved or unsaved, so other buttons/pages can refresh.</summary>
    public event Action? Changed;

    public async Task<HashSet<string>> GetAsync(string kind)
    {
        var userId = auth.CurrentUser?.Id;
        if (userId == null || !auth.IsAuthenticated) return [];

        if (_cacheUserId != userId)
        {
            _cache.Clear();
            _cacheUserId = userId;
        }

        if (!_cache.TryGetValue(kind, out var ids))
        {
            ids = await api.GetSavedIdsAsync(userId, kind);
            _cache[kind] = ids;
        }

        return ids;
    }

    public async Task<bool> IsSavedAsync(string kind, string itemId) => (await GetAsync(kind)).Contains(itemId);

    /// <summary>Saves or unsaves the item. Returns false if the request failed.</summary>
    public async Task<bool> ToggleAsync(string kind, string itemId)
    {
        var userId = auth.CurrentUser?.Id;
        if (userId == null) return false;

        var ids = await GetAsync(kind);
        var wasSaved = ids.Contains(itemId);
        var ok = wasSaved
            ? await api.UnsaveItemAsync(userId, kind, itemId)
            : await api.SaveItemAsync(userId, kind, itemId);

        if (ok)
        {
            if (wasSaved) ids.Remove(itemId); else ids.Add(itemId);
            Changed?.Invoke();
        }

        return ok;
    }
}
