namespace HannasHabits.Application.Auth;

/// <summary>Input limits shared by the validators (a long password would only cost hashing time).</summary>
public static class AuthLimits
{
    public const int EmailMaxLength = 256;
    public const int PasswordMaxLength = 128;
    public const int DisplayNameMaxLength = 100;

    // Refresh tokens are 88 characters, Google ID tokens roughly 1-2 KB.
    public const int RefreshTokenMaxLength = 256;
    public const int GoogleIdTokenMaxLength = 4096;
}
