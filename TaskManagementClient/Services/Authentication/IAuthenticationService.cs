using TaskManagementClient.Services.Base;

namespace TaskManagementClient.Services.Authentication;

public interface IAuthenticationService
{
    Task<bool> AuthenticateAsync(LoginUserDto loginModel);
    public Task Logout();
}