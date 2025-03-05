using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using TaskManagementClient.Providers;
using TaskManagementClient.Services.Base;
using Client = TaskManagementClient.Services.Base.Client;

namespace TaskManagementClient.Services.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly Client _httpClient;
    private readonly ILocalStorageService _localStorageService;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    
    public AuthenticationService(Client httpClient, ILocalStorageService localStorageService, AuthenticationStateProvider authenticationStateProvider)
    {
        _httpClient = httpClient;
        _localStorageService = localStorageService;
        _authenticationStateProvider = authenticationStateProvider;
    }
    
    public async Task<bool> AuthenticateAsync(LoginUserDto loginModel)
    {
        var response = await _httpClient.LoginAsync(loginModel);

        await _localStorageService.SetItemAsync("accessToken", response.Token);

        await ((ApiAuthenticationStateProvider)_authenticationStateProvider).LoggedIn();

        return true;
    }

    public async Task Logout()
    {
        await ((ApiAuthenticationStateProvider)_authenticationStateProvider).LoggedOut();
    }
}