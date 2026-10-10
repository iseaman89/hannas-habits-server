using MediatR;

namespace HannasHabits.Application.Tags.Commands.CreateTag;

/// <summary>
/// Makes sure the user has a tag of that name (ignoring case): creates it, or answers with the one that exists.
/// </summary>
public record CreateTagCommand(string? Name) : IRequest<CreateTagResult>;

/// <param name="Tag">The tag, new or existing - with the spelling it is stored in.</param>
/// <param name="Created"><c>false</c> if the user already had a tag of that name.</param>
public record CreateTagResult(TagSummaryDto Tag, bool Created);
