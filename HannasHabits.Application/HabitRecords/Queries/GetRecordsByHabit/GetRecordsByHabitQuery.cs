using MediatR;
using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;

namespace HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;

public record GetRecordsByHabitQuery(Guid HabitId) : IRequest<List<HabitRecordDto>>;