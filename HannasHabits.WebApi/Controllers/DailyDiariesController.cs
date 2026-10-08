using HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;
using HannasHabits.Application.DailyDiaries.Commands.DeleteDailyDiary;
using HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;
using HannasHabits.WebApi.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

// Routes are keyed by id for now; B6 redesigns the diary API around the date.
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

    [HttpGet]
    public async Task<ActionResult<List<DailyDiaryListItemDto>>> GetDailyDiaries(CancellationToken cancellationToken)
    {
        var dailyDiaries = await _mediator.Send(new GetAllDailyDiariesQuery(), cancellationToken);
        return Ok(dailyDiaries);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DailyDiaryDetailsDto>> GetDailyDiaryById(Guid id,
        CancellationToken cancellationToken)
    {
        var dailyDiary = await _mediator.Send(new GetDailyDiaryByIdQuery(id), cancellationToken);
        return Ok(dailyDiary);
    }

    /// <summary>Creates the entry of a day. Answers 409 if the user already has an entry for that day.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateDailyDiaryDto>> CreateDailyDiary(CreateDailyDiaryRequest request,
        CancellationToken cancellationToken)
    {
        var dailyDiary = await _mediator.Send(request.ToCommand(), cancellationToken);
        return CreatedAtAction(nameof(GetDailyDiaryById), new { id = dailyDiary.Id }, dailyDiary);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDailyDiary(Guid id, UpdateDailyDiaryRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(request.ToCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDailyDiary(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteDailyDiaryCommand(id), cancellationToken);
        return NoContent();
    }
}
