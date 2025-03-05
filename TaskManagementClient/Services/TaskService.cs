using AutoMapper;
using Blazored.LocalStorage;
using TaskManagementClient.Services.Base;
using Client = TaskManagementClient.Services.Base.Client;

namespace TaskManagementClient.Services;

public class TaskService : BaseHttpService, ITaskService
{
    private readonly Client _httpClient;
    private readonly IMapper _mapper;
    
    public TaskService(Client httpClient, ILocalStorageService localStorageService, IMapper mapper) : base(httpClient, localStorageService)
    {
        _httpClient = httpClient;
        _mapper = mapper;
    }

    public async Task<Response<List<TaskReadOnlyDto>>> GetTasks()
    {
        Response<List<TaskReadOnlyDto>> response;

        try
        {
            await GetBearerToken();
            var data = await _httpClient.TaskAllAsync();
            response = new Response<List<TaskReadOnlyDto>>()
            {
                Data = data.ToList(),
                Success = true
            };
        }
        catch (ApiException ex)
        {
            response = ConvertApiExceptions<List<TaskReadOnlyDto>>(ex);
        }

        return response;
    }

    public async Task<Response<TaskReadOnlyDto>> GetTask(int id)
    {
        Response<TaskReadOnlyDto> response;

        try
        {
            await GetBearerToken();
            var data = await _httpClient.TaskGETAsync(id);
            response = new Response<TaskReadOnlyDto>()
            {
                Data = data,
                Success = true
            };
        }
        catch (ApiException ex)
        {
            response = ConvertApiExceptions<TaskReadOnlyDto>(ex);
        }

        return response;
    }

    public async Task<Response<TaskUpdateDto>> GetTaskForUpdate(int id)
    {
        Response<TaskUpdateDto> response;

        try
        {
            await GetBearerToken();
            var data = await _httpClient.TaskGETAsync(id);
            response = new Response<TaskUpdateDto>()
            {
                Data = _mapper.Map<TaskUpdateDto>(data),
                Success = true
            };
        }
        catch (ApiException ex)
        {
            response = ConvertApiExceptions<TaskUpdateDto>(ex);
        }

        return response;
    }

    public async Task<Response<int>> CreateTask(TaskCreateDto task)
    {
        Response<int> response = new Response<int>() {Success = true};

        try
        {
            await GetBearerToken(); 
            await _httpClient.TaskPOSTAsync(task);
        }
        catch (ApiException ex)
        {
            response = ConvertApiExceptions<int>(ex);
        }

        return response;
    }

    public async Task<Response<int>> EditTask(int id, TaskUpdateDto task)
    {
        Response<int> response = new Response<int>() {Success = true};

        try
        {
            await GetBearerToken(); 
            await _httpClient.TaskPUTAsync(id, task);
        }
        catch (ApiException ex)
        {
            response = ConvertApiExceptions<int>(ex);
        }

        return response;
    }

    public async Task<Response<int>> Delete(int id)
    {
        Response<int> response = new Response<int>() {Success = true};

        try
        {
            await GetBearerToken(); 
            await _httpClient.TaskDELETEAsync(id);
        }
        catch (ApiException ex)
        {
            response = ConvertApiExceptions<int>(ex);
        }

        return response;
    }
}