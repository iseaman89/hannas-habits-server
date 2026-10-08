using HannasHabits.Application.Habits.Commands.CreateHabit;

namespace HannasHabits.WebApi.Models;

public record CreateHabitRequest(string Title, string? Description)
{
    public CreateHabitCommand ToCommand() => new() { Title = Title, Description = Description };
}
