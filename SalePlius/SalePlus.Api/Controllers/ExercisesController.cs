using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalePlus.Api.Services;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/xml")]
public class ExercisesController : ControllerBase
{
    private readonly IFirestoreService _service;
    private readonly ILogger<ExercisesController> _logger;

    public ExercisesController(IFirestoreService service, ILogger<ExercisesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 1. Gauti visus pratimus su filtravimo galimybe pagal raumenų grupes (?muscleGroup=Krūtinė,Tricepsas –
    /// pagrindinė arba antrinė, turi apimti visas), sunkumą arba paieškos žodį.
    /// </summary>
    /// Puslapiai: ?page=1&amp;pageSize=10, rikiavimas: ?sort=newest|rating|name, autorius: ?authorId=.
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<Exercise>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<Exercise>>> GetAll(
        [FromQuery] string[]? muscleGroup,
        [FromQuery] string? difficulty,
        [FromQuery] string? search,
        [FromQuery] string? authorId,
        [FromQuery] SortedPageQuery paging)
    {
        var exercises = (await _service.GetExercisesAsync(MuscleFilter.Parse(muscleGroup), difficulty, search))
            .Where(e => string.IsNullOrEmpty(authorId) || e.CreatedByUserId == authorId)
            .SortBy(paging.Sort, e => e.CreatedAt, e => e.Rating, e => e.Name);

        return Ok(exercises.ToPage(paging, Request, User.Identity?.IsAuthenticated == true ? "/api/exercises" : null));
    }

    /// <summary>
    /// 2. Gauti konkretų pratimą pagal unikalų identifikatorių.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Exercise), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Exercise>> GetById(string id)
    {
        var exercise = await _service.GetExerciseByIdAsync(id);
        if (exercise == null)
        {
            return NotFound(new { message = $"Pratimas su ID '{id}' nerastas." });
        }

        return Ok(exercise);
    }

    /// <summary>
    /// 3. Sukurti naują pratimą pratimų bibliotekoje.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Exercise), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Exercise>> Create([FromBody] CreateExerciseDto dto)
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

        var created = await _service.CreateExerciseAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// 4. Atnaujinti esamo pratimo informaciją (tik autorius arba administratorius).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Exercise), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Exercise>> Update(string id, [FromBody] UpdateExerciseDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (await _service.CheckCategoriesAsync(dto) is { } problem)
        {
            return BadRequest(new { message = problem });
        }

        var existing = await _service.GetExerciseByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Pratimas su ID '{id}' nerastas atnaujinimui." });
        }

        if (!User.CanModify(existing.CreatedByUserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Galite redaguoti tik savo pratimus." });
        }

        var updated = await _service.UpdateExerciseAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>
    /// 5. Ištrinti pratimą iš bibliotekos (autorius arba administratorius).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _service.GetExerciseByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Pratimas su ID '{id}' nerastas ištrynimui." });
        }

        if (!User.CanModify(existing.CreatedByUserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Galite ištrinti tik savo pratimus." });
        }

        await _service.DeleteExerciseAsync(id);
        return NoContent();
    }

    /// <summary>
    /// 6. Gauti visus konkretaus pratimo komentarus.
    /// </summary>
    [HttpGet("{exerciseId}/comments")]
    [ProducesResponseType(typeof(List<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<Comment>>> GetComments(string exerciseId)
    {
        var exercise = await _service.GetExerciseByIdAsync(exerciseId);
        if (exercise == null)
        {
            return NotFound(new { message = $"Pratimas su ID '{exerciseId}' nerastas." });
        }

        var comments = await _service.GetCommentsForExerciseAsync(exerciseId);
        return Ok(comments);
    }

    /// <summary>
    /// 7. Pridėti naują komentarą ar patarimą po pratimu.
    /// </summary>
    [HttpPost("{exerciseId}/comments")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Comment), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Comment>> AddComment(string exerciseId, [FromBody] CreateCommentDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Owner always comes from the token, whatever the body says.
        dto.UserId = User.GetUserId();
        dto.UserName = User.GetUserName();

        var comment = await _service.AddCommentToExerciseAsync(exerciseId, dto);
        if (comment == null)
        {
            return NotFound(new { message = $"Pratimas su ID '{exerciseId}' nerastas, komentaro pridėti negalima." });
        }

        return CreatedAtAction(nameof(GetComments), new { exerciseId }, comment);
    }

    /// <summary>
    /// 8. Ištrinti komentarą po pratimu (komentaro autorius arba administratorius).
    /// </summary>
    [HttpDelete("{exerciseId}/comments/{commentId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(string exerciseId, string commentId)
    {
        var comment = await _service.GetExerciseCommentAsync(exerciseId, commentId);
        if (comment == null)
        {
            return NotFound(new { message = $"Komentaras su ID '{commentId}' arba pratimas '{exerciseId}' nerastas." });
        }

        if (!User.CanModify(comment.UserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Galite ištrinti tik savo komentarus." });
        }

        await _service.DeleteCommentAsync(exerciseId, commentId);
        return NoContent();
    }
}
