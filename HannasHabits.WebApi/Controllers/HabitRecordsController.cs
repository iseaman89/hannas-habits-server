using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.HabitRecords.Commands.UnmarkCompleted;
using HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/habits/{habitId}/records")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public class HabitRecordsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HabitRecordsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Completed days of a habit, oldest first. <c>from</c>/<c>to</c> are inclusive and optional.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<HabitRecordDto>>> GetRecords(Guid habitId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var records = await _mediator.Send(new GetRecordsByHabitQuery(habitId, from, to), cancellationToken);
        return Ok(records);
    }

    /// <summary>Marks the day as completed. Idempotent: marking an already completed day returns the existing record.</summary>
    [HttpPut("{date}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HabitRecordDto>> MarkCompleted(Guid habitId, DateOnly date,
        CancellationToken cancellationToken)
    {
        var record = await _mediator.Send(new MarkCompletedCommand(habitId, date), cancellationToken);
        return Ok(record);
    }

    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnmarkCompleted(Guid habitId, DateOnly date, CancellationToken cancellationToken)
    {
        await _mediator.Send(new UnmarkCompletedCommand(habitId, date), cancellationToken);
        return NoContent();
    }
}
