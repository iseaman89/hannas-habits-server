using AutoMapper;
using TaskManagementClient.Services.Base;

namespace TaskManagementClient.Configuration;

public class MapperConfig : Profile
{
    public MapperConfig()
    {
        CreateMap<TaskReadOnlyDto, TaskUpdateDto>().ReverseMap();
    }
}