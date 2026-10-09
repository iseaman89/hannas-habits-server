namespace HannasHabits.Application.Common.Exceptions;

/// <summary>Too many failed sign-in attempts: the account is locked for a while (429).</summary>
public class AccountLockedOutException : Exception
{
    public AccountLockedOutException()
        : base("Too many failed sign-in attempts. Please try again later.")
    {
    }
}
