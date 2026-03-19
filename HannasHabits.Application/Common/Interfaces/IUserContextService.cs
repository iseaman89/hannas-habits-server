namespace HannasHabits.Application.Common.Interfaces;

public interface IUserContextService
{
    Guid? UserId { get; }
    string? Username { get; }
}