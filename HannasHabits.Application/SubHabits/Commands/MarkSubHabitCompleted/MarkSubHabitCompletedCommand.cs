using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.MarkSubHabitCompleted;

/// <summary>Ticks a sub-habit on a day; the habit's day follows. Answers with the day as it is afterwards.</summary>
public record MarkSubHabitCompletedCommand(Guid HabitId, Guid SubHabitId, DateOnly Date) : IRequest<HabitDayDto>;
