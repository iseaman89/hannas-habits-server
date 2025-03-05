using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TaskManagementClient.Services;
using TaskManagementClient.Services.Base;

namespace TaskManagementClient.Pages.Tasks;

public partial class Index
{ 
        [Inject] private ITaskService _taskService { get; set; }
        [Inject] private IJSRuntime _js { get; set; }
        
        private List<TaskReadOnlyDto> _tasks;
        private Response<List<TaskReadOnlyDto>> _response = new Response<List<TaskReadOnlyDto>>(){Success = true};

        protected override async Task OnInitializedAsync()
        {
                _response = await _taskService.GetTasks();

                if (_response.Success)
                {
                        _tasks = _response.Data;
                }
        }

        private async Task Delete(int id)
        {
                var task = _tasks.Find(lp => lp.Id == id);
                var confirm = await _js.InvokeAsync<bool>("confirm", $"Are you sure that you want delete camera {task.Title}?");
                if (confirm)
                {
                        var response = await _taskService.Delete(id);
                        if (response.Success)
                        {
                                await OnInitializedAsync();
                        }
                }
        }
}