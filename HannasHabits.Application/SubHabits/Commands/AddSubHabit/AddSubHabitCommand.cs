using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.AddSubHabit;

/// <param name="Weekdays">The weekdays the part is planned on; they must be days the habit itself is planned on.</param>
public record AddSubHabitCommand(Guid HabitId, string Title, List<DayOfWeek>? Weekdays) : IRequest<SubHabitDto>;
