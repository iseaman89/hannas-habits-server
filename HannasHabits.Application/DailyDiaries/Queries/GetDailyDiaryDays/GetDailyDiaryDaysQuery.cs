using MediatR;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;

/// <summary>The days with an entry between <paramref name="From"/> and <paramref name="To"/> (inclusive, both optional).</summary>
public record GetDailyDiaryDaysQuery(DateOnly? From, DateOnly? To) : IRequest<List<DailyDiaryDayDto>>;
