using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

public interface IFirestoreService
{
    // Exercises
    Task<List<Exercise>> GetExercisesAsync(IReadOnlyCollection<string> muscleGroups, string? difficulty, string? search);
    Task<Exercise?> GetExerciseByIdAsync(string id);
    Task<Exercise> CreateExerciseAsync(CreateExerciseDto dto);
    Task<Exercise?> UpdateExerciseAsync(string id, UpdateExerciseDto dto);
    Task<bool> DeleteExerciseAsync(string id);

    // Comments for Exercises
    Task<List<Comment>> GetCommentsForExerciseAsync(string exerciseId);
    Task<Comment?> AddCommentToExerciseAsync(string exerciseId, CreateCommentDto dto);
    Task<Comment?> GetExerciseCommentAsync(string exerciseId, string commentId);
    Task<bool> DeleteCommentAsync(string exerciseId, string commentId);

    // Comments for Workouts
    Task<List<Comment>> GetCommentsForWorkoutAsync(string workoutId);
    Task<Comment?> AddCommentToWorkoutAsync(string workoutId, CreateCommentDto dto);
    Task<Comment?> GetWorkoutCommentAsync(string workoutId, string commentId);
    Task<bool> DeleteWorkoutCommentAsync(string workoutId, string commentId);

    // Workouts
    Task<List<Workout>> GetWorkoutsAsync(IReadOnlyCollection<string> muscleGroups, string? author, string? search);
    Task<Workout?> GetWorkoutByIdAsync(string id);
    Task<Workout> CreateWorkoutAsync(CreateWorkoutDto dto);
    Task<Workout?> UpdateWorkoutAsync(string id, UpdateWorkoutDto dto);
    Task<bool> DeleteWorkoutAsync(string id);
    Task<Workout?> AddExerciseToWorkoutAsync(string workoutId, AddWorkoutExerciseDto dto);
    Task<bool> RemoveExerciseFromWorkoutAsync(string workoutId, string exerciseId);

    // Hierarchy: Program -> Workout -> Exercise entry (each level one-to-many)
    Task<List<TrainingProgram>> GetProgramsAsync(string? search, string? author, string? workoutId);
    Task<TrainingProgram?> GetProgramByIdAsync(string id);
    Task<ScheduleResult> CreateProgramAsync(CreateProgramDto dto, Func<Workout, bool> canUse);
    Task<ScheduleResult?> UpdateProgramAsync(string id, UpdateProgramDto dto, Func<Workout, bool> canUse);
    Task<bool> DeleteProgramAsync(string id);
    Task<List<Workout>?> GetProgramWorkoutsAsync(string programId);
    Task<Workout> CreateWorkoutInProgramAsync(string programId, CreateWorkoutDto dto);
    Task<WorkoutExerciseItem?> AddWorkoutEntryAsync(string workoutId, AddWorkoutExerciseDto dto);
    Task<WorkoutExerciseItem?> UpdateWorkoutEntryAsync(string workoutId, string entryId, AddWorkoutExerciseDto dto);
    Task<bool> DeleteWorkoutEntryAsync(string workoutId, string entryId);

    // Ratings for Workouts
    Task<List<WorkoutRating>> GetRatingsForWorkoutAsync(string workoutId);
    Task<(WorkoutRating? rating, string? error)> AddRatingToWorkoutAsync(string workoutId, CreateRatingDto dto);

    // Saved Workouts (Bookmarks)
    Task<List<SavedWorkout>> GetSavedWorkoutsForUserAsync(string userId);
    Task<(SavedWorkout? saved, string? error)> SaveWorkoutForUserAsync(string userId, string workoutId);
    Task<bool> UnsaveWorkoutForUserAsync(string userId, string workoutId);

    // Profile: one resource assembled from users, programs, workouts, ratings, exercises and saved items
    Task<UserProfileDetails?> GetUserProfileDetailsAsync(string userId, bool includePrivate);

    // Categories (admin-managed allowed values) and input checks
    Task<List<Category>> GetCategoriesAsync(string? type);
    Task<Category?> GetCategoryByIdAsync(string id);
    Task<CategoryResult> CreateCategoryAsync(CreateCategoryDto dto);
    Task<CategoryResult> UpdateCategoryAsync(string id, UpdateCategoryDto dto);
    Task<CategoryResult> DeleteCategoryAsync(string id);
    Task<(List<string> Names, string? Error)> MatchCategoriesAsync(string type, IEnumerable<string?>? names);
    Task<string?> CheckExerciseReferencesAsync(IEnumerable<WorkoutExerciseItem>? items);

    // Tokens: refresh tokens (stored as hashes) and access tokens revoked by logout
    Task StoreRefreshTokenAsync(string hash, string userId, string familyId, DateTime expiresAt);
    Task<RefreshTokenRecord?> GetRefreshTokenAsync(string hash);
    Task RevokeRefreshTokenAsync(string hash, string? replacedBy);
    Task RevokeRefreshFamilyAsync(string familyId);
    Task RevokeUserRefreshTokensAsync(string userId);
    Task RevokeAccessTokenAsync(string jti, DateTime expiresAt);
    Task<bool> IsAccessTokenRevokedAsync(string jti);

    // Saved programs & exercises
    Task<List<SavedItem>> GetSavedItemsAsync(string userId, string itemType);
    Task<(SavedItem? Saved, SaveItemError Error)> SaveItemAsync(string userId, string itemType, string itemId);
    Task<bool> UnsaveItemAsync(string userId, string itemType, string itemId);

    // Reports
    Task<List<ViolationReport>> GetReportsAsync(string? status);
    Task<ViolationReport> CreateReportAsync(CreateReportDto dto);
    Task<ViolationReport?> UpdateReportStatusAsync(string id, string status);

    // Users
    Task<UserProfile?> GetUserByIdAsync(string id);
    Task<UserProfile?> GetUserByUsernameOrEmailAsync(string identifier);
    Task<(UserProfile? user, string? error)> AuthenticateUserAsync(string usernameOrEmail, string password);
    Task<(UserProfile? user, string? error)> RegisterUserAsync(RegisterDto dto);
    Task<List<UserProfile>> GetAllUsersAsync();
    Task<(UserProfile? user, string? error)> CreateUserAsync(CreateUserDto dto);
    Task<UserProfile?> UpdateUserRoleAsync(string id, string role);
    Task<bool> DeleteUserAsync(string id, bool deleteContent = false);

    // Seeder
    Task SeedInitialDataAsync();
}
