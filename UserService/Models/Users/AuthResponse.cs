namespace UserService.Models.Users;

public class AuthResponse
{
    public string? UserId { get; set; }
    public string? Token { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}