using MediatR;

namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

/// <summary>
/// All habits of the user with the days completed between <paramref name="From"/> and <paramref name="To"/> (inclusive,
/// both required) and the current streak as of <paramref name="AsOf"/> (the caller's local today; <c>null</c> = server date).
/// </summary>
public record GetHabitsOverviewQuery(DateOnly? From, DateOnly? To, DateOnly? AsOf) : IRequest<List<HabitOverviewDto>>;
