using System.ComponentModel.DataAnnotations;

public class RegisterDto
{
    [Required(ErrorMessage = "用戶名是必需的")]
    public required string Username { get; set; }

    [Required(ErrorMessage = "郵箱是必需的")]
    [EmailAddress(ErrorMessage = "郵箱格式不正確")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "密碼是必需的")]
    [MinLength(6, ErrorMessage = "密碼至少需要6個字符")]
    public required string Password { get; set; }
} 