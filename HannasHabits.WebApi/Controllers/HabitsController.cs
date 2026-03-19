using HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;
using HannasHabits.Application.DailyDiaries.Commands.DeleteDailyDiary;
using HannasHabits.Application.DailyDiaries.Commands.UpdateDailyDiary;
using HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;
using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Application.Habits.Commands.DeleteHabit;
using HannasHabits.Application.Habits.Commands.UpdateHabit;
using HannasHabits.Application.Habits.Queries.GetAllHabits;
using HannasHabits.Application.Habits.Queries.GetHabitById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HannasHabits.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HabitsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HabitsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetHabits(GetAllHabitsQuery query, CancellationToken cancellationToken)
    {
        var habits = await _mediator.Send(query, cancellationToken);
        return Ok(habits);
    }
    
    [HttpGet("id")]
    public async Task<IActionResult> GetHabitById(GetHabitByIdQuery query, CancellationToken cancellationToken)
    {
        var habit = await _mediator.Send(query, cancellationToken);
        return Ok(habit);
    }

    [HttpPost]
    public async Task<IActionResult> CreateHabit(CreateHabitCommand command, CancellationToken cancellationToken)
    {
       var habit = await _mediator.Send(command, cancellationToken);
       return Ok(habit);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateHabit(UpdateHabitCommand command, CancellationToken cancellationToken)
    {
        var unit = await _mediator.Send(command, cancellationToken);
        return Ok(unit);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteHabit(DeleteHabitCommand command, CancellationToken cancellationToken)
    {
        var unit = await _mediator.Send(command, cancellationToken);
        return Ok(unit);
    }
}