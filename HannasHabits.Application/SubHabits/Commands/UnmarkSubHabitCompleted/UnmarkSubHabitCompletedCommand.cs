using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.UnmarkSubHabitCompleted;

/// <summary>Takes a sub-habit's tick back; the habit's day follows. Answers with the day as it is afterwards.</summary>
public record UnmarkSubHabitCompletedCommand(Guid HabitId, Guid SubHabitId, DateOnly Date) : IRequest<HabitDayDto>;
