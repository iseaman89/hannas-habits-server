using HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;

namespace HannasHabits.WebApi.Models;

public record CreateDailyDiaryRequest(DateOnly Date, string Text)
{
    public CreateDailyDiaryCommand ToCommand() => new() { Date = Date, Text = Text };
}
