using TaskManagementClient.Services.Base;

namespace TaskManagementClient.Services;

public interface ITaskService
{
    Task<Response<List<TaskReadOnlyDto>>> GetTasks();
    Task<Response<TaskReadOnlyDto>> GetTask(int id);
    Task<Response<TaskUpdateDto>> GetTaskForUpdate(int id);
    Task<Response<int>> CreateTask(TaskCreateDto task);
    Task<Response<int>> EditTask(int id, TaskUpdateDto task);
    Task<Response<int>> Delete(int id);

}