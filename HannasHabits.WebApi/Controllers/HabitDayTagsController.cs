using HannasHabits.Application.HabitDays.Commands.AttachTag;
using HannasHabits.Application.HabitDays.Commands.DetachTag;
using HannasHabits.Application.Tags;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

/// <summary>The tags of one day of a habit, independent of whether the day was completed.</summary>
[Authorize]
[ApiController]
[Route("api/habits/{habitId}/days/{date}/tags")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public class HabitDayTagsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HabitDayTagsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Puts a tag on the day. Idempotent. Answers with the tags the day has now.</summary>
    [HttpPut("{tagId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaggedDayDto>> Attach(Guid habitId, DateOnly date, Guid tagId,
        CancellationToken cancellationToken)
    {
        var day = await _mediator.Send(new AttachTagCommand(habitId, date, tagId), cancellationToken);
        return Ok(day);
    }

    /// <summary>Takes a tag off the day. Idempotent: a tag the day does not have is no error. Answers with the tags the day has now.</summary>
    [HttpDelete("{tagId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaggedDayDto>> Detach(Guid habitId, DateOnly date, Guid tagId,
        CancellationToken cancellationToken)
    {
        var day = await _mediator.Send(new DetachTagCommand(habitId, date, tagId), cancellationToken);
        return Ok(day);
    }
}
