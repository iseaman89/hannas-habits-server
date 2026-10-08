using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Tests.Fakes;

/// <summary>Short ways to build valid domain objects for the arrange part of a test.</summary>
internal static class Make
{
    public static Habit NewHabit(
        Guid userId, string title = "Read", DateOnly? start = null, HabitSchedule? schedule = null,
        string? description = null, IEnumerable<DateOnly>? completed = null)
    {
        var habit = Habit.Create(
            userId, HabitTitle.Create(title), start ?? new DateOnly(2026, 1, 1), description, schedule);

        foreach (var day in completed ?? [])
            habit.MarkCompleted(day);

        return habit;
    }

    public static DailyDiary NewDiary(Guid userId, DateOnly date, Mood? mood = Mood.Ok, string? highlight = null)
        => DailyDiary.Create(userId, date, DiaryContent.Create(mood, null, null, highlight, null, null, null));

    public static Resolution NewResolution(Guid userId, int year = 2026, string title = "Read 12 books", Guid? habitId = null)
        => Resolution.Create(userId, year, title, habitId, resolutionsInYear: 0);
}
