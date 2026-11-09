using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalePlus.Api.Services;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Controllers;

/// <summary>
/// Hierarchiniai (įdėtiniai) maršrutai per visus 3 lygius:
///   /api/programs/{programId}/workouts[/{workoutId}]
///   /api/programs/{programId}/workouts/{workoutId}/exercises[/{entryId}]
/// Apimtis (scoping) tikrinama per URL: jei treniruotė nepriklauso nurodytai programai,
/// arba įrašas – nurodytai treniruotei, grąžinamas 404, net jei toks ID egzistuoja kitur.
/// </summary>
[ApiController]
[Route("api/programs/{programId}/workouts")]
[Produces("application/json", "application/xml")]
public class ProgramWorkoutsController : ControllerBase
{
    private readonly IFirestoreService _service;

    public ProgramWorkoutsController(IFirestoreService service)
    {
        _service = service;
    }

    // ===================== Level 2: workouts of a program =====================

    /// <summary>Gauti visas programai priklausančias treniruotes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Workout>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<Workout>>> GetWorkouts(string programId)
    {
        var workouts = await _service.GetProgramWorkoutsAsync(programId);
        return workouts == null ? ProgramNotFound(programId) : Ok(workouts);
    }

    /// <summary>Sukurti naują treniruotę programoje. "days" (1–7) – kuriomis savaitės dienomis ji atliekama.</summary>
    [HttpPost]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Workout), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Workout>> CreateWorkout(string programId, [FromBody] CreateWorkoutDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (await _service.CheckCategoriesAsync(dto) is { } problem)
        {
            return BadRequest(new { message = problem });
        }

        var program = await _service.GetProgramByIdAsync(programId);
        if (program == null) return ProgramNotFound(programId);
        if (!User.CanModify(program.CreatedByUserId)) return Forbidden("Treniruotes galite kurti tik savo programose.");

        var daysProblem = CheckDays(dto.Days, required: true);
        if (daysProblem != null) return daysProblem;

        dto.CreatedByUserId = User.GetUserId();
        dto.CreatedByUserName = User.GetUserName();

        var workout = await _service.CreateWorkoutInProgramAsync(programId, dto);
        return CreatedAtAction(nameof(GetWorkout), new { programId, workoutId = workout.Id }, workout);
    }

    /// <summary>Gauti programos treniruotę. 404, jei ji nepriklauso šiai programai.</summary>
    [HttpGet("{workoutId}")]
    [ProducesResponseType(typeof(Workout), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Workout>> GetWorkout(string programId, string workoutId)
    {
        var (workout, notFound) = await FindWorkoutAsync(programId, workoutId);
        return notFound ?? Ok(workout);
    }

    /// <summary>Atnaujinti programos treniruotę (pavadinimas, aprašymas, dienos...). Tik autorius arba administratorius.</summary>
    [HttpPut("{workoutId}")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Workout), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Workout>> UpdateWorkout(string programId, string workoutId, [FromBody] UpdateWorkoutDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (await _service.CheckCategoriesAsync(dto) is { } problem)
        {
            return BadRequest(new { message = problem });
        }

        var (workout, notFound) = await FindWorkoutAsync(programId, workoutId);
        if (notFound != null) return notFound;
        if (!User.CanModify(workout!.CreatedByUserId)) return Forbidden("Galite keisti tik savo treniruotes.");

        if (dto.Days != null)
        {
            var daysProblem = CheckDays(dto.Days, required: true);
            if (daysProblem != null) return daysProblem;
        }

        return Ok(await _service.UpdateWorkoutAsync(workoutId, dto));
    }

    /// <summary>Ištrinti programos treniruotę (kartu su jos pratimais). Tik autorius arba administratorius.</summary>
    [HttpDelete("{workoutId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkout(string programId, string workoutId)
    {
        var (workout, notFound) = await FindWorkoutAsync(programId, workoutId);
        if (notFound != null) return notFound;
        if (!User.CanModify(workout!.CreatedByUserId)) return Forbidden("Galite ištrinti tik savo treniruotes.");

        await _service.DeleteWorkoutAsync(workoutId);
        return NoContent();
    }

    // ===================== Level 3: exercise entries of a workout =====================

    /// <summary>Gauti visus treniruotės pratimus (programa → treniruotė → pratimai).</summary>
    [HttpGet("{workoutId}/exercises")]
    [ProducesResponseType(typeof(List<WorkoutExerciseItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<WorkoutExerciseItem>>> GetEntries(string programId, string workoutId)
    {
        var (workout, notFound) = await FindWorkoutAsync(programId, workoutId);
        if (notFound != null) return notFound;

        return Ok(workout!.Exercises.Select(e => e.WithLinks(workout, User)).ToList());
    }

    /// <summary>Pridėti pratimą iš bibliotekos į treniruotę (serijos, pakartojimai, pastabos).</summary>
    [HttpPost("{workoutId}/exercises")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(WorkoutExerciseItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutExerciseItem>> CreateEntry(string programId, string workoutId, [FromBody] AddWorkoutExerciseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (workout, notFound) = await FindWorkoutAsync(programId, workoutId);
        if (notFound != null) return notFound;
        if (!User.CanModify(workout!.CreatedByUserId)) return Forbidden("Galite keisti tik savo treniruotes.");

        var entry = await _service.AddWorkoutEntryAsync(workoutId, dto);
        if (entry == null) return UnknownExercise(dto.ExerciseId);

        return CreatedAtAction(nameof(GetEntry), new { programId, workoutId, entryId = entry.Id }, entry.WithLinks(workout, User));
    }

    /// <summary>Gauti vieną treniruotės pratimą.</summary>
    [HttpGet("{workoutId}/exercises/{entryId}")]
    [ProducesResponseType(typeof(WorkoutExerciseItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutExerciseItem>> GetEntry(string programId, string workoutId, string entryId)
    {
        var (workout, notFound) = await FindWorkoutAsync(programId, workoutId);
        if (notFound != null) return notFound;

        var entry = workout!.Exercises.FirstOrDefault(e => e.Id == entryId);
        return entry == null ? EntryNotFound(entryId) : Ok(entry.WithLinks(workout, User));
    }

    /// <summary>Atnaujinti treniruotės pratimą (kitas pratimas, serijos, pakartojimai, pastabos).</summary>
    [HttpPut("{workoutId}/exercises/{entryId}")]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(WorkoutExerciseItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutExerciseItem>> UpdateEntry(string programId, string workoutId, string entryId, [FromBody] AddWorkoutExerciseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (workout, notFound) = await FindWorkoutAsync(programId, workoutId);
        if (notFound != null) return notFound;
        if (workout!.Exercises.All(e => e.Id != entryId)) return EntryNotFound(entryId);
        if (!User.CanModify(workout.CreatedByUserId)) return Forbidden("Galite keisti tik savo treniruotes.");

        var entry = await _service.UpdateWorkoutEntryAsync(workoutId, entryId, dto);
        return entry == null ? UnknownExercise(dto.ExerciseId) : Ok(entry.WithLinks(workout, User));
    }

    /// <summary>Pašalinti pratimą iš treniruotės.</summary>
    [HttpDelete("{workoutId}/exercises/{entryId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntry(string programId, string workoutId, string entryId)
    {
        var (workout, notFound) = await FindWorkoutAsync(programId, workoutId);
        if (notFound != null) return notFound;
        if (workout!.Exercises.All(e => e.Id != entryId)) return EntryNotFound(entryId);
        if (!User.CanModify(workout.CreatedByUserId)) return Forbidden("Galite keisti tik savo treniruotes.");

        await _service.DeleteWorkoutEntryAsync(workoutId, entryId);
        return NoContent();
    }

    // ===================== helpers =====================

    /// <summary>Loads the workout and checks it belongs to the program in the URL (scoping).</summary>
    private async Task<(Workout? Workout, ActionResult? NotFound)> FindWorkoutAsync(string programId, string workoutId)
    {
        var workouts = await _service.GetProgramWorkoutsAsync(programId);
        if (workouts == null) return (null, ProgramNotFound(programId));

        var workout = workouts.FirstOrDefault(w => w.Id == workoutId);
        return workout == null
            ? (null, NotFound(new { message = $"Treniruotė '{workoutId}' nepriklauso programai '{programId}'." }))
            : (workout, null);
    }

    private ActionResult? CheckDays(List<int> days, bool required)
    {
        if (required && days.Count == 0)
            return BadRequest(new { message = "Nurodykite bent vieną savaitės dieną (days: 1–7)." });
        if (days.Any(d => d is < 1 or > 7))
            return BadRequest(new { message = "Savaitės dienos turi būti nuo 1 (pirmadienis) iki 7 (sekmadienis)." });
        return null;
    }

    private NotFoundObjectResult ProgramNotFound(string id) => NotFound(new { message = $"Programa su ID '{id}' nerasta." });
    private NotFoundObjectResult EntryNotFound(string id) => NotFound(new { message = $"Pratimo įrašas '{id}' šioje treniruotėje nerastas." });
    private BadRequestObjectResult UnknownExercise(string id) => BadRequest(new { message = $"Pratimas su ID '{id}' bibliotekoje nerastas." });
    private ObjectResult Forbidden(string message) => StatusCode(StatusCodes.Status403Forbidden, new { message });
}
