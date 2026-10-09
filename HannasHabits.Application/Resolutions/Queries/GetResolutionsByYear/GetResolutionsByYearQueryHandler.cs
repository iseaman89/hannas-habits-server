using HannasHabits.Application.Common.Interfaces;
using MediatR;

namespace HannasHabits.Application.Resolutions.Queries.GetResolutionsByYear;

public class GetResolutionsByYearQueryHandler : IRequestHandler<GetResolutionsByYearQuery, List<ResolutionDto>>
{
    private readonly IResolutionQueries _resolutionQueries;
    private readonly ICurrentUser _currentUser;

    public GetResolutionsByYearQueryHandler(IResolutionQueries resolutionQueries, ICurrentUser currentUser)
    {
        _resolutionQueries = resolutionQueries;
        _currentUser = currentUser;
    }

    public Task<List<ResolutionDto>> Handle(GetResolutionsByYearQuery request, CancellationToken cancellationToken)
        => _resolutionQueries.GetByYearAsync(_currentUser.UserId, request.Year, cancellationToken);
}
