using HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;
using HannasHabits.Domain.Entities;
using Mapster;

namespace HannasHabits.Application.DailyDiaries;

public class DailyDiaryMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<DailyDiary, CreateDailyDiaryDto>();
    }
}
