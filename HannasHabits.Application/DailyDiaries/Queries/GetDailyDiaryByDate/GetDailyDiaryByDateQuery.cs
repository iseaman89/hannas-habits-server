using MediatR;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryByDate;

public record GetDailyDiaryByDateQuery(DateOnly Date) : IRequest<DailyDiaryDto>;
