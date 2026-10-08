namespace HannasHabits.Application.Common.Exceptions;

/// <summary>
/// The caller could not prove who they are: wrong credentials, an invalid Google token or an unusable refresh token (401).
/// The message is safe to show to the client, so keep it free of details that help an attacker.
/// </summary>
public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message) : base(message)
    {
    }
}
