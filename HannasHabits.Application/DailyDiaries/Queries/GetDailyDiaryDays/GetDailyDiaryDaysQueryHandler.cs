using HannasHabits.Application.Common.Interfaces;
using MediatR;

namespace HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;

public class GetDailyDiaryDaysQueryHandler : IRequestHandler<GetDailyDiaryDaysQuery, List<DailyDiaryDayDto>>
{
    private readonly IDailyDiaryQueries _dailyDiaryQueries;
    private readonly ICurrentUser _currentUser;

    public GetDailyDiaryDaysQueryHandler(IDailyDiaryQueries dailyDiaryQueries, ICurrentUser currentUser)
    {
        _dailyDiaryQueries = dailyDiaryQueries;
        _currentUser = currentUser;
    }

    public Task<List<DailyDiaryDayDto>> Handle(GetDailyDiaryDaysQuery request, CancellationToken cancellationToken)
        => _dailyDiaryQueries.GetDaysAsync(_currentUser.UserId, request.From, request.To, cancellationToken);
}
