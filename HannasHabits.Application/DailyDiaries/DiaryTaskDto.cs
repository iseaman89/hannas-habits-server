namespace HannasHabits.Application.DailyDiaries;

/// <summary>One task of a day, as the client sends and receives it.</summary>
public record DiaryTaskDto(string Title, bool Done);
