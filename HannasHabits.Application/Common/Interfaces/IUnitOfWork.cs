namespace HannasHabits.Application.Common.Interfaces;

/// <summary>Commits the changes made through the repositories as one atomic unit.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
