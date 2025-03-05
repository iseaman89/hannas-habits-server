using Microsoft.AspNetCore.Components;
using TaskManagementClient.Services.Base;

namespace TaskManagementClient.Pages.Users;

public partial class Register
{
    [Inject] private Client _httpClient { get; set; } 
    [Inject] private NavigationManager _navManager { get; set; }
    
    private UserDto _registrationModel = new UserDto{Role = "User"};
    private string _message = String.Empty;

    private async Task HandleRegistration()
    {
        try
        {
            await _httpClient.RegisterAsync(_registrationModel);
            NavigateToLogin();
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode >= 200 && ex.StatusCode <= 299)
            {
                NavigateToLogin();
            }
            _message = ex.Response;
        }
    }

    private void NavigateToLogin()
    {
        _navManager.NavigateTo("/users/login");
    }
}