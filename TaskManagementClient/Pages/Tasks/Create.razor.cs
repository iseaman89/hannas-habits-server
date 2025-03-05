using Microsoft.AspNetCore.Components;
using TaskManagementClient.Services;
using TaskManagementClient.Services.Base;

namespace TaskManagementClient.Pages.Tasks;

public partial class Create
{
    [Inject] public ITaskService TaskService { get; set; }
    [Inject] public NavigationManager NavManager { get; set; }
    
    private TaskCreateDto _task = new TaskCreateDto();

    private async Task HandleCreateTask()
    {
        var response = await TaskService.CreateTask(_task);
        if (response.Success)
        {
            BackToList();
        }
    }

    private void BackToList()
    {
        NavManager.NavigateTo("/tasks/");
    }
}