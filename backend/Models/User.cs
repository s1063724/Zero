// Models/User.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Username { get; set; }  // 用戶名

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public required string Email { get; set; }     // 電子郵件

        [Required]
        [StringLength(100)]
        public required string Password { get; set; }  // 密碼

        public DateTime CreatedAt { get; set; }        // 創建時間

        [StringLength(100)]
        public string? ResetPasswordToken { get; set; }        // 重置密碼令牌
        public DateTime? ResetPasswordTokenExpires { get; set; }  // 重置密碼令牌過期時間
    }
}