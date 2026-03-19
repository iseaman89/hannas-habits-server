using MediatR;

namespace HannasHabits.Application.Habits.Queries.GetAllHabits;

public record GetAllHabitsQuery() : IRequest<List<HabitListItemDto>>;