using HannasHabits.Application.DailyDiaries.Commands.DeleteDailyDiary;
using HannasHabits.Application.DailyDiaries.Commands.SaveDailyDiary;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryByDate;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;
using HannasHabits.WebApi.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

/// <summary>The diary is one document per user and day; the date is its key, so clients never handle ids.</summary>
[Authorize]
[ApiController]
[Route("api/daily-diaries")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class DailyDiariesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DailyDiariesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Days that have an entry, oldest first, with their mood (for the calendar). <c>from</c>/<c>to</c> are inclusive and optional.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<DailyDiaryDayDto>>> GetDays(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var days = await _mediator.Send(new GetDailyDiaryDaysQuery(from, to), cancellationToken);
        return Ok(days);
    }

    /// <summary>The entry of a day. 404 means nothing is written for that day yet.</summary>
    [HttpGet("{date}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DailyDiaryDto>> GetByDate(DateOnly date, CancellationToken cancellationToken)
    {
        var dailyDiary = await _mediator.Send(new GetDailyDiaryByDateQuery(date), cancellationToken);
        return Ok(dailyDiary);
    }

    /// <summary>
    /// Saves the whole entry of a day (creates or replaces it; idempotent, the last write wins). An entry that contains
    /// nothing is removed instead of stored. No body is returned: the client keeps its own copy while it is typing.
    /// </summary>
    [HttpPut("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Save(DateOnly date, SaveDailyDiaryRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(request.ToCommand(date), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(DateOnly date, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteDailyDiaryCommand(date), cancellationToken);
        return NoContent();
    }
}
