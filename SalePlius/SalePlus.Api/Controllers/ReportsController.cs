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
public class ReportsController : ControllerBase
{
    private readonly IFirestoreService _service;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IFirestoreService service, ILogger<ReportsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 26. Administratoriaus veiksmas: peržiūrėti visus sistemos taisyklių pažeidimų pranešimus / bilietus (reportus).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(PagedResult<ViolationReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ViolationReport>>> GetAll([FromQuery] string? status, [FromQuery] PageQuery paging)
    {
        var reports = await _service.GetReportsAsync(status);
        return Ok(reports.ToPage(paging, Request));
    }

    /// <summary>
    /// 27. Užregistruotas vartotojas gali pateikti reportą apie taisyklių nusižengimą (pratimą, treniruotę, komentarą, vartotoją).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireUser")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(ViolationReport), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ViolationReport>> Create([FromBody] CreateReportDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Reporter always comes from the token, whatever the body says.
        dto.ReportedByUserId = User.GetUserId();

        var report = await _service.CreateReportAsync(dto);
        return CreatedAtAction(nameof(GetAll), new { status = report.Status }, report);
    }

    /// <summary>
    /// 28. Administratoriaus veiksmas: pakeisti bilieto / reporto statusą (Pending, Reviewed, Resolved, Dismissed).
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize(Policy = "RequireAdmin")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(ViolationReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ViolationReport>> UpdateStatus(string id, [FromBody] UpdateReportStatusDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var updated = await _service.UpdateReportStatusAsync(id, dto.Status);
        if (updated == null)
        {
            return NotFound(new { message = $"Pranešimas su ID '{id}' nerastas." });
        }

        return Ok(updated);
    }
}
