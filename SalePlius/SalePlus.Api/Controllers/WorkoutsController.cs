using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalePlus.Api.Services;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/xml")]
public class WorkoutsController : ControllerBase
{
    private readonly IFirestoreService _service;
    private readonly ILogger<WorkoutsController> _logger;

    public WorkoutsController(IFirestoreService service, ILogger<WorkoutsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 9. Gauti treniruotes puslapiais. Filtrai: raumenų grupės (?muscleGroup=Krūtinė,Tricepsas – treniruotė turi
    /// apimti visas), autorius (vardas arba ID), paieška.
    /// Rikiavimas: ?sort=newest|rating|name. Puslapiai: ?page=1&amp;pageSize=10 (daugiausiai 50).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<Workout>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<Workout>>> GetAll(
        [FromQuery] string[]? muscleGroup,
        [FromQuery] string? author,
        [FromQuery] string? authorId,
        [FromQuery] string? search,
        [FromQuery] SortedPageQuery paging)
    {
        var workouts = (await _service.GetWorkoutsAsync(MuscleFilter.Parse(muscleGroup), author, search))
            .Where(w => string.IsNullOrEmpty(authorId) || w.CreatedByUserId == authorId)
            .SortBy(paging.Sort, w => w.CreatedAt, w => w.AverageRating, w => w.Title);

        return Ok(workouts.ToPage(paging, Request, User.Identity?.IsAuthenticated == true ? "/api/workouts" : null));
    }

    /// <summary>
    /// 10. Gauti konkrečią treniruotę pagal ID su visa pratimų informacija.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Workout), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Workout>> GetById(string id)
    {
        var workout = await _service.GetWorkoutByIdAsync(id);
        if (workout == null)
        {
            return NotFound(new { message = $"Treniruotė su ID '{id}' nerasta." });
        }

        return Ok(workout);
    }

    /// <summary>
    /// 11. Sukurti naują treniruotę (sudaryti iš pratimų bibliotekos).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Workout), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Workout>> Create([FromBody] CreateWorkoutDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (await _service.CheckCategoriesAsync(dto) is { } problem)
        {
            return BadRequest(new { message = problem });
        }

        // Owner always comes from the token, whatever the body says.
        dto.CreatedByUserId = User.GetUserId();
        dto.CreatedByUserName = User.GetUserName();
        dto.ProgramId = string.Empty; // workouts are added to programs via /api/programs/{id}/workouts

        var workout = await _service.CreateWorkoutAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = workout.Id }, workout);
    }

    /// <summary>
    /// 12. Atnaujinti esamą treniruotę (tik autorius arba administratorius).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Workout), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Workout>> Update(string id, [FromBody] UpdateWorkoutDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (await _service.CheckCategoriesAsync(dto) is { } problem)
        {
            return BadRequest(new { message = problem });
        }

        var existing = await _service.GetWorkoutByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Treniruotė su ID '{id}' nerasta atnaujinimui." });
        }

        if (!User.CanModify(existing.CreatedByUserId))
        {
            return NotOwner();
        }

        var updated = await _service.UpdateWorkoutAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>
    /// 13. Ištrinti treniruotę (autorius arba administratorius).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _service.GetWorkoutByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Treniruotė su ID '{id}' nerasta ištrynimui." });
        }

        if (!User.CanModify(existing.CreatedByUserId))
        {
            return NotOwner();
        }

        await _service.DeleteWorkoutAsync(id);
        return NoContent();
    }

    /// <summary>
    /// 14. Pridėti pratimą į esamą treniruotę (tik autorius arba administratorius).
    /// </summary>
    [HttpPost("{workoutId}/exercises")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Workout), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Workout>> AddExerciseToWorkout(string workoutId, [FromBody] AddWorkoutExerciseDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existing = await _service.GetWorkoutByIdAsync(workoutId);
        if (existing == null)
        {
            return NotFound(new { message = $"Treniruotė su ID '{workoutId}' nerasta." });
        }

        if (!User.CanModify(existing.CreatedByUserId))
        {
            return NotOwner();
        }

        if (await _service.GetExerciseByIdAsync(dto.ExerciseId) == null)
        {
            return BadRequest(new { message = $"Pratimas su ID '{dto.ExerciseId}' bibliotekoje nerastas." });
        }

        var updatedWorkout = await _service.AddExerciseToWorkoutAsync(workoutId, dto);
        return CreatedAtAction(nameof(GetById), new { id = workoutId }, updatedWorkout);
    }

    /// <summary>
    /// 15. Pašalinti pratimą iš treniruotės (tik autorius arba administratorius).
    /// </summary>
    [HttpDelete("{workoutId}/exercises/{exerciseId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveExerciseFromWorkout(string workoutId, string exerciseId)
    {
        var existing = await _service.GetWorkoutByIdAsync(workoutId);
        if (existing != null && !User.CanModify(existing.CreatedByUserId))
        {
            return NotOwner();
        }

        var removed = existing != null && await _service.RemoveExerciseFromWorkoutAsync(workoutId, exerciseId);
        if (!removed)
        {
            return NotFound(new { message = $"Treniruotė '{workoutId}' arba pratimas '{exerciseId}' nerastas treniruotėje." });
        }

        return NoContent();
    }

    /// <summary>
    /// 16. Gauti visus treniruotės įvertinimus ir atsiliepimus.
    /// </summary>
    [HttpGet("{workoutId}/ratings")]
    [ProducesResponseType(typeof(List<WorkoutRating>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<WorkoutRating>>> GetRatings(string workoutId)
    {
        var workout = await _service.GetWorkoutByIdAsync(workoutId);
        if (workout == null)
        {
            return NotFound(new { message = $"Treniruotė su ID '{workoutId}' nerasta." });
        }

        var ratings = await _service.GetRatingsForWorkoutAsync(workoutId);
        return Ok(ratings);
    }

    /// <summary>
    /// 17. Įvertinti treniruotę (1-5 balais) ir palikti atsiliepimą.
    /// </summary>
    [HttpPost("{workoutId}/ratings")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(WorkoutRating), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkoutRating>> AddRating(string workoutId, [FromBody] CreateRatingDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Owner always comes from the token, whatever the body says.
        dto.UserId = User.GetUserId();
        dto.UserName = User.GetUserName();

        var (rating, error) = await _service.AddRatingToWorkoutAsync(workoutId, dto);
        if (error != null)
        {
            if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = error });
            }

            if (error.Contains("already rated", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = error });
            }

            return BadRequest(new { message = error });
        }

        return CreatedAtAction(nameof(GetRatings), new { workoutId }, rating);
    }

    /// <summary>
    /// Gauti visus treniruotės komentarus.
    /// </summary>
    [HttpGet("{workoutId}/comments")]
    [ProducesResponseType(typeof(List<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<Comment>>> GetComments(string workoutId)
    {
        var workout = await _service.GetWorkoutByIdAsync(workoutId);
        if (workout == null)
        {
            return NotFound(new { message = $"Treniruotė su ID '{workoutId}' nerasta." });
        }

        return Ok(await _service.GetCommentsForWorkoutAsync(workoutId));
    }

    /// <summary>
    /// Pridėti komentarą po bet kurio vartotojo treniruote.
    /// </summary>
    [HttpPost("{workoutId}/comments")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Comment), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Comment>> AddComment(string workoutId, [FromBody] CreateCommentDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Owner always comes from the token, whatever the body says.
        dto.UserId = User.GetUserId();
        dto.UserName = User.GetUserName();

        var comment = await _service.AddCommentToWorkoutAsync(workoutId, dto);
        if (comment == null)
        {
            return NotFound(new { message = $"Treniruotė su ID '{workoutId}' nerasta, komentaro pridėti negalima." });
        }

        return CreatedAtAction(nameof(GetComments), new { workoutId }, comment);
    }

    /// <summary>
    /// Ištrinti komentarą po treniruote (komentaro autorius arba administratorius).
    /// </summary>
    [HttpDelete("{workoutId}/comments/{commentId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(string workoutId, string commentId)
    {
        var comment = await _service.GetWorkoutCommentAsync(workoutId, commentId);
        if (comment == null)
        {
            return NotFound(new { message = $"Komentaras su ID '{commentId}' arba treniruotė '{workoutId}' nerasta." });
        }

        if (!User.CanModify(comment.UserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Galite ištrinti tik savo komentarus." });
        }

        await _service.DeleteWorkoutCommentAsync(workoutId, commentId);
        return NoContent();
    }

    private ObjectResult NotOwner() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "Galite keisti tik savo treniruotes." });

    /// <summary>
    /// 18. Gauti treniruotės tekstinę suvestinę (Formatas: text/plain).
    /// </summary>
    [HttpGet("{id}/summary")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkoutSummaryText(string id)
    {
        var workout = await _service.GetWorkoutByIdAsync(id);
        if (workout == null)
        {
            return NotFound($"Treniruotė su ID '{id}' nerasta.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"========================================");
        sb.AppendLine($"TRENIRUOTĖ: {workout.Title.ToUpperInvariant()}");
        sb.AppendLine($"========================================");
        sb.AppendLine($"Sunkumas: {workout.Difficulty}");
        sb.AppendLine($"Autorius: {workout.CreatedByUserName}");
        sb.AppendLine($"Tikslinės raumenų grupės: {string.Join(", ", workout.TargetMuscleGroups)}");
        sb.AppendLine($"Vidutinis įvertinimas: {workout.AverageRating:F1}/5.0 ({workout.RatingsCount} balsai)");
        sb.AppendLine($"Aprašymas: {workout.Description}");
        sb.AppendLine($"----------------------------------------");
        sb.AppendLine($"PRATIMAI ({workout.Exercises.Count}):");
        int index = 1;
        foreach (var ex in workout.Exercises)
        {
            sb.AppendLine($"  {index++}. {ex.ExerciseName} | {ex.Sets} serijos x {ex.Reps} pakartojimai" +
                          (!string.IsNullOrEmpty(ex.Notes) ? $" (Pastaba: {ex.Notes})" : ""));
        }
        sb.AppendLine($"========================================");

        return Content(sb.ToString(), "text/plain; charset=utf-8");
    }
}
