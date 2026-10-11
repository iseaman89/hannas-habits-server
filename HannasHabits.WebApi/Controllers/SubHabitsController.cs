using HannasHabits.Application.SubHabits;
using HannasHabits.Application.SubHabits.Commands.MarkSubHabitCompleted;
using HannasHabits.Application.SubHabits.Commands.RemoveSubHabit;
using HannasHabits.Application.SubHabits.Commands.UnmarkSubHabitCompleted;
using HannasHabits.WebApi.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

/// <summary>
/// The parts of a habit ("Legs" of "Sport"): planned on some of the habit's weekdays and ticked on their own. A day of
/// the habit is done when all of that day's due parts are, so a tick here can complete or take back the habit's day -
/// the answers of the tick routes say how the day stands afterwards. The habit's own tick
/// (<c>PUT /api/habits/{id}/records/{date}</c>) ticks the parts that are due that day.
/// </summary>
[Authorize]
[ApiController]
[Route("api/habits/{habitId}/sub-habits")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public class SubHabitsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubHabitsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Adds a part to the habit. At most 10 per habit.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubHabitDto>> Add(Guid habitId, SubHabitRequest request,
        CancellationToken cancellationToken)
    {
        var subHabit = await _mediator.Send(request.ToAddCommand(habitId), cancellationToken);

        // There is no single-sub-habit route; the habit's details carry its parts.
        return CreatedAtAction(nameof(HabitsController.GetHabitById), "Habits", new { id = habitId }, subHabit);
    }

    /// <summary>Renames a part and / or moves it to other weekdays. Its ticks stay.</summary>
    [HttpPut("{subHabitId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubHabitDto>> Update(Guid habitId, Guid subHabitId, SubHabitRequest request,
        CancellationToken cancellationToken)
    {
        var subHabit = await _mediator.Send(request.ToUpdateCommand(habitId, subHabitId), cancellationToken);
        return Ok(subHabit);
    }

    /// <summary>Removes a part with all its ticks. The habit's own days stay as they are.</summary>
    [HttpDelete("{subHabitId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Remove(Guid habitId, Guid subHabitId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveSubHabitCommand(habitId, subHabitId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Ticks a part on a day. Idempotent. When this was the last of the day's due parts the habit's day is completed
    /// too. Answers with the day as it is afterwards.
    /// </summary>
    [HttpPut("{subHabitId}/records/{date}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HabitDayDto>> MarkCompleted(Guid habitId, Guid subHabitId, DateOnly date,
        CancellationToken cancellationToken)
    {
        var day = await _mediator.Send(new MarkSubHabitCompletedCommand(habitId, subHabitId, date), cancellationToken);
        return Ok(day);
    }

    /// <summary>
    /// Takes a part's tick back. Idempotent: a part that is not done is no error. A day that was done because all its
    /// due parts were is not done any more. Answers with the day as it is afterwards.
    /// </summary>
    [HttpDelete("{subHabitId}/records/{date}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HabitDayDto>> UnmarkCompleted(Guid habitId, Guid subHabitId, DateOnly date,
        CancellationToken cancellationToken)
    {
        var day = await _mediator.Send(new UnmarkSubHabitCompletedCommand(habitId, subHabitId, date), cancellationToken);
        return Ok(day);
    }
}
