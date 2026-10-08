using HannasHabits.Application.Common.Interfaces;
using MediatR;

namespace HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;

public class GetAllDailyDiariesQueryHandler : IRequestHandler<GetAllDailyDiariesQuery, List<DailyDiaryListItemDto>>
{
    private readonly IDailyDiaryQueries _dailyDiaryQueries;
    private readonly ICurrentUser _currentUser;

    public GetAllDailyDiariesQueryHandler(IDailyDiaryQueries dailyDiaryQueries, ICurrentUser currentUser)
    {
        _dailyDiaryQueries = dailyDiaryQueries;
        _currentUser = currentUser;
    }

    public Task<List<DailyDiaryListItemDto>> Handle(GetAllDailyDiariesQuery request, CancellationToken cancellationToken)
        => _dailyDiaryQueries.GetAllAsync(_currentUser.UserId, cancellationToken);
}
