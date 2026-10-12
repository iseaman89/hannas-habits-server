using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using MediatR;

namespace HannasHabits.Application.Tags.Commands.CreateTag;

public class CreateTagCommandHandler : IRequestHandler<CreateTagCommand, CreateTagResult>
{
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public CreateTagCommandHandler(ITagRepository tags, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _tags = tags;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<CreateTagResult> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var name = TagName.Create(request.Name);

        // The Domain only knows that a name is valid; "one tag per name" is made idempotent here: asking for a tag that
        // exists is not an error, the client (typing a name it has not seen in its list yet) just gets that tag.
        var existing = await _tags.FindByNameAsync(userId, name, cancellationToken);
        if (existing is not null)
            return Existing(existing);

        var count = await _tags.CountAsync(userId, cancellationToken);

        // Two parallel requests can both see "199" and end up with 201. Accepted: the limit is a guard against runaway
        // data, not a business promise that would justify locking.
        var tag = Tag.Create(userId, name, count);
        _tags.Add(tag);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateEntryException)
        {
            // A concurrent request created a tag of this name between our lookup and our save and the unique index
            // rejected ours. The goal "the tag exists" is reached either way: forget our failed insert and answer with
            // the winner's tag.
            _tags.Remove(tag);

            var winner = await _tags.FindByNameAsync(userId, name, cancellationToken);
            if (winner is null)
                throw; // the winner was deleted again in the meantime: let the client retry (409)

            return Existing(winner);
        }

        return new CreateTagResult(new TagSummaryDto(tag.Id, tag.Name.Value), Created: true);
    }

    private static CreateTagResult Existing(Tag tag)
        => new(new TagSummaryDto(tag.Id, tag.Name.Value), Created: false);
}
