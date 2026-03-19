using MediatR;
using System;

namespace HannasHabits.Application.HabitRecords.Commands.MarkCompleted;

public record MarkCompletedCommand(Guid HabitId, DateOnly Date) : IRequest<HabitRecordDto>;