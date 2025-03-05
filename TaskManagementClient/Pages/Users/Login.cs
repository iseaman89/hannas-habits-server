

using TaskManagementClient.Services.Base;
using TaskManagmentSystem.Data.Shared.Statics;

namespace TaskManagementClient.Pages.Users;

public partial class Login
{
    private LoginUserDto _loginModel = new LoginUserDto();
    private string _message = String.Empty;

    private async Task HandleLogin()
    {
        try
        {
            var response = await _authService.AuthenticateAsync(_loginModel);
            if (response)
            {
                _navManager.NavigateTo("/");
            }
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode >= 200 && ex.StatusCode <= 299)
            {
                _navManager.NavigateTo("/");
            }
            _message = Messages.LoginError;
        }
    }
}