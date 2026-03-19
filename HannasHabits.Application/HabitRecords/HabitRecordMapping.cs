using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Domain.Entities;
using Mapster;

namespace HannasHabits.Application.HabitRecords;

public class HabitRecordMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<HabitRecord, HabitRecordDto>();
    }
}