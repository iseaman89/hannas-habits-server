using MediatR;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;

public record GetDailyDiaryByIdQuery(Guid Id) : IRequest<DailyDiaryDetailsDto>;