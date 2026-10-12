using MediatR;

namespace HannasHabits.Application.Tags.Queries.GetTags;

/// <summary>The user's tags with their use; with a habit, that habit's own use decides the order.</summary>
public record GetTagsQuery(Guid? HabitId) : IRequest<List<TagDto>>;
