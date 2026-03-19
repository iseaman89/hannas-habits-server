namespace HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;

public record DailyDiaryListItemDto(Guid Id, DateOnly Date, string Text);