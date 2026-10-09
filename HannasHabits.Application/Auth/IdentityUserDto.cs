namespace HannasHabits.Application.Auth;

/// <param name="FirstName">Never empty: the first name the user gave, else the part of the email before the "@".</param>
/// <param name="LastName">The last name; <c>null</c> = none given.</param>
public record IdentityUserDto(Guid Id, string UserName, string Email, string FirstName, string? LastName);
