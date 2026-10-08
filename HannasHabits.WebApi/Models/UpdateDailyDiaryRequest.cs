using HannasHabits.Application.DailyDiaries.Commands.UpdateDailyDiary;

namespace HannasHabits.WebApi.Models;

public record UpdateDailyDiaryRequest(string Text)
{
    public UpdateDailyDiaryCommand ToCommand(Guid id) => new() { Id = id, Text = Text };
}
