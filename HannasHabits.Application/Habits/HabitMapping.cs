using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Domain.Entities;
using Mapster;

namespace HannasHabits.Application.Habits;

public class HabitMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Title and Schedule are value objects; the DTO carries their plain values.
        config.NewConfig<Habit, CreateHabitDto>()
            .Map(dto => dto.Title, habit => habit.Title.Value)
            .Map(dto => dto.Schedule, habit => habit.Schedule.Days);
    }
}
