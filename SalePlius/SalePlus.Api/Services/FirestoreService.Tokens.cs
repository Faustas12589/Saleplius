using Google.Cloud.Firestore;

namespace SalePlus.Api.Services;

/// <summary>A stored refresh token (the token itself is never stored, only its hash = document id).</summary>
public record RefreshTokenRecord(string Hash, string UserId, string FamilyId, DateTime ExpiresAt, DateTime? RevokedAt);

/// <summary>
/// Token storage:
///   refreshTokens/{sha256 of token}  – userId, familyId (one login session), expiresAt, revokedAt, replacedBy
///   revokedAccessTokens/{jti}        – access tokens ended by logout before their expiry
/// </summary>
public partial class FirestoreService
{
    private CollectionReference RefreshTokens => _db.Collection("refreshTokens");
    private CollectionReference RevokedAccessTokens => _db.Collection("revokedAccessTokens");

    public Task StoreRefreshTokenAsync(string hash, string userId, string familyId, DateTime expiresAt) =>
        RefreshTokens.Document(hash).SetAsync(new Dictionary<string, object>
        {
            { "userId", userId },
            { "familyId", familyId },
            { "createdAt", Timestamp.FromDateTime(DateTime.UtcNow) },
            { "expiresAt", Timestamp.FromDateTime(expiresAt) }
        });

    public async Task<RefreshTokenRecord?> GetRefreshTokenAsync(string hash)
    {
        var doc = await RefreshTokens.Document(hash).GetSnapshotAsync();
        if (!doc.Exists) return null;

        var d = doc.ToDictionary();
        return new RefreshTokenRecord(
            hash,
            d.TryGetValue("userId", out var u) ? u?.ToString() ?? "" : "",
            d.TryGetValue("familyId", out var f) ? f?.ToString() ?? "" : "",
            d.TryGetValue("expiresAt", out var e) && e is Timestamp te ? te.ToDateTime() : DateTime.MinValue,
            d.TryGetValue("revokedAt", out var r) && r is Timestamp tr ? tr.ToDateTime() : null);
    }

    public Task RevokeRefreshTokenAsync(string hash, string? replacedBy) =>
        RefreshTokens.Document(hash).UpdateAsync(new Dictionary<string, object>
        {
            { "revokedAt", Timestamp.FromDateTime(DateTime.UtcNow) },
            { "replacedBy", replacedBy ?? "" }
        });

    /// <summary>Ends one login session: every refresh token issued in it.</summary>
    public async Task RevokeRefreshFamilyAsync(string familyId)
    {
        var snap = await RefreshTokens.WhereEqualTo("familyId", familyId).GetSnapshotAsync();
        foreach (var doc in snap.Documents.Where(d => !d.ContainsField("revokedAt")))
        {
            await doc.Reference.UpdateAsync("revokedAt", Timestamp.FromDateTime(DateTime.UtcNow));
        }
    }

    /// <summary>Used when an account is deleted.</summary>
    public async Task RevokeUserRefreshTokensAsync(string userId)
    {
        var snap = await RefreshTokens.WhereEqualTo("userId", userId).GetSnapshotAsync();
        foreach (var doc in snap.Documents.Where(d => !d.ContainsField("revokedAt")))
        {
            await doc.Reference.UpdateAsync("revokedAt", Timestamp.FromDateTime(DateTime.UtcNow));
        }
    }

    public Task RevokeAccessTokenAsync(string jti, DateTime expiresAt) =>
        RevokedAccessTokens.Document(jti).SetAsync(new Dictionary<string, object>
        {
            { "revokedAt", Timestamp.FromDateTime(DateTime.UtcNow) },
            { "expiresAt", Timestamp.FromDateTime(expiresAt) }
        });

    public async Task<bool> IsAccessTokenRevokedAsync(string jti) =>
        (await RevokedAccessTokens.Document(jti).GetSnapshotAsync()).Exists;
}
