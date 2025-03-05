using AutoMapper;
using UserService.Data;
using UserService.Models.Users;

namespace UserService.Configurations;

public class MapperConfig : Profile
{
    public MapperConfig()
    {
        CreateMap<ApiUser, UserDto>().ReverseMap();
    }
}