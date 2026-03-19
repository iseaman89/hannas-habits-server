using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.HabitRecords.Commands.UnmarkCompleted;
using HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HabitRecordsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HabitRecordsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecordsByHabit(GetRecordsByHabitQuery query,
        CancellationToken cancellationToken)
    {
        var habitRecords = await _mediator.Send(query, cancellationToken);
        return Ok(habitRecords);
    }

    [HttpPut]
    public async Task<IActionResult> MarkCompleted(MarkCompletedCommand command, CancellationToken cancellationToken)
    {
        var habitRecord = await _mediator.Send(command, cancellationToken);
        return Ok(habitRecord);
    }
    
    [HttpPut]
    public async Task<IActionResult> UnmarkCompleted(UnmarkCompletedCommand command, CancellationToken cancellationToken)
    {
        var unit = await _mediator.Send(command, cancellationToken);
        return Ok(unit);
    }
}