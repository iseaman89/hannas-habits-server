using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Domain.Entities;
using Mapster;

namespace HannasHabits.Application.Habits;

public class HabitMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Habit, CreateHabitDto>();
    }
}
