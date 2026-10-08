using MediatR;

namespace HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;

public record GetAllDailyDiariesQuery() : IRequest<List<DailyDiaryListItemDto>>;