using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.HabitDays;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using MediatR;

namespace HannasHabits.Application.Tags.Commands.RenameTag;

public class RenameTagCommandHandler : IRequestHandler<RenameTagCommand, RenamedTagDto>
{
    private readonly ITagRepository _tags;
    private readonly IHabitDayEntryRepository _days;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public RenameTagCommandHandler(
        ITagRepository tags, IHabitDayEntryRepository days, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _tags = tags;
        _days = days;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<RenamedTagDto> Handle(RenameTagCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        var tag = await _tags.GetByIdAsync(userId, request.TagId, cancellationToken);
        if (tag is null)
            throw new NotFoundException(nameof(Tag), request.TagId);

        var name = TagName.Create(request.Name);

        // "Another tag already has this name" spans two aggregates, so this use case decides what happens: the names are
        // not rejected as taken, the tags are merged - what the person means by renaming a tag to a name it already has.
        var other = await _tags.FindByNameAsync(userId, name, cancellationToken);

        if (other is null || other.Id == tag.Id)
        {
            // Also covers a change of spelling only ("krank" -> "Krank"): the same tag finds itself by that name.
            tag.Rename(name);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new RenamedTagDto(tag.Id, tag.Name.Value, Merged: false);
        }

        // Every day of this tag goes to the other one. Done through the day entries (the aggregate that owns the rule
        // "a tag only once per day") in ONE save, so a merge is all or nothing. Cost: the entries of this tag are
        // loaded - one per day it was used on, which is fine for a rare operation on a personal tracker.
        var entries = await _days.GetWithTagAsync(userId, tag.Id, cancellationToken);

        foreach (var entry in entries)
            entry.ReplaceTag(tag.Id, other.Id);

        _tags.Remove(tag);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RenamedTagDto(other.Id, other.Name.Value, Merged: true);
    }
}
