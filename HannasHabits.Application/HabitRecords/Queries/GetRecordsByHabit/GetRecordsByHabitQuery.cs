using MediatR;
using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;

namespace HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;

/// <summary>Records of a habit; <paramref name="From"/> and <paramref name="To"/> are inclusive and optional.</summary>
public record GetRecordsByHabitQuery(Guid HabitId, DateOnly? From = null, DateOnly? To = null)
    : IRequest<List<HabitRecordDto>>;
