using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Models
{
    public class LoginDto
    {
        [Required(ErrorMessage = "郵箱是必需的")]
        [EmailAddress(ErrorMessage = "郵箱格式不正確")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "密碼是必需的")]
        public required string Password { get; set; }

        public void ClearSensitiveData()
        {
            Password = string.Empty;
        }
    }

    public class LoginResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserDto? User { get; set; }
    }

    public class UserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsAuthenticated { get; set; }
    }

    public class GoogleLoginDto
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? Picture { get; set; }
        public bool Authenticated { get; set; }
    }
}
