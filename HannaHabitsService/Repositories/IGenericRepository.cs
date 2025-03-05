namespace HannaHabitsService.Repositories;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetAsync(int? id, CancellationToken cancellationToken);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken);
    Task UpdateAsync(T entity, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
    Task<bool> Exists(int id, CancellationToken cancellationToken);
}