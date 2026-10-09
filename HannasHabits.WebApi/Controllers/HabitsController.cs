using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Application.Habits.Commands.DeleteHabit;
using HannasHabits.Application.Habits.Queries.GetAllHabits;
using HannasHabits.Application.Habits.Queries.GetHabitById;
using HannasHabits.Application.Habits.Queries.GetHabitsOverview;
using HannasHabits.WebApi.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/habits")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class HabitsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HabitsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HabitListItemDto>>> GetHabits(CancellationToken cancellationToken)
    {
        var habits = await _mediator.Send(new GetAllHabitsQuery(), cancellationToken);
        return Ok(habits);
    }

    /// <summary>
    /// Everything the habit grid and the "today" panel need in one call: per habit its plan, start date, the completed
    /// days between <c>from</c> and <c>to</c> (inclusive, both required) and the current streak as of <c>asOf</c> - the
    /// caller's local today; omitted = the server's UTC date.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<HabitOverviewDto>>> GetOverview(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] DateOnly? asOf,
        CancellationToken cancellationToken)
    {
        var overview = await _mediator.Send(new GetHabitsOverviewQuery(from, to, asOf), cancellationToken);
        return Ok(overview);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HabitDetailsDto>> GetHabitById(Guid id, CancellationToken cancellationToken)
    {
        var habit = await _mediator.Send(new GetHabitByIdQuery(id), cancellationToken);
        return Ok(habit);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateHabitDto>> CreateHabit(CreateHabitRequest request,
        CancellationToken cancellationToken)
    {
        var habit = await _mediator.Send(request.ToCommand(), cancellationToken);
        return CreatedAtAction(nameof(GetHabitById), new { id = habit.Id }, habit);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateHabit(Guid id, UpdateHabitRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(request.ToCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteHabit(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteHabitCommand(id), cancellationToken);
        return NoContent();
    }
}
