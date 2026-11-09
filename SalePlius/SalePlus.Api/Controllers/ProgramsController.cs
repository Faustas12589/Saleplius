using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalePlus.Api.Services;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Controllers;

/// <summary>
/// Treniruočių programos – aukščiausias hierarchijos lygis: Programa → Treniruotė → Treniruotės pratimas.
/// Programai priklauso jos treniruotės (vienas-su-daug); savaitės tvarkaraštis sudaromas iš jų dienų.
/// Įdėtiniai maršrutai (/api/programs/{id}/workouts/...) yra ProgramWorkoutsController.
/// </summary>
[ApiController]
[Route("api/programs")]
[Produces("application/json", "application/xml")]
public class ProgramsController : ControllerBase
{
    private readonly IFirestoreService _service;

    public ProgramsController(IFirestoreService service)
    {
        _service = service;
    }

    /// <summary>
    /// Gauti programas puslapiais. Filtrai: paieška, tikslas (goal), autorius (vardas arba ID) arba
    /// programa, kuriai priklauso treniruotė (workoutId), raumenų grupės (?muscleGroup=Keturgalviai,Sėdmenys – programos
    /// treniruotės turi apimti visas). Rikiavimas: ?sort=newest|name.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TrainingProgram>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TrainingProgram>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? author,
        [FromQuery] string? authorId,
        [FromQuery] string? workoutId,
        [FromQuery] string? goal,
        [FromQuery] string[]? muscleGroup,
        [FromQuery] SortedPageQuery paging)
    {
        var muscleGroups = MuscleFilter.Parse(muscleGroup);

        // Programs have no rating of their own, so sort=rating falls back to newest first.
        var programs = (await _service.GetProgramsAsync(search, author, workoutId))
            .Where(p => string.IsNullOrEmpty(authorId) || p.CreatedByUserId == authorId)
            .Where(p => string.IsNullOrEmpty(goal) || string.Equals(p.Goal, goal, StringComparison.OrdinalIgnoreCase))
            .Where(p => p.MuscleGroups.TrainsAll(muscleGroups))
            .SortBy(paging.Sort, p => p.CreatedAt, _ => 0, p => p.Title);

        return Ok(programs.ToPage(paging, Request, User.Identity?.IsAuthenticated == true ? "/api/programs" : null));
    }

    /// <summary>Gauti programą pagal ID su savaitės tvarkaraščiu.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TrainingProgram), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingProgram>> GetById(string id)
    {
        var program = await _service.GetProgramByIdAsync(id);
        return program == null ? ProgramNotFound(id) : Ok(program);
    }

    /// <summary>
    /// Sukurti programą. Neprivalomas "workouts" sąrašas priskiria esamas (savo) treniruotes nurodytoms dienoms.
    /// 409 – treniruotė jau priklauso kitai programai, 403 – ne jūsų treniruotė.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(TrainingProgram), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TrainingProgram>> Create([FromBody] CreateProgramDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (await _service.CheckCategoriesAsync(dto) is { } problem)
        {
            return BadRequest(new { message = problem });
        }

        dto.CreatedByUserId = User.GetUserId();
        dto.CreatedByUserName = User.GetUserName();

        var result = await _service.CreateProgramAsync(dto, w => User.CanModify(w.CreatedByUserId));
        return result.Error == ScheduleError.None
            ? CreatedAtAction(nameof(GetById), new { id = result.Program!.Id }, result.Program)
            : ScheduleProblem(result);
    }

    /// <summary>Atnaujinti programą ir jos tvarkaraštį (tik autorius arba administratorius).</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(TrainingProgram), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TrainingProgram>> Update(string id, [FromBody] UpdateProgramDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (await _service.CheckCategoriesAsync(dto) is { } problem)
        {
            return BadRequest(new { message = problem });
        }

        var existing = await _service.GetProgramByIdAsync(id);
        if (existing == null) return ProgramNotFound(id);
        if (!User.CanModify(existing.CreatedByUserId)) return NotOwner();

        var result = await _service.UpdateProgramAsync(id, dto, w => User.CanModify(w.CreatedByUserId));
        if (result == null) return ProgramNotFound(id);

        return result.Error == ScheduleError.None ? Ok(result.Program) : ScheduleProblem(result);
    }

    /// <summary>Ištrinti programą (autorius arba administratorius). Jos treniruotės lieka kaip atskiros.</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _service.GetProgramByIdAsync(id);
        if (existing == null) return ProgramNotFound(id);
        if (!User.CanModify(existing.CreatedByUserId)) return NotOwner();

        await _service.DeleteProgramAsync(id);
        return NoContent();
    }

    private ActionResult ScheduleProblem(ScheduleResult result) => result.Error switch
    {
        ScheduleError.NotYourWorkout => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Message }),
        ScheduleError.InAnotherProgram => Conflict(new { message = result.Message }),
        _ => BadRequest(new { message = result.Message })
    };

    private NotFoundObjectResult ProgramNotFound(string id) =>
        NotFound(new { message = $"Programa su ID '{id}' nerasta." });

    private ObjectResult NotOwner() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "Galite keisti tik savo programas." });
}
