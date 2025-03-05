using System.Net.Http.Headers;
using Blazored.LocalStorage;
using TaskManagementClient.Services.Base;

namespace TaskManagementClient.Services.Base;

public class BaseHttpService
{
    private readonly Client _httpClient;
    private readonly ILocalStorageService _localStorageService;

    public BaseHttpService(Client httpClient, ILocalStorageService localStorageService)
    {
        _httpClient = httpClient;
        _localStorageService = localStorageService;
    }

    protected Response<Guid> ConvertApiExceptions<Guid>(ApiException apiException)
    {
        if (apiException.StatusCode == 400)
        {
            return new Response<Guid>()
            {
                Message = "Validation errors have occured.", ValidationErrors = apiException.Response, Success = false
            };
        }
        if (apiException.StatusCode == 404)
        {
            return new Response<Guid>()
            {
                Message = "The requested item could not be found.", Success = false
            };
        }

        if (apiException.StatusCode >= 200 && apiException.StatusCode <= 299)
        {
            return new Response<Guid>()
            {
                Message = "Operation reported success.", Success = true
            };
        }
        return new Response<Guid>()
        {
            Message = "Something went wrong, please try again.", Success = false
        };
    }

    protected async Task GetBearerToken()
    {
        var token = await _localStorageService.GetItemAsync<string>("accessToken");
        if (token != null)
        {
            _httpClient.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);
        }
    }
}