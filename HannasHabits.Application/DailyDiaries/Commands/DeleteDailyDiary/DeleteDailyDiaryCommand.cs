using MediatR;

namespace HannasHabits.Application.DailyDiaries.Commands.DeleteDailyDiary;

public record DeleteDailyDiaryCommand(Guid Id) : IRequest<Unit>;