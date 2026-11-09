using Google.Cloud.Firestore;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

public partial class FirestoreService : IFirestoreService
{
    private readonly FirestoreDb _db;
    private readonly ILogger<FirestoreService> _logger;
    private readonly IConfiguration _config;

    public FirestoreService(FirestoreDb db, ILogger<FirestoreService> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _config = config;
    }

    #region Exercise Methods

    public async Task<List<Exercise>> GetExercisesAsync(IReadOnlyCollection<string> muscleGroups, string? difficulty, string? search)
    {
        var collection = _db.Collection("exercises");
        var snapshot = await collection.GetSnapshotAsync();

        var exercises = snapshot.Documents
            .Select(MapDocumentToExercise)
            .ToList();

        // Main or secondary muscle group both count.
        exercises = exercises
            .Where(e => e.SecondaryMuscleGroups.Append(e.MuscleGroup).TrainsAll(muscleGroups))
            .ToList();

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            exercises = exercises.Where(e =>
                string.Equals(e.Difficulty, difficulty, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            exercises = exercises.Where(e =>
                e.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains(s, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        return exercises;
    }

    public async Task<Exercise?> GetExerciseByIdAsync(string id)
    {
        var doc = await _db.Collection("exercises").Document(id).GetSnapshotAsync();
        if (!doc.Exists) return null;
        return MapDocumentToExercise(doc);
    }

    public async Task<Exercise> CreateExerciseAsync(CreateExerciseDto dto)
    {
        var docRef = _db.Collection("exercises").Document();
        var now = DateTime.UtcNow;

        var data = new Dictionary<string, object>
        {
            { "name", dto.Name.Trim() },
            { "description", dto.Description.Trim() },
            { "muscleGroup", dto.MuscleGroup.Trim() },
            { "secondaryMuscleGroups", dto.SecondaryMuscleGroups ?? new List<string>() },
            { "difficulty", dto.Difficulty },
            { "instructions", dto.Instructions ?? string.Empty },
            { "equipment", dto.Equipment ?? "Bodyweight" },
            { "createdByUserId", dto.CreatedByUserId ?? "anonymous" },
            { "createdByUserName", dto.CreatedByUserName ?? "anonymous" },
            { "createdAt", Timestamp.FromDateTime(now) },
            { "rating", 0.0 },
            { "ratingCount", 0 }
        };

        await docRef.SetAsync(data);

        return new Exercise
        {
            Id = docRef.Id,
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            MuscleGroup = dto.MuscleGroup.Trim(),
            SecondaryMuscleGroups = dto.SecondaryMuscleGroups ?? new List<string>(),
            Difficulty = dto.Difficulty,
            Instructions = dto.Instructions ?? string.Empty,
            Equipment = dto.Equipment ?? "Bodyweight",
            CreatedByUserId = dto.CreatedByUserId ?? "anonymous",
            CreatedByUserName = dto.CreatedByUserName ?? "anonymous",
            CreatedAt = now,
            Rating = 0.0,
            RatingCount = 0
        };
    }

    public async Task<Exercise?> UpdateExerciseAsync(string id, UpdateExerciseDto dto)
    {
        var docRef = _db.Collection("exercises").Document(id);
        var snapshot = await docRef.GetSnapshotAsync();
        if (!snapshot.Exists) return null;

        var updates = new Dictionary<string, object>
        {
            { "name", dto.Name.Trim() },
            { "description", dto.Description.Trim() },
            { "muscleGroup", dto.MuscleGroup.Trim() },
            { "secondaryMuscleGroups", dto.SecondaryMuscleGroups ?? new List<string>() },
            { "difficulty", dto.Difficulty },
            { "instructions", dto.Instructions ?? string.Empty },
            { "equipment", dto.Equipment ?? "Bodyweight" }
        };

        await docRef.UpdateAsync(updates);

        var updatedSnap = await docRef.GetSnapshotAsync();
        return MapDocumentToExercise(updatedSnap);
    }

    public async Task<bool> DeleteExerciseAsync(string id)
    {
        var docRef = _db.Collection("exercises").Document(id);
        var snapshot = await docRef.GetSnapshotAsync();
        if (!snapshot.Exists) return false;

        // Delete subcollection comments if any
        var commentsSnap = await docRef.Collection("comments").GetSnapshotAsync();
        foreach (var c in commentsSnap.Documents)
        {
            await c.Reference.DeleteAsync();
        }

        await docRef.DeleteAsync();
        return true;
    }

    #endregion

    #region Comments (exercises and workouts)

    public Task<List<Comment>> GetCommentsForExerciseAsync(string exerciseId) => GetCommentsAsync("exercises", exerciseId);
    public Task<Comment?> AddCommentToExerciseAsync(string exerciseId, CreateCommentDto dto) => AddCommentAsync("exercises", exerciseId, dto);
    public Task<Comment?> GetExerciseCommentAsync(string exerciseId, string commentId) => GetCommentAsync("exercises", exerciseId, commentId);
    public Task<bool> DeleteCommentAsync(string exerciseId, string commentId) => DeleteCommentAsync("exercises", exerciseId, commentId);

    public Task<List<Comment>> GetCommentsForWorkoutAsync(string workoutId) => GetCommentsAsync("workouts", workoutId);
    public Task<Comment?> AddCommentToWorkoutAsync(string workoutId, CreateCommentDto dto) => AddCommentAsync("workouts", workoutId, dto);
    public Task<Comment?> GetWorkoutCommentAsync(string workoutId, string commentId) => GetCommentAsync("workouts", workoutId, commentId);
    public Task<bool> DeleteWorkoutCommentAsync(string workoutId, string commentId) => DeleteCommentAsync("workouts", workoutId, commentId);

    private async Task<List<Comment>> GetCommentsAsync(string parentCollection, string parentId)
    {
        var parentDoc = await _db.Collection(parentCollection).Document(parentId).GetSnapshotAsync();
        if (!parentDoc.Exists) return new List<Comment>();

        var commentsSnap = await _db.Collection(parentCollection)
            .Document(parentId)
            .Collection("comments")
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        return commentsSnap.Documents.Select(d => MapDocumentToComment(d, parentCollection, parentId)).ToList();
    }

    private async Task<Comment?> GetCommentAsync(string parentCollection, string parentId, string commentId)
    {
        var snapshot = await _db.Collection(parentCollection).Document(parentId)
            .Collection("comments").Document(commentId).GetSnapshotAsync();
        return snapshot.Exists ? MapDocumentToComment(snapshot, parentCollection, parentId) : null;
    }

    private async Task<Comment?> AddCommentAsync(string parentCollection, string parentId, CreateCommentDto dto)
    {
        var parentRef = _db.Collection(parentCollection).Document(parentId);
        var parentDoc = await parentRef.GetSnapshotAsync();
        if (!parentDoc.Exists) return null;

        var commentRef = parentRef.Collection("comments").Document();
        var now = DateTime.UtcNow;
        var parentKey = parentCollection == "workouts" ? "workoutId" : "exerciseId";

        var data = new Dictionary<string, object>
        {
            { parentKey, parentId },
            { "userId", dto.UserId },
            { "userName", dto.UserName },
            { "content", dto.Content.Trim() },
            { "createdAt", Timestamp.FromDateTime(now) }
        };

        await commentRef.SetAsync(data);

        return new Comment
        {
            Id = commentRef.Id,
            ExerciseId = parentCollection == "exercises" ? parentId : string.Empty,
            WorkoutId = parentCollection == "workouts" ? parentId : string.Empty,
            UserId = dto.UserId,
            UserName = dto.UserName,
            Content = dto.Content.Trim(),
            CreatedAt = now
        };
    }

    private async Task<bool> DeleteCommentAsync(string parentCollection, string parentId, string commentId)
    {
        var commentRef = _db.Collection(parentCollection)
            .Document(parentId)
            .Collection("comments")
            .Document(commentId);

        var snapshot = await commentRef.GetSnapshotAsync();
        if (!snapshot.Exists) return false;

        await commentRef.DeleteAsync();
        return true;
    }

    #endregion

    #region Workout Methods

    public async Task<List<Workout>> GetWorkoutsAsync(IReadOnlyCollection<string> muscleGroups, string? author, string? search)
    {
        var collection = _db.Collection("workouts");
        var snapshot = await collection.GetSnapshotAsync();

        var workouts = snapshot.Documents
            .Select(MapDocumentToWorkout)
            .ToList();

        workouts = workouts.Where(w => w.TargetMuscleGroups.TrainsAll(muscleGroups)).ToList();

        if (!string.IsNullOrWhiteSpace(author))
        {
            workouts = workouts.Where(w =>
                string.Equals(w.CreatedByUserName, author, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(w.CreatedByUserId, author, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            workouts = workouts.Where(w =>
                w.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                w.Description.Contains(s, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        return workouts;
    }

    public async Task<Workout?> GetWorkoutByIdAsync(string id)
    {
        var doc = await _db.Collection("workouts").Document(id).GetSnapshotAsync();
        if (!doc.Exists) return null;
        return MapDocumentToWorkout(doc);
    }

    public async Task<Workout> CreateWorkoutAsync(CreateWorkoutDto dto)
    {
        var docRef = _db.Collection("workouts").Document();
        var now = DateTime.UtcNow;

        await FillExerciseNamesAsync(dto.Exercises);
        var exercisesList = ExercisesToFirestore(dto.Exercises);

        var data = new Dictionary<string, object>
        {
            { "programId", dto.ProgramId ?? string.Empty },
            { "days", NormalizeDays(dto.Days) },
            { "title", dto.Title.Trim() },
            { "description", dto.Description.Trim() },
            { "difficulty", dto.Difficulty },
            { "targetMuscleGroups", dto.TargetMuscleGroups ?? new List<string>() },
            { "createdByUserId", dto.CreatedByUserId ?? "anonymous" },
            { "createdByUserName", dto.CreatedByUserName ?? "anonymous" },
            { "createdAt", Timestamp.FromDateTime(now) },
            { "exercises", exercisesList },
            { "averageRating", 0.0 },
            { "ratingsCount", 0 }
        };

        await docRef.SetAsync(data);
        return MapDocumentToWorkout(await docRef.GetSnapshotAsync());
    }

    public async Task<Workout?> UpdateWorkoutAsync(string id, UpdateWorkoutDto dto)
    {
        var docRef = _db.Collection("workouts").Document(id);
        var snapshot = await docRef.GetSnapshotAsync();
        if (!snapshot.Exists) return null;

        var updates = new Dictionary<string, object>
        {
            { "title", dto.Title.Trim() },
            { "description", dto.Description.Trim() },
            { "difficulty", dto.Difficulty },
            { "targetMuscleGroups", dto.TargetMuscleGroups ?? new List<string>() }
        };

        // Optional parts: only replaced when the request includes them.
        if (dto.Exercises != null)
        {
            await FillExerciseNamesAsync(dto.Exercises);
            updates["exercises"] = ExercisesToFirestore(dto.Exercises);
        }
        if (dto.Days != null) updates["days"] = NormalizeDays(dto.Days);

        await docRef.UpdateAsync(updates);

        var updatedSnap = await docRef.GetSnapshotAsync();
        return MapDocumentToWorkout(updatedSnap);
    }

    public async Task<bool> DeleteWorkoutAsync(string id)
    {
        var docRef = _db.Collection("workouts").Document(id);
        var snapshot = await docRef.GetSnapshotAsync();
        if (!snapshot.Exists) return false;

        var ratingsSnap = await docRef.Collection("ratings").GetSnapshotAsync();
        foreach (var r in ratingsSnap.Documents)
        {
            await r.Reference.DeleteAsync();
        }

        var commentsSnap = await docRef.Collection("comments").GetSnapshotAsync();
        foreach (var c in commentsSnap.Documents)
        {
            await c.Reference.DeleteAsync();
        }

        await docRef.DeleteAsync();
        return true;
    }

    public async Task<Workout?> AddExerciseToWorkoutAsync(string workoutId, AddWorkoutExerciseDto dto)
    {
        var docRef = _db.Collection("workouts").Document(workoutId);
        var snapshot = await docRef.GetSnapshotAsync();
        if (!snapshot.Exists) return null;

        var workout = MapDocumentToWorkout(snapshot);

        // Auto-fill exercise name if empty
        var exerciseName = dto.ExerciseName;
        if (string.IsNullOrWhiteSpace(exerciseName) && !string.IsNullOrWhiteSpace(dto.ExerciseId))
        {
            var ex = await GetExerciseByIdAsync(dto.ExerciseId);
            if (ex != null) exerciseName = ex.Name;
        }

        var newItem = new WorkoutExerciseItem
        {
            Id = NewEntryId(),
            ExerciseId = dto.ExerciseId,
            ExerciseName = exerciseName ?? "Custom Exercise",
            Sets = dto.Sets,
            Reps = dto.Reps,
            Notes = dto.Notes ?? string.Empty
        };

        workout.Exercises.Add(newItem);
        await docRef.UpdateAsync("exercises", ExercisesToFirestore(workout.Exercises));
        return workout;
    }

    public async Task<bool> RemoveExerciseFromWorkoutAsync(string workoutId, string exerciseId)
    {
        var docRef = _db.Collection("workouts").Document(workoutId);
        var snapshot = await docRef.GetSnapshotAsync();
        if (!snapshot.Exists) return false;

        var workout = MapDocumentToWorkout(snapshot);
        var initialCount = workout.Exercises.Count;
        workout.Exercises.RemoveAll(e => e.ExerciseId.Equals(exerciseId, StringComparison.OrdinalIgnoreCase));

        if (workout.Exercises.Count == initialCount)
        {
            return false;
        }

        await docRef.UpdateAsync("exercises", ExercisesToFirestore(workout.Exercises));
        return true;
    }

    #endregion

    #region Workout Ratings

    public async Task<List<WorkoutRating>> GetRatingsForWorkoutAsync(string workoutId)
    {
        var workoutDoc = await _db.Collection("workouts").Document(workoutId).GetSnapshotAsync();
        if (!workoutDoc.Exists) return new List<WorkoutRating>();

        var ratingsSnap = await _db.Collection("workouts")
            .Document(workoutId)
            .Collection("ratings")
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        return ratingsSnap.Documents.Select(d => MapDocumentToRating(d, workoutId)).ToList();
    }

    public async Task<(WorkoutRating? rating, string? error)> AddRatingToWorkoutAsync(string workoutId, CreateRatingDto dto)
    {
        var workoutRef = _db.Collection("workouts").Document(workoutId);
        var workoutDoc = await workoutRef.GetSnapshotAsync();
        if (!workoutDoc.Exists) return (null, "Workout not found");

        var existingUserRating = await workoutRef.Collection("ratings")
            .WhereEqualTo("userId", dto.UserId)
            .GetSnapshotAsync();

        if (existingUserRating.Count > 0)
        {
            return (null, "User has already rated this workout");
        }

        var ratingRef = workoutRef.Collection("ratings").Document();
        var now = DateTime.UtcNow;

        var data = new Dictionary<string, object>
        {
            { "workoutId", workoutId },
            { "userId", dto.UserId },
            { "userName", dto.UserName },
            { "score", dto.Score },
            { "review", dto.Review ?? string.Empty },
            { "createdAt", Timestamp.FromDateTime(now) }
        };

        await ratingRef.SetAsync(data);

        // Recalculate average rating
        var allRatingsSnap = await workoutRef.Collection("ratings").GetSnapshotAsync();
        var allRatings = allRatingsSnap.Documents.Select(d => MapDocumentToRating(d, workoutId)).ToList();
        var avg = allRatings.Any() ? allRatings.Average(r => r.Score) : 0.0;

        await workoutRef.UpdateAsync(new Dictionary<string, object>
        {
            { "averageRating", Math.Round(avg, 1) },
            { "ratingsCount", allRatings.Count }
        });

        var createdRating = new WorkoutRating
        {
            Id = ratingRef.Id,
            WorkoutId = workoutId,
            UserId = dto.UserId,
            UserName = dto.UserName,
            Score = dto.Score,
            Review = dto.Review ?? string.Empty,
            CreatedAt = now
        };

        return (createdRating, null);
    }

    #endregion

    #region Saved Workouts (Bookmarks)

    public async Task<List<SavedWorkout>> GetSavedWorkoutsForUserAsync(string userId)
    {
        var snap = await _db.Collection("users")
            .Document(userId)
            .Collection("savedWorkouts")
            .OrderByDescending("savedAt")
            .GetSnapshotAsync();

        return snap.Documents.Select(d =>
        {
            var dict = d.ToDictionary();
            return new SavedWorkout
            {
                Id = d.Id,
                UserId = userId,
                WorkoutId = dict.TryGetValue("workoutId", out var wid) ? wid?.ToString() ?? "" : "",
                WorkoutTitle = dict.TryGetValue("workoutTitle", out var wt) ? wt?.ToString() ?? "" : "",
                SavedAt = dict.TryGetValue("savedAt", out var sa) && sa is Timestamp ts ? ts.ToDateTime() : DateTime.UtcNow
            };
        }).ToList();
    }

    public async Task<(SavedWorkout? saved, string? error)> SaveWorkoutForUserAsync(string userId, string workoutId)
    {
        var workoutDoc = await _db.Collection("workouts").Document(workoutId).GetSnapshotAsync();
        if (!workoutDoc.Exists)
        {
            return (null, "Workout not found");
        }

        var savedDocRef = _db.Collection("users").Document(userId).Collection("savedWorkouts").Document(workoutId);
        var existing = await savedDocRef.GetSnapshotAsync();
        if (existing.Exists)
        {
            return (null, "Workout is already saved");
        }

        var workoutDict = workoutDoc.ToDictionary();
        var workoutTitle = workoutDict.TryGetValue("title", out var t) ? t?.ToString() ?? "Workout" : "Workout";
        var now = DateTime.UtcNow;

        var data = new Dictionary<string, object>
        {
            { "workoutId", workoutId },
            { "workoutTitle", workoutTitle },
            { "savedAt", Timestamp.FromDateTime(now) }
        };

        await savedDocRef.SetAsync(data);

        return (new SavedWorkout
        {
            Id = workoutId,
            UserId = userId,
            WorkoutId = workoutId,
            WorkoutTitle = workoutTitle,
            SavedAt = now
        }, null);
    }

    public async Task<bool> UnsaveWorkoutForUserAsync(string userId, string workoutId)
    {
        var savedDocRef = _db.Collection("users").Document(userId).Collection("savedWorkouts").Document(workoutId);
        var existing = await savedDocRef.GetSnapshotAsync();
        if (!existing.Exists)
        {
            return false;
        }

        await savedDocRef.DeleteAsync();
        return true;
    }

    #endregion

    #region Reports

    public async Task<List<ViolationReport>> GetReportsAsync(string? status)
    {
        var query = _db.Collection("reports").OrderByDescending("createdAt");
        var snap = await query.GetSnapshotAsync();

        var reports = snap.Documents.Select(MapDocumentToReport).ToList();

        if (!string.IsNullOrWhiteSpace(status))
        {
            reports = reports.Where(r => string.Equals(r.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return reports;
    }

    public async Task<ViolationReport> CreateReportAsync(CreateReportDto dto)
    {
        var docRef = _db.Collection("reports").Document();
        var now = DateTime.UtcNow;

        var data = new Dictionary<string, object>
        {
            { "targetType", dto.TargetType },
            { "targetId", dto.TargetId },
            { "reportedByUserId", dto.ReportedByUserId },
            { "reason", dto.Reason.Trim() },
            { "status", "Pending" },
            { "createdAt", Timestamp.FromDateTime(now) }
        };

        await docRef.SetAsync(data);

        return new ViolationReport
        {
            Id = docRef.Id,
            TargetType = dto.TargetType,
            TargetId = dto.TargetId,
            ReportedByUserId = dto.ReportedByUserId,
            Reason = dto.Reason.Trim(),
            Status = "Pending",
            CreatedAt = now
        };
    }

    public async Task<ViolationReport?> UpdateReportStatusAsync(string id, string status)
    {
        var docRef = _db.Collection("reports").Document(id);
        var snap = await docRef.GetSnapshotAsync();
        if (!snap.Exists) return null;

        var updates = new Dictionary<string, object>
        {
            { "status", status }
        };

        if (status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Dismissed", StringComparison.OrdinalIgnoreCase))
        {
            updates["resolvedAt"] = Timestamp.FromDateTime(DateTime.UtcNow);
        }

        await docRef.UpdateAsync(updates);
        var updated = await docRef.GetSnapshotAsync();
        return MapDocumentToReport(updated);
    }

    #endregion

    #region Users

    public async Task<UserProfile?> GetUserByIdAsync(string id)
    {
        var doc = await _db.Collection("users").Document(id).GetSnapshotAsync();
        if (!doc.Exists) return null;
        return MapDocumentToUserProfile(doc);
    }

    public async Task<UserProfile?> GetUserByUsernameOrEmailAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        var clean = identifier.Trim();
        var byUsername = await _db.Collection("users")
            .WhereEqualTo("username", clean)
            .Limit(1)
            .GetSnapshotAsync();

        if (byUsername.Count > 0)
        {
            return MapDocumentToUserProfile(byUsername.Documents[0]);
        }

        var byEmail = await _db.Collection("users")
            .WhereEqualTo("email", clean.ToLowerInvariant())
            .Limit(1)
            .GetSnapshotAsync();

        if (byEmail.Count > 0)
        {
            return MapDocumentToUserProfile(byEmail.Documents[0]);
        }

        return null;
    }

    public async Task<(UserProfile? user, string? error)> AuthenticateUserAsync(string usernameOrEmail, string password)
    {
        // One message for every failure, so the answer never reveals which usernames or emails exist.
        const string failed = "Neteisingas prisijungimo vardas arba slaptažodis.";

        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrEmpty(password))
        {
            return (null, failed);
        }

        var clean = usernameOrEmail.Trim();
        var snap = await _db.Collection("users")
            .WhereEqualTo("username", clean)
            .Limit(1)
            .GetSnapshotAsync();

        if (snap.Count == 0)
        {
            snap = await _db.Collection("users")
                .WhereEqualTo("email", clean.ToLowerInvariant())
                .Limit(1)
                .GetSnapshotAsync();
        }

        if (snap.Count == 0)
        {
            return (null, failed);
        }

        var doc = snap.Documents[0];

        // An account without a password (e.g. created before passwords were required) cannot be logged into.
        var hash = doc.ToDictionary().TryGetValue("passwordHash", out var stored) ? stored?.ToString() : null;
        if (string.IsNullOrWhiteSpace(hash) || !PasswordHasher.VerifyPassword(password, hash))
        {
            return (null, failed);
        }

        return (MapDocumentToUserProfile(doc), null);
    }

    public async Task<(UserProfile? user, string? error)> RegisterUserAsync(RegisterDto dto)
    {
        var existingUsername = await _db.Collection("users")
            .WhereEqualTo("username", dto.Username.Trim())
            .GetSnapshotAsync();

        if (existingUsername.Count > 0)
        {
            return (null, "Vartotojas su tokiu vartotojo vardu jau egzistuoja.");
        }

        var existingEmail = await _db.Collection("users")
            .WhereEqualTo("email", dto.Email.Trim().ToLowerInvariant())
            .GetSnapshotAsync();

        if (existingEmail.Count > 0)
        {
            return (null, "Vartotojas su tokiu el. pašto adresu jau egzistuoja.");
        }

        var docRef = _db.Collection("users").Document();
        var now = DateTime.UtcNow;
        var hash = PasswordHasher.HashPassword(dto.Password);

        var data = new Dictionary<string, object>
        {
            { "username", dto.Username.Trim() },
            { "email", dto.Email.Trim().ToLowerInvariant() },
            { "role", AppRoles.User },
            { "bio", dto.Bio?.Trim() ?? string.Empty },
            { "passwordHash", hash },
            { "createdAt", Timestamp.FromDateTime(now) }
        };

        await docRef.SetAsync(data);

        return (new UserProfile
        {
            Id = docRef.Id,
            Username = dto.Username.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            Role = AppRoles.User,
            Bio = dto.Bio?.Trim() ?? string.Empty,
            CreatedAt = now
        }, null);
    }

    public async Task<List<UserProfile>> GetAllUsersAsync()
    {
        var snap = await _db.Collection("users").GetSnapshotAsync();
        return snap.Documents.Select(MapDocumentToUserProfile).OrderBy(u => u.Username).ToList();
    }

    public async Task<(UserProfile? user, string? error)> CreateUserAsync(CreateUserDto dto)
    {
        var existingUsername = await _db.Collection("users")
            .WhereEqualTo("username", dto.Username.Trim())
            .GetSnapshotAsync();

        if (existingUsername.Count > 0)
        {
            return (null, "User with this username already exists");
        }

        var existingEmail = await _db.Collection("users")
            .WhereEqualTo("email", dto.Email.Trim().ToLowerInvariant())
            .GetSnapshotAsync();

        if (existingEmail.Count > 0)
        {
            return (null, "User with this email already exists");
        }

        var docRef = _db.Collection("users").Document();
        var now = DateTime.UtcNow;

        var data = new Dictionary<string, object>
        {
            { "username", dto.Username.Trim() },
            { "email", dto.Email.Trim().ToLowerInvariant() },
            { "role", dto.Role ?? "User" },
            { "bio", dto.Bio ?? string.Empty },
            { "createdAt", Timestamp.FromDateTime(now) }
        };

        if (!string.IsNullOrEmpty(dto.Password))
        {
            data["passwordHash"] = PasswordHasher.HashPassword(dto.Password);
        }

        await docRef.SetAsync(data);

        return (new UserProfile
        {
            Id = docRef.Id,
            Username = dto.Username.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            Role = dto.Role ?? "User",
            Bio = dto.Bio ?? string.Empty,
            CreatedAt = now
        }, null);
    }

    public async Task<UserProfile?> UpdateUserRoleAsync(string id, string role)
    {
        var docRef = _db.Collection("users").Document(id);
        var snap = await docRef.GetSnapshotAsync();
        if (!snap.Exists) return null;

        await docRef.UpdateAsync("role", role);
        var updated = await docRef.GetSnapshotAsync();
        return MapDocumentToUserProfile(updated);
    }

    public async Task<bool> DeleteUserAsync(string id, bool deleteContent = false)
    {
        var docRef = _db.Collection("users").Document(id);
        var snap = await docRef.GetSnapshotAsync();
        if (!snap.Exists) return false;

        if (deleteContent)
        {
            // Remove everything the user authored (workouts incl. their ratings/comments, and exercises).
            await DeleteProgramsByUserAsync(id);

            var workouts = await _db.Collection("workouts").WhereEqualTo("createdByUserId", id).GetSnapshotAsync();
            foreach (var w in workouts.Documents)
            {
                await DeleteWorkoutAsync(w.Id);
            }

            var exercises = await _db.Collection("exercises").WhereEqualTo("createdByUserId", id).GetSnapshotAsync();
            foreach (var e in exercises.Documents)
            {
                await DeleteExerciseAsync(e.Id);
            }
        }

        // Delete subcollections
        await DeleteSavedItemsAsync(docRef);
        await RevokeUserRefreshTokensAsync(id);
        var saved = await docRef.Collection("savedWorkouts").GetSnapshotAsync();
        foreach (var s in saved.Documents)
        {
            await s.Reference.DeleteAsync();
        }

        await docRef.DeleteAsync();
        return true;
    }

    #endregion

    #region Data Seeder

    public async Task SeedInitialDataAsync()
    {
        try
        {
            var snap = await _db.Collection("exercises").Limit(1).GetSnapshotAsync();
            if (snap.Count > 0)
            {
                return; // Already seeded
            }

            _logger.LogInformation("Seeding initial fitness data into Firestore...");

            // 1. Seed Users
            var adminUser = await CreateUserAsync(new CreateUserDto
            {
                Username = "admin",
                Email = "admin@saleplius.lt",
                Role = "Admin",
                Bio = "Vyriausiasis Salė+ administratorius",
                // First admin of an empty database: password from configuration (Seed__AdminPassword), never from code.
                // Without it the account exists but nobody can log into it.
                Password = _config["Seed:AdminPassword"] ?? string.Empty
            });

            var coachUser = await CreateUserAsync(new CreateUserDto
            {
                Username = "treneris_jonas",
                Email = "jonas@saleplius.lt",
                Role = "User",
                Bio = "Asmeninis fitneso treneris"
            });

            var adminId = adminUser.user?.Id ?? "admin-1";
            var coachId = coachUser.user?.Id ?? "coach-1";

            // 2. Seed Exercises
            var ex1 = await CreateExerciseAsync(new CreateExerciseDto
            {
                Name = "Štangos spaudimas gulint (Bench Press)",
                Description = "Klasikinis pratimas viršutinei kūno daliai, ypač krūtinės raumenims.",
                MuscleGroup = "Krūtinė",
                SecondaryMuscleGroups = new List<string> { "Tricepsas", "Priekiniai deltiniai" },
                Difficulty = "Intermediate",
                Equipment = "Barbell",
                Instructions = "Atsigulkite ant suolo, paimkite štangą pečių plotyje, leiskite kontroliuojamai iki krūtinės ir spauskite aukštyn.",
                CreatedByUserId = coachId,
                CreatedByUserName = "treneris_jonas"
            });

            var ex2 = await CreateExerciseAsync(new CreateExerciseDto
            {
                Name = "Pritūpimai su štanga (Barbell Squat)",
                Description = "Pagrindinis pratimas kojų ir sėdmenų stiprinimui bei bendrai jėgai.",
                MuscleGroup = "Keturgalviai",
                SecondaryMuscleGroups = new List<string> { "Sėdmenys", "Pritraukiamieji", "Apatinė nugara" },
                Difficulty = "Advanced",
                Equipment = "Barbell",
                Instructions = "Padėkite štangą ant viršutinės nugaros dalies, leiskitės žemyn laikydami nugarą tiesią iki lygiagretės.",
                CreatedByUserId = coachId,
                CreatedByUserName = "treneris_jonas"
            });

            var ex3 = await CreateExerciseAsync(new CreateExerciseDto
            {
                Name = "Prisitraukimai prie skersinio (Pull-ups)",
                Description = "Puikus pratimas nugaros pločio ir bicepso jėgos ugdymui.",
                MuscleGroup = "Platieji nugaros",
                SecondaryMuscleGroups = new List<string> { "Bicepsas", "Užpakaliniai deltiniai", "Dilbiai" },
                Difficulty = "Intermediate",
                Equipment = "Bodyweight",
                Instructions = "Suimkite skersinį plačiau nei pečių plotis, kelkite kūną į viršų kol smakras pasieks skersinį.",
                CreatedByUserId = coachId,
                CreatedByUserName = "treneris_jonas"
            });

            var ex4 = await CreateExerciseAsync(new CreateExerciseDto
            {
                Name = "Kariškas spaudimas stovint (Overhead Press)",
                Description = "Efektyviausias pratimas pečių juostos jėgai ir masei auginti.",
                MuscleGroup = "Priekiniai deltiniai",
                SecondaryMuscleGroups = new List<string> { "Tricepsas", "Pilvo presas" },
                Difficulty = "Intermediate",
                Equipment = "Barbell",
                Instructions = "Stovint tvirtai, stumkite štangą nuo viršutinės krūtinės tiesiai virš galvos pilnai ištiesdami rankas.",
                CreatedByUserId = coachId,
                CreatedByUserName = "treneris_jonas"
            });

            var ex5 = await CreateExerciseAsync(new CreateExerciseDto
            {
                Name = "Lenta (Plank)",
                Description = "Statinis pratimas liemens stabilizavimui ir giliųjų pilvo raumenų stiprinimui.",
                MuscleGroup = "Pilvo presas",
                SecondaryMuscleGroups = new List<string> { "Įstrižiniai", "Apatinė nugara", "Sėdmenys" },
                Difficulty = "Beginner",
                Equipment = "Bodyweight",
                Instructions = "Atsiremkite dilbiais ir kojų pirštais į grindis, išlaikykite kūną tiesioje linijoje 30-60 sekundžių.",
                CreatedByUserId = coachId,
                CreatedByUserName = "treneris_jonas"
            });

            // 3. Add sample comment to Exercise 1
            await AddCommentToExerciseAsync(ex1.Id, new CreateCommentDto
            {
                UserId = coachId,
                UserName = "treneris_jonas",
                Content = "Pradedantiesiems rekomenduoju pradėti nuo lengvo svorio ir susitelkti į taisyklingą mentčių suvedimą!"
            });

            // 4. Seed Workouts
            var w1 = await CreateWorkoutAsync(new CreateWorkoutDto
            {
                Title = "Viso kūno treniruotė pradedantiesiems (Full Body)",
                Description = "Bazinė viso kūno treniruotė, apimanti pagrindinius stambiuosius raumenis.",
                Difficulty = "Beginner",
                TargetMuscleGroups = new List<string> { "Keturgalviai", "Krūtinė", "Platieji nugaros", "Pilvo presas" },
                CreatedByUserId = coachId,
                CreatedByUserName = "treneris_jonas",
                Exercises = new List<WorkoutExerciseItem>
                {
                    new() { ExerciseId = ex2.Id, ExerciseName = ex2.Name, Sets = 3, Reps = 10, Notes = "Poilsis 90s tarp serijų" },
                    new() { ExerciseId = ex1.Id, ExerciseName = ex1.Name, Sets = 3, Reps = 8, Notes = "Poilsis 60-90s" },
                    new() { ExerciseId = ex3.Id, ExerciseName = ex3.Name, Sets = 3, Reps = 6, Notes = "Galima naudoti pasipriešinimo gumą" },
                    new() { ExerciseId = ex5.Id, ExerciseName = ex5.Name, Sets = 3, Reps = 45, Notes = "45 sekundžių trukmė" }
                }
            });

            var w2 = await CreateWorkoutAsync(new CreateWorkoutDto
            {
                Title = "Viršutinės kūno dalies sprogstamoji jėga",
                Description = "Intensyvi viršutinės kūno dalies treniruotė krūtinei, nugarai ir pečiams.",
                Difficulty = "Intermediate",
                TargetMuscleGroups = new List<string> { "Krūtinė", "Priekiniai deltiniai", "Platieji nugaros" },
                CreatedByUserId = coachId,
                CreatedByUserName = "treneris_jonas",
                Exercises = new List<WorkoutExerciseItem>
                {
                    new() { ExerciseId = ex1.Id, ExerciseName = ex1.Name, Sets = 4, Reps = 8, Notes = "Sunki serija" },
                    new() { ExerciseId = ex4.Id, ExerciseName = ex4.Name, Sets = 3, Reps = 10, Notes = "Kariškas spaudimas" },
                    new() { ExerciseId = ex3.Id, ExerciseName = ex3.Name, Sets = 4, Reps = 8, Notes = "Platus suėmimas" }
                }
            });

            // 5. Add rating to workout
            await AddRatingToWorkoutAsync(w1.Id, new CreateRatingDto
            {
                UserId = coachId,
                UserName = "treneris_jonas",
                Score = 5,
                Review = "Labai gera subalansuota programa naujokams."
            });

            // 6. Seed a sample report
            await CreateReportAsync(new CreateReportDto
            {
                TargetType = "Comment",
                TargetId = "sample-comment-id",
                ReportedByUserId = coachId,
                Reason = "Netinkama kalba komentare."
            });

            _logger.LogInformation("Database seeded successfully with initial exercises and workouts!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while seeding Firestore initial data");
        }
    }

    #endregion

    #region Helper Mappers

    private static Exercise MapDocumentToExercise(DocumentSnapshot doc)
    {
        var dict = doc.ToDictionary();
        var ex = new Exercise
        {
            Id = doc.Id,
            Name = dict.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "",
            Description = dict.TryGetValue("description", out var d) ? d?.ToString() ?? "" : "",
            MuscleGroup = dict.TryGetValue("muscleGroup", out var mg) ? mg?.ToString() ?? "" : "",
            Difficulty = dict.TryGetValue("difficulty", out var diff) ? diff?.ToString() ?? "Intermediate" : "Intermediate",
            Instructions = dict.TryGetValue("instructions", out var inst) ? inst?.ToString() ?? "" : "",
            Equipment = dict.TryGetValue("equipment", out var eq) ? eq?.ToString() ?? "Bodyweight" : "Bodyweight",
            CreatedByUserId = dict.TryGetValue("createdByUserId", out var cb) ? cb?.ToString() ?? "" : "",
            CreatedByUserName = dict.TryGetValue("createdByUserName", out var cbn) ? cbn?.ToString() ?? "" : "",
            Rating = dict.TryGetValue("rating", out var r) ? Convert.ToDouble(r) : 0.0,
            RatingCount = dict.TryGetValue("ratingCount", out var rc) ? Convert.ToInt32(rc) : 0,
            CreatedAt = dict.TryGetValue("createdAt", out var ca) && ca is Timestamp ts ? ts.ToDateTime() : (doc.CreateTime?.ToDateTime() ?? DateTime.UtcNow)
        };

        if (dict.TryGetValue("secondaryMuscleGroups", out var smg) && smg is IEnumerable<object> list)
        {
            ex.SecondaryMuscleGroups = list.Select(x => x?.ToString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        return ex;
    }

    private static Comment MapDocumentToComment(DocumentSnapshot doc, string parentCollection, string parentId)
    {
        var dict = doc.ToDictionary();
        return new Comment
        {
            Id = doc.Id,
            ExerciseId = parentCollection == "exercises" ? parentId : string.Empty,
            WorkoutId = parentCollection == "workouts" ? parentId : string.Empty,
            UserId = dict.TryGetValue("userId", out var u) ? u?.ToString() ?? "" : "",
            UserName = dict.TryGetValue("userName", out var un) ? un?.ToString() ?? "" : "",
            Content = dict.TryGetValue("content", out var c) ? c?.ToString() ?? "" : "",
            CreatedAt = dict.TryGetValue("createdAt", out var ca) && ca is Timestamp ts ? ts.ToDateTime() : (doc.CreateTime?.ToDateTime() ?? DateTime.UtcNow)
        };
    }

    private static Workout MapDocumentToWorkout(DocumentSnapshot doc)
    {
        var dict = doc.ToDictionary();
        var w = new Workout
        {
            Id = doc.Id,
            Title = dict.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "",
            Description = dict.TryGetValue("description", out var d) ? d?.ToString() ?? "" : "",
            Difficulty = dict.TryGetValue("difficulty", out var diff) ? diff?.ToString() ?? "Intermediate" : "Intermediate",
            CreatedByUserId = dict.TryGetValue("createdByUserId", out var cb) ? cb?.ToString() ?? "" : "",
            CreatedByUserName = dict.TryGetValue("createdByUserName", out var cbn) ? cbn?.ToString() ?? "" : "",
            AverageRating = dict.TryGetValue("averageRating", out var ar) ? Convert.ToDouble(ar) : 0.0,
            RatingsCount = dict.TryGetValue("ratingsCount", out var rc) ? Convert.ToInt32(rc) : 0,
            CreatedAt = dict.TryGetValue("createdAt", out var ca) && ca is Timestamp ts ? ts.ToDateTime() : (doc.CreateTime?.ToDateTime() ?? DateTime.UtcNow),
            ProgramId = dict.TryGetValue("programId", out var pid) ? pid?.ToString() ?? "" : ""
        };

        if (dict.TryGetValue("days", out var rawDays) && rawDays is IEnumerable<object> dayList)
        {
            w.Days = dayList.Select(Convert.ToInt32).OrderBy(d => d).ToList();
        }

        if (dict.TryGetValue("targetMuscleGroups", out var tmg) && tmg is IEnumerable<object> list)
        {
            w.TargetMuscleGroups = list.Select(x => x?.ToString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        if (dict.TryGetValue("exercises", out var rawExercises) && rawExercises is IEnumerable<object> exerciseList)
        {
            var index = 0;
            foreach (var item in exerciseList)
            {
                if (item is Dictionary<string, object> exDict)
                {
                    index++;
                    w.Exercises.Add(new WorkoutExerciseItem
                    {
                        // Older entries were saved without an ID; give them a stable one until the next save stores it.
                        Id = exDict.TryGetValue("id", out var entryId) && !string.IsNullOrEmpty(entryId?.ToString()) ? entryId.ToString()! : $"e{index}",
                        ExerciseId = exDict.TryGetValue("exerciseId", out var eid) ? eid?.ToString() ?? "" : "",
                        ExerciseName = exDict.TryGetValue("exerciseName", out var ename) ? ename?.ToString() ?? "" : "",
                        Sets = exDict.TryGetValue("sets", out var s) ? Convert.ToInt32(s) : 3,
                        Reps = exDict.TryGetValue("reps", out var r) ? Convert.ToInt32(r) : 10,
                        Notes = exDict.TryGetValue("notes", out var not) ? not?.ToString() ?? "" : ""
                    });
                }
            }
        }

        return w;
    }

    private static WorkoutRating MapDocumentToRating(DocumentSnapshot doc, string workoutId)
    {
        var dict = doc.ToDictionary();
        return new WorkoutRating
        {
            Id = doc.Id,
            WorkoutId = workoutId,
            UserId = dict.TryGetValue("userId", out var u) ? u?.ToString() ?? "" : "",
            UserName = dict.TryGetValue("userName", out var un) ? un?.ToString() ?? "" : "",
            Score = dict.TryGetValue("score", out var s) ? Convert.ToInt32(s) : 5,
            Review = dict.TryGetValue("review", out var rev) ? rev?.ToString() ?? "" : "",
            CreatedAt = dict.TryGetValue("createdAt", out var ca) && ca is Timestamp ts ? ts.ToDateTime() : (doc.CreateTime?.ToDateTime() ?? DateTime.UtcNow)
        };
    }

    private static ViolationReport MapDocumentToReport(DocumentSnapshot doc)
    {
        var dict = doc.ToDictionary();
        DateTime? resDate = null;
        if (dict.TryGetValue("resolvedAt", out var ra) && ra is Timestamp tsRes)
        {
            resDate = tsRes.ToDateTime();
        }

        return new ViolationReport
        {
            Id = doc.Id,
            TargetType = dict.TryGetValue("targetType", out var tt) ? tt?.ToString() ?? "" : "",
            TargetId = dict.TryGetValue("targetId", out var tid) ? tid?.ToString() ?? "" : "",
            ReportedByUserId = dict.TryGetValue("reportedByUserId", out var rb) ? rb?.ToString() ?? "" : "",
            Reason = dict.TryGetValue("reason", out var r) ? r?.ToString() ?? "" : "",
            Status = dict.TryGetValue("status", out var s) ? s?.ToString() ?? "Pending" : "Pending",
            CreatedAt = dict.TryGetValue("createdAt", out var ca) && ca is Timestamp ts ? ts.ToDateTime() : (doc.CreateTime?.ToDateTime() ?? DateTime.UtcNow),
            ResolvedAt = resDate
        };
    }

    private static UserProfile MapDocumentToUserProfile(DocumentSnapshot doc)
    {
        var dict = doc.ToDictionary();
        return new UserProfile
        {
            Id = doc.Id,
            Username = dict.TryGetValue("username", out var u) ? u?.ToString() ?? "" : "",
            Email = dict.TryGetValue("email", out var e) ? e?.ToString() ?? "" : "",
            Role = dict.TryGetValue("role", out var r) ? r?.ToString() ?? "User" : "User",
            Bio = dict.TryGetValue("bio", out var b) ? b?.ToString() ?? "" : "",
            CreatedAt = dict.TryGetValue("createdAt", out var ca) && ca is Timestamp ts ? ts.ToDateTime() : (doc.CreateTime?.ToDateTime() ?? DateTime.UtcNow)
        };
    }

    #endregion
}
