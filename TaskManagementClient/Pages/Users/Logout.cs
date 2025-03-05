using Microsoft.AspNetCore.Components;
using TaskManagementClient.Services.Authentication;

namespace TaskManagementClient.Pages.Users;

public partial class Logout
{
    [Inject] private IAuthenticationService _authService { get; set; } 
    [Inject] private NavigationManager _navManager { get; set; }
    
    protected override async Task OnInitializedAsync()
    {
        await _authService.Logout();
        _navManager.NavigateTo("/");
    }
}