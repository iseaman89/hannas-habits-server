using MediatR;

namespace HannasHabits.Application.DailyDiaries.Commands.DeleteDailyDiary;

public record DeleteDailyDiaryCommand(DateOnly Date) : IRequest<Unit>;
