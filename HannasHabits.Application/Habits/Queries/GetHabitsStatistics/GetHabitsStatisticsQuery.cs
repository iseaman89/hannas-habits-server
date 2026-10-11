using MediatR;

namespace HannasHabits.Application.Habits.Queries.GetHabitsStatistics;

/// <summary>
/// How much every habit of the user (and every part of it) was planned and done between <paramref name="From"/> and
/// <paramref name="To"/> (inclusive, both required), judged as of <paramref name="AsOf"/> (the caller's local today;
/// <c>null</c> = server date).
/// </summary>
public record GetHabitsStatisticsQuery(DateOnly? From, DateOnly? To, DateOnly? AsOf) : IRequest<List<HabitStatisticsDto>>;
