using HannasHabits.Application.Habits.Commands.UpdateHabit;

namespace HannasHabits.WebApi.Models;

public record UpdateHabitRequest(string Title, string? Description)
{
    public UpdateHabitCommand ToCommand(Guid id) => new() { Id = id, Title = Title, Description = Description };
}
