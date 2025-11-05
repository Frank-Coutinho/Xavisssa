namespace Xavissa.Frontend.Models
{
    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public UserRole UserRole { get; set; } // 0=Superuser, 1=Admin, 2=Clerk
    }

    public enum UserRole
    {
        Superuser = 0,
        Admin = 1,
        Clerk = 2,
    }
}
