namespace TaskManagementClient.Services.Base;

public partial class Client
{
    public HttpClient HttpClient
    {
        get
        {
            return _httpClient;
        }
    }
}