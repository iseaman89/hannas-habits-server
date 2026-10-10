using HannasHabits.Application.Tags;
using MediatR;

namespace HannasHabits.Application.HabitDays.Commands.AttachTag;

/// <summary>Puts one of the user's tags on a day of one of the user's habits. Idempotent. Answers with the day's tags.</summary>
public record AttachTagCommand(Guid HabitId, DateOnly Date, Guid TagId) : IRequest<TaggedDayDto>;
