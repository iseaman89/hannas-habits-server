using MediatR;

namespace HannasHabits.Application.Tags.Commands.RenameTag;

/// <summary>
/// Gives a tag another name. If another tag of the user already has that name (ignoring case) the two are merged:
/// every day of this tag goes to the other one and this tag is gone.
/// </summary>
public record RenameTagCommand(Guid TagId, string? Name) : IRequest<RenamedTagDto>;

/// <param name="Id">The tag that carries the name now. After a merge that is NOT the tag that was asked for.</param>
/// <param name="Name">The tag's name as stored.</param>
/// <param name="Merged"><c>true</c> if the name belonged to another tag and the tag that was asked for no longer exists.</param>
public record RenamedTagDto(Guid Id, string Name, bool Merged);
