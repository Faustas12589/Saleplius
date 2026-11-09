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
public class UsersController : ControllerBase
{
    private readonly IFirestoreService _service;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IFirestoreService service, ILogger<UsersController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Gauti visus vartotojų profilius (tik administratoriui).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(PagedResult<UserProfile>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<UserProfile>>> GetAll([FromQuery] string? search, [FromQuery] PageQuery paging)
    {
        var users = (await _service.GetAllUsersAsync())
            .Where(u => string.IsNullOrWhiteSpace(search)
                || u.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
                || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));

        return Ok(users.ToPage(paging, Request));
    }

    /// <summary>
    /// Vartotojo profilis – vienas resursas, sudarytas iš kelių esybių: vartotojo, jo programų,
    /// treniruočių (ir jų įvertinimų), pratimų bei išsaugotų įrašų. El. paštas ir išsaugotų skaičiai
    /// rodomi tik pačiam vartotojui arba administratoriui.
    /// </summary>
    [HttpGet("{id}/profile")]
    [ProducesResponseType(typeof(UserProfileDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDetails>> GetProfile(string id)
    {
        var includePrivate = User.Identity?.IsAuthenticated == true && User.CanModify(id);
        var profile = await _service.GetUserProfileDetailsAsync(id, includePrivate);
        if (profile == null)
        {
            return NotFound(new { message = $"Vartotojas su ID '{id}' nerastas." });
        }

        profile.Links =
        [
            new("self", $"/api/users/{id}/profile"),
            new("user", $"/api/users/{id}"),
            new("programs", $"/api/programs?authorId={id}"),
            new("workouts", $"/api/workouts?authorId={id}"),
            new("exercises", $"/api/exercises?authorId={id}")
        ];
        return Ok(profile);
    }

    /// <summary>
    /// 19. Gauti vartotojo profilį pagal vartotojo ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile>> GetById(string id)
    {
        var user = await _service.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Vartotojas su ID '{id}' nerastas." });
        }

        // Email is private: only the user themselves or an admin sees it.
        if (User.Identity?.IsAuthenticated != true || !User.CanModify(id))
        {
            user.Email = string.Empty;
        }

        return Ok(user);
    }

    /// <summary>
    /// 20. Užregistruoti naują vartotoją.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfile>> Create([FromBody] CreateUserDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (user, error) = await _service.CreateUserAsync(dto);
        if (error != null)
        {
            return Conflict(new { message = error });
        }

        return CreatedAtAction(nameof(GetById), new { id = user!.Id }, user);
    }

    /// <summary>
    /// 21. Administratoriaus veiksmas: pakeisti vartotojo rolę (pvz., paaukštinti į Admin).
    /// </summary>
    [HttpPatch("{id}/role")]
    [Authorize(Policy = "RequireAdmin")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile>> UpdateRole(string id, [FromBody] UpdateUserRoleDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _service.UpdateUserRoleAsync(id, dto.Role);
        if (user == null)
        {
            return NotFound(new { message = $"Vartotojas su ID '{id}' nerastas." });
        }

        return Ok(user);
    }

    /// <summary>
    /// 22. Administratoriaus veiksmas: ištrinti registruoto vartotojo paskyrą
    /// (pvz. dėl pakartotinių pažeidimų). Su ?deleteContent=true ištrinamos ir jo treniruotės bei pratimai.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, [FromQuery] bool deleteContent = false)
    {
        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return BadRequest(new { message = "Negalite ištrinti savo paties paskyros." });
        }

        var deleted = await _service.DeleteUserAsync(id, deleteContent);
        if (!deleted)
        {
            return NotFound(new { message = $"Vartotojas su ID '{id}' nerastas ištrynimui." });
        }

        return NoContent();
    }

    /// <summary>
    /// 23. Gauti visus konkretaus vartotojo išsaugotus treniruočių rinkinius (favoritus).
    /// </summary>
    [HttpGet("{userId}/saved-workouts")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(List<SavedWorkout>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<SavedWorkout>>> GetSavedWorkouts(string userId)
    {
        if (!User.CanModify(userId))
        {
            return NotSelf();
        }

        var list = await _service.GetSavedWorkoutsForUserAsync(userId);
        return Ok(list);
    }

    /// <summary>
    /// 24. Išsisaugoti treniruotę į asmeninių išsaugotų treniruočių sąrašą.
    /// </summary>
    [HttpPost("{userId}/saved-workouts/{workoutId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(SavedWorkout), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SavedWorkout>> SaveWorkout(string userId, string workoutId)
    {
        if (!User.CanModify(userId))
        {
            return NotSelf();
        }

        var (saved, error) = await _service.SaveWorkoutForUserAsync(userId, workoutId);
        if (error != null)
        {
            if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = error });
            }

            if (error.Contains("already saved", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = error });
            }

            return BadRequest(new { message = error });
        }

        return CreatedAtAction(nameof(GetSavedWorkouts), new { userId }, saved);
    }

    /// <summary>
    /// 25. Pašalinti treniruotę iš asmeninių išsaugotų treniruočių sąrašo.
    /// </summary>
    [HttpDelete("{userId}/saved-workouts/{workoutId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnsaveWorkout(string userId, string workoutId)
    {
        if (!User.CanModify(userId))
        {
            return NotSelf();
        }

        var deleted = await _service.UnsaveWorkoutForUserAsync(userId, workoutId);
        if (!deleted)
        {
            return NotFound(new { message = $"Išsaugota treniruotė '{workoutId}' vartotojui '{userId}' nerasta." });
        }

        return NoContent();
    }

    // ---------- Saved programs & exercises (same rules as saved workouts) ----------

    /// <summary>Gauti vartotojo išsaugotas programas.</summary>
    [HttpGet("{userId}/saved-programs")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(List<SavedItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<List<SavedItem>>> GetSavedPrograms(string userId) => GetSaved(userId, SavedItemType.Program);

    /// <summary>Išsisaugoti programą.</summary>
    [HttpPost("{userId}/saved-programs/{programId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(SavedItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<SavedItem>> SaveProgram(string userId, string programId) =>
        Save(userId, SavedItemType.Program, programId, nameof(GetSavedPrograms));

    /// <summary>Pašalinti programą iš išsaugotų.</summary>
    [HttpDelete("{userId}/saved-programs/{programId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UnsaveProgram(string userId, string programId) => Unsave(userId, SavedItemType.Program, programId);

    /// <summary>Gauti vartotojo išsaugotus pratimus.</summary>
    [HttpGet("{userId}/saved-exercises")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(List<SavedItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<List<SavedItem>>> GetSavedExercises(string userId) => GetSaved(userId, SavedItemType.Exercise);

    /// <summary>Išsisaugoti pratimą.</summary>
    [HttpPost("{userId}/saved-exercises/{exerciseId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(SavedItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<SavedItem>> SaveExercise(string userId, string exerciseId) =>
        Save(userId, SavedItemType.Exercise, exerciseId, nameof(GetSavedExercises));

    /// <summary>Pašalinti pratimą iš išsaugotų.</summary>
    [HttpDelete("{userId}/saved-exercises/{exerciseId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UnsaveExercise(string userId, string exerciseId) => Unsave(userId, SavedItemType.Exercise, exerciseId);

    private async Task<ActionResult<List<SavedItem>>> GetSaved(string userId, string itemType)
    {
        if (!User.CanModify(userId)) return NotSelf();
        return Ok(await _service.GetSavedItemsAsync(userId, itemType));
    }

    private async Task<ActionResult<SavedItem>> Save(string userId, string itemType, string itemId, string listAction)
    {
        if (!User.CanModify(userId)) return NotSelf();

        var (saved, error) = await _service.SaveItemAsync(userId, itemType, itemId);
        var what = itemType == SavedItemType.Program ? "Programa" : "Pratimas";
        return error switch
        {
            SaveItemError.None => CreatedAtAction(listAction, new { userId }, saved),
            SaveItemError.NotFound => NotFound(new { message = $"{what} su ID '{itemId}' nerasta(-s)." }),
            _ => Conflict(new { message = $"{what} jau išsaugota(-s)." })
        };
    }

    private async Task<IActionResult> Unsave(string userId, string itemType, string itemId)
    {
        if (!User.CanModify(userId)) return NotSelf();

        return await _service.UnsaveItemAsync(userId, itemType, itemId)
            ? NoContent()
            : NotFound(new { message = $"Išsaugotas įrašas '{itemId}' nerastas." });
    }

    // Saved items are private: only the user themselves or an admin may access them.
    private ObjectResult NotSelf() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "Galite tvarkyti tik savo išsaugotus įrašus." });
}
