using MediatR;

namespace HannasHabits.Application.Habits.Queries.GetHabitById;

public record GetHabitByIdQuery(Guid Id) : IRequest<HabitDetailsDto>;