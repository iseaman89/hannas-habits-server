using HannaHabitsService.Data;
using Microsoft.EntityFrameworkCore;

namespace HannaHabitsService.Repositories;

public class GenericRepository<T>(HannaDbContext context) : IGenericRepository<T> where T : class
{
    public async Task<T?> GetAsync(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return null;
        return await context.Set<T>().FindAsync(id, cancellationToken);
    }

    public async Task<List<T>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await context.Set<T>().ToListAsync(cancellationToken);
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken)
    {
        await context.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        context.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await GetAsync(id, cancellationToken);
        if (entity != null) context.Set<T>().Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Exists(int id, CancellationToken cancellationToken)
    {
        var entity = await GetAsync(id, cancellationToken);
        return entity != null;
    }
}