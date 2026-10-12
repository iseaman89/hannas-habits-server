using HannasHabits.Application.Habits.Commands.CreateHabit;
using HannasHabits.Application.Habits.Recurrence;
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
            .Map(dto => dto.Recurrence, habit => RecurrenceDto.From(habit.Schedule));
    }
}
