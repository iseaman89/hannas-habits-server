using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.UpdateSubHabit;

/// <summary>Replaces the title and the weekdays of a sub-habit; its marks stay.</summary>
public record UpdateSubHabitCommand(Guid HabitId, Guid SubHabitId, string Title, List<DayOfWeek>? Weekdays)
    : IRequest<SubHabitDto>;
