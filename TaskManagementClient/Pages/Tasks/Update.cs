using Microsoft.AspNetCore.Components;
using TaskManagementClient.Services;
using TaskManagementClient.Services.Base;

namespace TaskManagementClient.Pages.Tasks;

public partial class Update
{ 
        [Inject] private ITaskService _taskService { get; set; }
        [Inject] private NavigationManager _navManager { get; set; }
        
        [Parameter] public int Id { get; set; }
    
        private TaskUpdateDto _task = new();

        protected override async Task OnInitializedAsync()
        {
                var response = await _taskService.GetTaskForUpdate(Id);
                if (response.Success)
                {
                        _task = response.Data;
                }
        }

        private async Task HandleUpdateTask()
        {
                var response = await _taskService.EditTask(Id, _task);
                if (response.Success)
                {
                        BackToList();
                }
        }

        private void BackToList()
        {
                _navManager.NavigateTo("/tasks/");
        }
}