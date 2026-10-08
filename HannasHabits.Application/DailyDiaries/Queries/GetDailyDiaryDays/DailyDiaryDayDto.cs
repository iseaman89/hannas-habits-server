using HannasHabits.Domain.Enums;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;

/// <summary>A day that has an entry, reduced to what the calendar needs: the mood colours the day (<c>null</c> = entry without a mood).</summary>
public record DailyDiaryDayDto(DateOnly Date, Mood? Mood);
