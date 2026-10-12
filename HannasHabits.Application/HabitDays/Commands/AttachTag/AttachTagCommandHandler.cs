using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Application.Tags;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.HabitDays.Commands.AttachTag;

public class AttachTagCommandHandler : IRequestHandler<AttachTagCommand, TaggedDayDto>
{
    private readonly IHabitRepository _habits;
    private readonly ITagRepository _tags;
    private readonly IHabitDayEntryRepository _days;
    private readonly ITagQueries _tagQueries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public AttachTagCommandHandler(IHabitRepository habits, ITagRepository tags, IHabitDayEntryRepository days,
        ITagQueries tagQueries, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _tags = tags;
        _days = days;
        _tagQueries = tagQueries;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<TaggedDayDto> Handle(AttachTagCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        // The tag and the habit are two aggregates of the same user; that both are the user's is checked here, where
        // both repositories are in reach.
        if (await _habits.GetByIdAsync(userId, request.HabitId, cancellationToken) is null)
            throw new NotFoundException(nameof(Habit), request.HabitId);

        if (await _tags.GetByIdAsync(userId, request.TagId, cancellationToken) is null)
            throw new NotFoundException(nameof(Tag), request.TagId);

        var entry = await _days.GetAsync(userId, request.HabitId, request.Date, cancellationToken);

        // The Domain forbids a tag twice on a day; making the API call idempotent is this use case's decision:
        // attaching a tag that is already there is not an error, the client just gets the day as it is.
        if (entry is null || !entry.HasTag(request.TagId))
            await AttachAsync(entry, userId, request, cancellationToken);

        return await _tagQueries.GetTaggedDayAsync(userId, request.HabitId, request.Date, cancellationToken);
    }

    private async Task AttachAsync(
        HabitDayEntry? entry, Guid userId, AttachTagCommand request, CancellationToken cancellationToken)
    {
        var isNew = entry is null;

        if (entry is null)
        {
            entry = HabitDayEntry.Create(request.HabitId, request.Date);
            _days.Add(entry);
        }

        entry.AddTag(request.TagId);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateEntryException)
        {
            // A concurrent request wrote to the same day between our lookup and our save: it created the day's entry
            // (we were the first to tag the day, so was the other) or put the very same tag on it. Forget what we tried
            // to add, look at the day as it is now and add our tag to that.
            if (isNew)
                _days.Remove(entry);
            else
                entry.RemoveTag(request.TagId);

            var winner = await _days.GetAsync(userId, request.HabitId, request.Date, cancellationToken);
            if (winner is null)
                throw; // the winner was removed again in the meantime: let the client retry (409)

            if (!winner.HasTag(request.TagId))
            {
                winner.AddTag(request.TagId);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
