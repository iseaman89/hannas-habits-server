using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.RemoveSubHabit;

/// <summary>Removes a sub-habit with all its marks. The habit's own days stay as they are.</summary>
public record RemoveSubHabitCommand(Guid HabitId, Guid SubHabitId) : IRequest<Unit>;
