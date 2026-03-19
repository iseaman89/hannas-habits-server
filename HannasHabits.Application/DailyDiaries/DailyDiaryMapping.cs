using HannasHabits.Application.DailyDiaries.Commands.CreateDailyDiary;
using HannasHabits.Application.DailyDiaries.Queries.GetAllDailyDiaries;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryById;
using HannasHabits.Domain.Entities;
using Mapster;

namespace HannasHabits.Application.DailyDiaries;

public class DailyDiaryMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<DailyDiary, CreateDailyDiaryDto>();
        config.NewConfig<DailyDiary, DailyDiaryDetailsDto>();
        config.NewConfig<DailyDiary, DailyDiaryListItemDto>();
    }
}