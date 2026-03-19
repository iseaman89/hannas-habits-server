namespace HannasHabits.Application.HabitRecords.Commands.MarkCompleted;

public record HabitRecordDto(Guid Id, Guid HabitId, DateOnly Date);