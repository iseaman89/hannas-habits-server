using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using MediatR;

namespace HannasHabits.Application.DailyDiaries.Commands.SaveDailyDiary;

public class SaveDailyDiaryCommandHandler : IRequestHandler<SaveDailyDiaryCommand, Unit>
{
    private readonly IDailyDiaryRepository _dailyDiaries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public SaveDailyDiaryCommandHandler(IDailyDiaryRepository dailyDiaries, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _dailyDiaries = dailyDiaries;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SaveDailyDiaryCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        var content = DiaryContent.Create(
            request.Mood,
            request.Body is { } body ? Percentage.Create(body) : null,
            request.Mind is { } mind ? Percentage.Create(mind) : null,
            request.Highlight,
            request.Grateful,
            request.Learned,
            request.Tasks?.Select(task => DiaryTask.Create(task.Title, task.Done)));

        var existing = await _dailyDiaries.GetByDateAsync(userId, request.Date, cancellationToken);

        // The Domain does not store an empty entry; what the API does about it is this use case's decision: saving
        // "nothing" removes the entry (the calendar would otherwise show a day as written that holds nothing, e.g. after
        // everything was erased again), and doing so when there is none is fine - the day is empty either way.
        if (content.IsEmpty)
        {
            if (existing is not null)
            {
                _dailyDiaries.Remove(existing);

                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (ConflictException)
                {
                    // A concurrent request removed the entry between our lookup and our save: the day is empty, which
                    // is exactly what was asked for.
                }
            }

            return Unit.Value;
        }

        if (existing is not null)
        {
            existing.Replace(content);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }

        var created = DailyDiary.Create(userId, request.Date, content);
        _dailyDiaries.Add(created);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateEntryException)
        {
            // A concurrent request created the entry of this day between our lookup and our save (e.g. a retried
            // autosave) and the unique index rejected ours. Last write wins: forget our failed insert, then put our
            // content onto the entry that won.
            _dailyDiaries.Remove(created);

            var winner = await _dailyDiaries.GetByDateAsync(userId, request.Date, cancellationToken);
            if (winner is null)
                throw; // the winner was deleted again in the meantime: let the client retry (409)

            winner.Replace(content);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
