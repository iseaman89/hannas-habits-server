namespace HannasHabits.Application.Auth;

/// <param name="DisplayName">Never empty: the name the user chose, else the part of the email before the "@".</param>
public record IdentityUserDto(Guid Id, string UserName, string Email, string DisplayName);
