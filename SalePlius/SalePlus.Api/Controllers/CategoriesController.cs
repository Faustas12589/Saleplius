using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalePlus.Api.Services;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Controllers;

/// <summary>
/// Kategorijos: raumenų grupės, įranga ir programų tikslai. Skaityti gali visi, kurti, keisti ir trinti –
/// tik administratorius. Pratimai, treniruotės ir programos gali naudoti tik čia esančias reikšmes.
/// </summary>
[ApiController]
[Route("api/categories")]
[Produces("application/json", "application/xml")]
public class CategoriesController : ControllerBase
{
    private readonly IFirestoreService _service;

    public CategoriesController(IFirestoreService service)
    {
        _service = service;
    }

    /// <summary>Gauti kategorijas puslapiais. Filtras: ?type=MuscleGroup|Equipment|Goal.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<Category>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<Category>>> GetAll([FromQuery] string? type, [FromQuery] PageQuery paging)
    {
        if (!string.IsNullOrEmpty(type) && !CategoryTypes.All.Contains(type))
        {
            return BadRequest(new { message = "Tipas gali būti tik: MuscleGroup, Equipment arba Goal." });
        }

        var categories = await _service.GetCategoriesAsync(type);
        return Ok(categories.ToPage(paging, Request, User.IsInRole(AppRoles.Admin) ? "/api/categories" : null));
    }

    /// <summary>Gauti kategoriją pagal ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Category), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Category>> GetById(string id)
    {
        var category = await _service.GetCategoryByIdAsync(id);
        return category == null ? CategoryNotFound(id) : Ok(category);
    }

    /// <summary>Sukurti kategoriją (tik administratorius). 409 – toks pavadinimas jau yra.</summary>
    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Category), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Category>> Create([FromBody] CreateCategoryDto dto)
    {
        var result = await _service.CreateCategoryAsync(dto);
        return result.Error == CategoryError.None
            ? CreatedAtAction(nameof(GetById), new { id = result.Category!.Id }, result.Category)
            : Problem(result, dto.Name);
    }

    /// <summary>Pervadinti kategoriją (tik administratorius). Naujas pavadinimas pritaikomas visur, kur ji naudojama.</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(Category), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Category>> Update(string id, [FromBody] UpdateCategoryDto dto)
    {
        var result = await _service.UpdateCategoryAsync(id, dto);
        return result.Error == CategoryError.None ? Ok(result.Category) : Problem(result, id);
    }

    /// <summary>Ištrinti kategoriją (tik administratorius). 409 – jei ji dar naudojama.</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _service.DeleteCategoryAsync(id);
        return result.Error == CategoryError.None ? NoContent() : Problem(result, id);
    }

    private ActionResult Problem(CategoryResult result, string idOrName) => result.Error switch
    {
        CategoryError.NotFound => CategoryNotFound(idOrName),
        _ => Conflict(new { message = result.Message })
    };

    private NotFoundObjectResult CategoryNotFound(string id) => NotFound(new { message = $"Kategorija su ID '{id}' nerasta." });
}
