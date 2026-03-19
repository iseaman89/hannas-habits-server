using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Application.Habits.Queries.GetAllHabits;
using HannasHabits.Application.Habits.Queries.GetHabitById;
using HannasHabits.Domain.Entities;
using Mapster;

namespace HannasHabits.Application.Habits;

public class HabitMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Habit, CreateHabitDto>();
        config.NewConfig<Habit, HabitDetailsDto>();
        config.NewConfig<Habit, HabitListItemDto>();
    }
}