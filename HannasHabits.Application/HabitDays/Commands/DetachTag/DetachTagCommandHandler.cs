using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Application.Tags;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.HabitDays.Commands.DetachTag;

public class DetachTagCommandHandler : IRequestHandler<DetachTagCommand, TaggedDayDto>
{
    private readonly IHabitRepository _habits;
    private readonly ITagRepository _tags;
    private readonly IHabitDayEntryRepository _days;
    private readonly ITagQueries _tagQueries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DetachTagCommandHandler(IHabitRepository habits, ITagRepository tags, IHabitDayEntryRepository days,
        ITagQueries tagQueries, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _tags = tags;
        _days = days;
        _tagQueries = tagQueries;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<TaggedDayDto> Handle(DetachTagCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (await _habits.GetByIdAsync(userId, request.HabitId, cancellationToken) is null)
            throw new NotFoundException(nameof(Habit), request.HabitId);

        if (await _tags.GetByIdAsync(userId, request.TagId, cancellationToken) is null)
            throw new NotFoundException(nameof(Tag), request.TagId);

        var entry = await _days.GetAsync(userId, request.HabitId, request.Date, cancellationToken);

        // The Domain only allows taking off a tag the day has; for the API "the tag is not on the day" is the state that
        // was asked for, so a tag that is not there is no error (unlike un-marking a day: there the client learns that
        // it marked nothing - here the day's answer shows it anyway).
        if (entry is not null && entry.HasTag(request.TagId))
        {
            entry.RemoveTag(request.TagId);

            // An entry is never stored empty: the day's last tag takes the entry with it.
            if (entry.IsEmpty)
                _days.Remove(entry);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConflictException)
            {
                // A concurrent request took the same tag off between our lookup and our save: the tag is off the day,
                // which is exactly what was asked for.
            }
        }

        return await _tagQueries.GetTaggedDayAsync(userId, request.HabitId, request.Date, cancellationToken);
    }
}
