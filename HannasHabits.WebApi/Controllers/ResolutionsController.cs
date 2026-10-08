using HannasHabits.Application.Resolutions;
using HannasHabits.Application.Resolutions.Commands.AddResolution;
using HannasHabits.Application.Resolutions.Commands.DeleteResolution;
using HannasHabits.Application.Resolutions.Queries.GetResolutionsByYear;
using HannasHabits.WebApi.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

/// <summary>The resolutions of a year; the year is part of every route, an item is addressed by year + id.</summary>
[Authorize]
[ApiController]
[Route("api/resolutions")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ResolutionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ResolutionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The year's resolutions in creation order. A year without any answers 200 with an empty list.</summary>
    [HttpGet("{year}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<ResolutionDto>>> GetByYear(int year, CancellationToken cancellationToken)
    {
        var resolutions = await _mediator.Send(new GetResolutionsByYearQuery(year), cancellationToken);
        return Ok(resolutions);
    }

    [HttpPost("{year}/items")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResolutionDto>> Add(int year, AddResolutionRequest request,
        CancellationToken cancellationToken)
    {
        var resolution = await _mediator.Send(request.ToCommand(year), cancellationToken);

        // There is no single-item route; the year's list is where the new item shows up.
        return CreatedAtAction(nameof(GetByYear), new { year }, resolution);
    }

    /// <summary>Replaces title, kept flag and habit link (this is also how an item is marked kept or open again).</summary>
    [HttpPut("{year}/items/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int year, Guid id, UpdateResolutionRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(request.ToCommand(year, id), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{year}/items/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int year, Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteResolutionCommand(year, id), cancellationToken);
        return NoContent();
    }
}
