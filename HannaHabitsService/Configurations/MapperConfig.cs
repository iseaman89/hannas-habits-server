using AutoMapper;
using HannaHabitsService.Dtos.Completions;
using HannaHabitsService.Dtos.DailyDiaries;
using HannaHabitsService.Dtos.Habits;
using HannaHabitsService.Dtos.YearResolutions;
using HannaHabitsService.Models;

namespace HannaHabitsService.Configurations;

public class MapperConfig : Profile
{
    public MapperConfig()
    {
        CreateMap<HabitCreateDto, Habit>().ReverseMap();
        CreateMap<HabitReadOnlyDto, Habit>().ReverseMap();
        CreateMap<HabitUpdateDto, Habit>().ReverseMap();

        CreateMap<DailyDiaryCreateDto, DailyDiary>().ReverseMap();
        CreateMap<DailyDiaryReadOnlyDto, DailyDiary>().ReverseMap();
        CreateMap<DailyDiaryUpdateDto, DailyDiary>().ReverseMap();

        CreateMap<YearResolutionCreateDto, YearResolution>().ReverseMap();
        CreateMap<YearResolutionReadOnlyDto, YearResolution>().ReverseMap();
        CreateMap<YearResolutionUpdateDto, YearResolution>().ReverseMap();

        CreateMap<CompletionCreateDto, Completion>().ReverseMap();
    }
}