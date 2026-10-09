using HannasHabits.Domain.Enums;
using MediatR;

namespace HannasHabits.Application.DailyDiaries.Commands.SaveDailyDiary;

/// <summary>
/// Saves the whole entry of a day (create or replace). What is missing is cleared: <c>null</c> mood/body/mind/highlight
/// and <c>null</c> lists mean "nothing written".
/// </summary>
public record SaveDailyDiaryCommand(
    DateOnly Date,
    Mood? Mood,
    int? Body,
    int? Mind,
    string? Highlight,
    IReadOnlyList<string>? Grateful,
    IReadOnlyList<string>? Learned,
    IReadOnlyList<DiaryTaskDto>? Tasks) : IRequest<Unit>;
