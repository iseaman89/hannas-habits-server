using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.Habits.Queries.GetAllHabits;
using HannasHabits.Application.Habits.Queries.GetHabitById;
using HannasHabits.Application.Habits.Queries.GetHabitsOverview;

namespace HannasHabits.Application.Habits;

/// <summary>
/// Read side for habits and their records: projects straight to DTOs, nothing is tracked. Every method takes the
/// owner's id, data of other users is never returned.
/// </summary>
public interface IHabitQueries
{
    Task<List<HabitListItemDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Every habit of the user, in creation order, with ALL its completed days. The overview needs the whole history for
    /// the streak, which can reach back further than the requested range; the caller narrows it down.
    /// </summary>
    Task<List<HabitWithCompletedDates>> GetWithCompletedDatesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary><c>null</c> if the habit does not exist or belongs to another user.</summary>
    Task<HabitDetailsDto?> GetByIdAsync(Guid userId, Guid habitId, CancellationToken cancellationToken);

    /// <summary>
    /// Records of the habit ordered by date, <paramref name="from"/> and <paramref name="to"/> inclusive and optional.
    /// <c>null</c> (not an empty list) if the habit does not exist or belongs to another user.
    /// </summary>
    Task<List<HabitRecordDto>?> GetRecordsAsync(
        Guid userId, Guid habitId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}
