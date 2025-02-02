using System.ComponentModel.DataAnnotations;

public class ForgotPasswordDto
{
    [Required(ErrorMessage = "郵箱是必需的")]
    [EmailAddress(ErrorMessage = "郵箱格式不正確")]
    public required string Email { get; set; }
} 