namespace HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;

public record CreateDailyDiaryDto(Guid Id, DateOnly Date, string Text);
