using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.HabitDays;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.Tags.Commands.DeleteTag;

public class DeleteTagCommandHandler : IRequestHandler<DeleteTagCommand, DeletedTagDto>
{
    private readonly ITagRepository _tags;
    private readonly IHabitDayEntryRepository _days;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DeleteTagCommandHandler(
        ITagRepository tags, IHabitDayEntryRepository days, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _tags = tags;
        _days = days;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<DeletedTagDto> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        var tag = await _tags.GetByIdAsync(userId, request.TagId, cancellationToken);
        if (tag is null)
            throw new NotFoundException(nameof(Tag), request.TagId);

        // The days are taken through their aggregate (not left to the database's cascade) so that a day that held
        // nothing but this tag disappears with it - an entry is never stored empty - and so that the answer can say on
        // how many days the tag was.
        var entries = await _days.GetWithTagAsync(userId, tag.Id, cancellationToken);

        foreach (var entry in entries)
        {
            entry.RemoveTag(tag.Id);

            if (entry.IsEmpty)
                _days.Remove(entry);
        }

        _tags.Remove(tag);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeletedTagDto(entries.Count);
    }
}
