using MediatR;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

public class CreateHabitCommand : IRequest<CreateHabitDto>
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>The first day the habit applies, in the caller's time zone; <c>null</c> = today (server date).</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>The planned days of the week; <c>null</c> = every day.</summary>
    public List<DayOfWeek>? Schedule { get; set; }
}
