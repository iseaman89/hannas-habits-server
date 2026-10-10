using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.Tags.Queries.GetTags;

public class GetTagsQueryHandler : IRequestHandler<GetTagsQuery, List<TagDto>>
{
    private readonly ITagQueries _tagQueries;
    private readonly ICurrentUser _currentUser;

    public GetTagsQueryHandler(ITagQueries tagQueries, ICurrentUser currentUser)
    {
        _tagQueries = tagQueries;
        _currentUser = currentUser;
    }

    public async Task<List<TagDto>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
    {
        var tags = await _tagQueries.GetAllAsync(_currentUser.UserId, request.HabitId, cancellationToken);

        // The read side answers null for a habit that is not the user's; for the API that is a missing resource.
        return tags ?? throw new NotFoundException(nameof(Habit), request.HabitId!.Value);
    }
}
