using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryByDate;

public class GetDailyDiaryByDateQueryHandler : IRequestHandler<GetDailyDiaryByDateQuery, DailyDiaryDto>
{
    private readonly IDailyDiaryQueries _dailyDiaryQueries;
    private readonly ICurrentUser _currentUser;

    public GetDailyDiaryByDateQueryHandler(IDailyDiaryQueries dailyDiaryQueries, ICurrentUser currentUser)
    {
        _dailyDiaryQueries = dailyDiaryQueries;
        _currentUser = currentUser;
    }

    public async Task<DailyDiaryDto> Handle(GetDailyDiaryByDateQuery request, CancellationToken cancellationToken)
    {
        var dailyDiary = await _dailyDiaryQueries.GetByDateAsync(_currentUser.UserId, request.Date, cancellationToken);

        return dailyDiary ?? throw new NotFoundException(nameof(DailyDiary), request.Date.ToString("O"));
    }
}
