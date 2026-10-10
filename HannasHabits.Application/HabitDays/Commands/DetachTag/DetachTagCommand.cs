using HannasHabits.Application.Tags;
using MediatR;

namespace HannasHabits.Application.HabitDays.Commands.DetachTag;

/// <summary>Takes a tag off a day of one of the user's habits. Idempotent. Answers with the day's tags.</summary>
public record DetachTagCommand(Guid HabitId, DateOnly Date, Guid TagId) : IRequest<TaggedDayDto>;
