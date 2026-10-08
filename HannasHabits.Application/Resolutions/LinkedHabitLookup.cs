using FluentValidation;
using FluentValidation.Results;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.Resolutions;

/// <summary>
/// "The habit a resolution points to must be one of the user's own" - a rule across two aggregates, so it lives in the
/// use cases (which see both repositories), not in either entity.
/// </summary>
internal static class LinkedHabitLookup
{
    /// <summary>
    /// The user's habit, or <c>null</c> if no link was asked for. An unknown habit and another user's habit are reported
    /// the same way, as an error on <c>habitId</c> (the field the client can show it at).
    /// </summary>
    public static async Task<Habit?> FindAsync(
        IHabitRepository habits, Guid userId, Guid? habitId, CancellationToken cancellationToken)
    {
        if (habitId is null)
            return null;

        var habit = await habits.GetByIdAsync(userId, habitId.Value, cancellationToken);

        return habit ?? throw new ValidationException(
            [new ValidationFailure(nameof(habitId), "The habit to link does not exist.")]);
    }
}
