using HannasHabits.Domain.Enums;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryByDate;

/// <summary>The whole entry of a day. There is no id: the date is the key.</summary>
public record DailyDiaryDto(
    DateOnly Date,
    Mood? Mood,
    int? Body,
    int? Mind,
    string? Highlight,
    IReadOnlyList<string> Grateful,
    IReadOnlyList<string> Learned,
    IReadOnlyList<DiaryTaskDto> Tasks);
