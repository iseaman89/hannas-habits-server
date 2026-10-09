namespace HannasHabits.Application.Common.Exceptions;

/// <summary>
/// The requested resource does not exist - or belongs to another user, which must look the same
/// from the outside (data isolation).
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.")
    {
    }
}
