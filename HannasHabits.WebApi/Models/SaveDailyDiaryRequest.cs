using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.DailyDiaries.Commands.SaveDailyDiary;
using HannasHabits.Domain.Enums;

namespace HannasHabits.WebApi.Models;

/// <summary>The whole entry of a day. Everything that is left out is cleared: the request replaces the stored entry.</summary>
/// <param name="Mood">1 (rough) to 5 (great).</param>
/// <param name="Body">0 (drained) to 100 (energised).</param>
/// <param name="Mind">0 (foggy) to 100 (clear).</param>
public record SaveDailyDiaryRequest(
    Mood? Mood,
    int? Body,
    int? Mind,
    string? Highlight,
    List<string>? Grateful,
    List<string>? Learned,
    List<DiaryTaskDto>? Tasks)
{
    public SaveDailyDiaryCommand ToCommand(DateOnly date) =>
        new(date, Mood, Body, Mind, Highlight, Grateful, Learned, Tasks);
}
