using Google.Cloud.Firestore;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

/// <summary>What can be bookmarked besides workouts.</summary>
public static class SavedItemType
{
    public const string Program = "Program";
    public const string Exercise = "Exercise";
}

public enum SaveItemError
{
    None,
    NotFound,
    AlreadySaved
}

/// <summary>
/// Saved programs and exercises. Stored like saved workouts: users/{userId}/savedPrograms|savedExercises/{itemId}.
/// </summary>
public partial class FirestoreService
{
    private static readonly Dictionary<string, (string Source, string SavedCollection, string TitleField)> SavedKinds = new()
    {
        [SavedItemType.Program] = ("programs", "savedPrograms", "title"),
        [SavedItemType.Exercise] = ("exercises", "savedExercises", "name"),
    };

    private CollectionReference SavedCollection(string userId, string itemType) =>
        _db.Collection("users").Document(userId).Collection(SavedKinds[itemType].SavedCollection);

    public async Task<List<SavedItem>> GetSavedItemsAsync(string userId, string itemType)
    {
        var kind = SavedKinds[itemType];
        var snap = await SavedCollection(userId, itemType).OrderByDescending("savedAt").GetSnapshotAsync();

        var items = new List<SavedItem>();
        foreach (var doc in snap.Documents)
        {
            // Drop bookmarks whose program/exercise has since been deleted.
            if (!(await _db.Collection(kind.Source).Document(doc.Id).GetSnapshotAsync()).Exists)
            {
                await doc.Reference.DeleteAsync();
                continue;
            }

            var dict = doc.ToDictionary();
            items.Add(new SavedItem
            {
                Id = doc.Id,
                UserId = userId,
                ItemType = itemType,
                ItemId = doc.Id,
                Title = dict.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "",
                SavedAt = dict.TryGetValue("savedAt", out var sa) && sa is Timestamp ts ? ts.ToDateTime() : DateTime.UtcNow
            });
        }

        return items;
    }

    public async Task<(SavedItem? Saved, SaveItemError Error)> SaveItemAsync(string userId, string itemType, string itemId)
    {
        var kind = SavedKinds[itemType];
        var source = await _db.Collection(kind.Source).Document(itemId).GetSnapshotAsync();
        if (!source.Exists) return (null, SaveItemError.NotFound);

        var docRef = SavedCollection(userId, itemType).Document(itemId);
        if ((await docRef.GetSnapshotAsync()).Exists) return (null, SaveItemError.AlreadySaved);

        var title = source.ToDictionary().TryGetValue(kind.TitleField, out var t) ? t?.ToString() ?? "" : "";
        var now = DateTime.UtcNow;
        await docRef.SetAsync(new Dictionary<string, object>
        {
            { "itemId", itemId },
            { "title", title },
            { "savedAt", Timestamp.FromDateTime(now) }
        });

        return (new SavedItem { Id = itemId, UserId = userId, ItemType = itemType, ItemId = itemId, Title = title, SavedAt = now }, SaveItemError.None);
    }

    public async Task<bool> UnsaveItemAsync(string userId, string itemType, string itemId)
    {
        var docRef = SavedCollection(userId, itemType).Document(itemId);
        if (!(await docRef.GetSnapshotAsync()).Exists) return false;

        await docRef.DeleteAsync();
        return true;
    }

    /// <summary>Used when a user account is deleted.</summary>
    private async Task DeleteSavedItemsAsync(DocumentReference userRef)
    {
        foreach (var kind in SavedKinds.Values)
        {
            var snap = await userRef.Collection(kind.SavedCollection).GetSnapshotAsync();
            foreach (var doc in snap.Documents)
            {
                await doc.Reference.DeleteAsync();
            }
        }
    }
}
