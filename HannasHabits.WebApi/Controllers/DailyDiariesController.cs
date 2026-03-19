using HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;
using HannasHabits.Application.DailyDiaries.Commands.DeleteDailyDiary;
using HannasHabits.Application.DailyDiaries.Commands.UpdateDailyDiary;
using HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DailyDiariesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DailyDiariesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetDailyDiaries(GetAllDailyDiariesQuery query, CancellationToken cancellationToken)
    {
        var dailyDiaries = await _mediator.Send(query, cancellationToken);
        return Ok(dailyDiaries);
    }
    
    [HttpGet("id")]
    public async Task<IActionResult> GetDailyDiaryById(GetDailyDiaryByIdQuery query, CancellationToken cancellationToken)
    {
        var dailyDiary = await _mediator.Send(query, cancellationToken);
        return Ok(dailyDiary);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDailyDiary(CreateDailyDiaryCommand command, CancellationToken cancellationToken)
    {
       var dailyDiary = await _mediator.Send(command, cancellationToken);
       return Ok(dailyDiary);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateDailyDiary(UpdateDailyDiaryCommand command, CancellationToken cancellationToken)
    {
        var unit = await _mediator.Send(command, cancellationToken);
        return Ok(unit);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteDailyDiary(DeleteDailyDiaryCommand command, CancellationToken cancellationToken)
    {
        var unit = await _mediator.Send(command, cancellationToken);
        return Ok(unit);
    }
}