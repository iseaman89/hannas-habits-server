using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;

public class GetDailyDiaryByIdQueryHandler : IRequestHandler<GetDailyDiaryByIdQuery, DailyDiaryDetailsDto>
{
    private readonly IDailyDiaryQueries _dailyDiaryQueries;
    private readonly ICurrentUser _currentUser;

    public GetDailyDiaryByIdQueryHandler(IDailyDiaryQueries dailyDiaryQueries, ICurrentUser currentUser)
    {
        _dailyDiaryQueries = dailyDiaryQueries;
        _currentUser = currentUser;
    }

    public async Task<DailyDiaryDetailsDto> Handle(GetDailyDiaryByIdQuery request, CancellationToken cancellationToken)
    {
        var dailyDiary = await _dailyDiaryQueries.GetByIdAsync(_currentUser.UserId, request.Id, cancellationToken);

        return dailyDiary ?? throw new NotFoundException(nameof(DailyDiary), request.Id);
    }
}
