namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;

public record DailyDiaryDetailsDto(Guid Id, DateOnly Date, string Text);