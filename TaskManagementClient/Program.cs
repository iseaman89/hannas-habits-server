using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using TaskManagementClient;
using TaskManagementClient.Configuration;
using TaskManagementClient.Providers;
using TaskManagementClient.Services;
using TaskManagementClient.Services.Authentication;
using TaskManagementClient.Services.Base;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

builder.Services.AddHttpClient<Client>(client =>
    client.BaseAddress = new Uri("https://localhost:7047"));

builder.Services.AddHttpClient<ITaskService>(client =>
    client.BaseAddress = new Uri("https://localhost:7245"));

builder.Services.AddBlazoredLocalStorage();

builder.Services.AddAutoMapper(typeof(MapperConfig));

//builder.Services.AddScoped<Client>();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(p => p.GetRequiredService<ApiAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
