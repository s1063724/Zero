// Controllers/UsersController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using UserManagementAPI.Data;
using UserManagementAPI.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Google.Apis.Auth;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Web;
using System.Text.Json;
using System.Linq;

namespace UserManagementAPI.Controllers
{
    [Route("api/users")]
    [ApiController]
    [EnableCors("AllowVueApp")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UsersController> _logger;
        private readonly IEmailService _emailService;
        private const string FrontendUrl = "http://localhost:8080"; // 更新為正確的前端 URL
        private readonly SmtpClient _smtpClient;

        public UsersController(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<UsersController> logger,
            IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _emailService = emailService;
            
            // 初始化 SMTP 客戶端
            _smtpClient = new SmtpClient
            {
                Host = configuration["Smtp:Host"] ?? "localhost",
                Port = int.Parse(configuration["Smtp:Port"] ?? "25"),
                EnableSsl = bool.Parse(configuration["Smtp:EnableSsl"] ?? "false"),
                Credentials = new NetworkCredential(
                    configuration["Smtp:Username"],
                    configuration["Smtp:Password"]
                )
            };
        }

        // 注册
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try 
            {
                _logger.LogInformation($"收到註冊請求: {JsonSerializer.Serialize(registerDto)}");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    _logger.LogWarning($"驗證失敗: {string.Join(", ", errors)}");
                    return BadRequest(new { errors = errors });
                }

                _logger.LogInformation("=== 開始註冊流程 ===");
                _logger.LogInformation($"接收到的註冊數據: {JsonSerializer.Serialize(registerDto)}");

                if (registerDto == null)
                {
                    _logger.LogWarning("註冊數據為空");
                    return BadRequest("註冊數據不能為空");
                }

                // 檢查用戶是否已存在
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == registerDto.Email);
                
                if (existingUser != null)
                {
                    _logger.LogWarning($"用戶已存在: {registerDto.Email}");
                    return BadRequest("此郵箱已被註冊");
                }

                // 創建新用戶
                var user = new User
                {
                    Username = registerDto.Username,
                    Email = registerDto.Email,
                    Password = registerDto.Password, // 注意：實際應用中應該加密
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"用戶註冊成功: {user.Email}");
                return Ok(new { message = "註冊成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"註冊過程發生錯誤: {ex.Message}");
                _logger.LogError($"錯誤詳情: {ex.StackTrace}");
                return StatusCode(500, new { message = "註冊失敗", error = ex.Message });
            }
        }

        private async Task SendRegistrationEmail(string email)
        {
            try 
            {
                _logger.LogInformation("開始配置郵件發送: {Email}", email);
                
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Smtp:Username"] ?? throw new InvalidOperationException("SMTP Username not configured")),
                    Subject = "註冊成功",
                    Body = "<h1>歡迎註冊</h1><p>您的註冊已成功。</p>",
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(email);

                _logger.LogInformation("SMTP 配置: Host={Host}, Port={Port}, SSL={SSL}", 
                    _smtpClient.Host, _smtpClient.Port, _smtpClient.EnableSsl);

                await _smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("郵件發送成功: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "郵件發送失敗: {Message}", ex.Message);
                throw; // 重新拋出異常，讓調用者處理
            }
        }

        // 登录
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                _logger.LogInformation("=== 開始登入流程 ===");
                _logger.LogInformation($"接收到的登入數據: {JsonSerializer.Serialize(loginDto)}");

                // 檢查請求數據是否為空
                if (loginDto == null)
                {
                    _logger.LogWarning("登入數據為空");
                    return BadRequest("登入數據不能為空");
                }

                // 檢查 ModelState
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    
                    _logger.LogWarning($"登入驗證失敗: {string.Join(", ", errors)}");
                    return BadRequest(new { errors = errors });
                }

                // 檢查必填欄位
                if (string.IsNullOrEmpty(loginDto.Email) || string.IsNullOrEmpty(loginDto.Password))
                {
                    _logger.LogWarning("郵箱或密碼為空");
                    return BadRequest(new { message = "郵箱和密碼都是必填的" });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

                if (user == null)
                {
                    _logger.LogWarning($"找不到用戶: {loginDto.Email}");
                    return BadRequest(new { message = "郵箱或密碼不正確" });
                }

                if (user.Password != loginDto.Password)
                {
                    _logger.LogWarning($"密碼不正確: {loginDto.Email}");
                    return BadRequest(new { message = "郵箱或密碼不正確" });
                }

                _logger.LogInformation($"用戶登入成功: {loginDto.Email}");
                return Ok(new { 
                    message = "登入成功",
                    user = new { 
                        id = user.Id,
                        email = user.Email,
                        username = user.Username
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"登入過程發生錯誤: {ex.Message}");
                return StatusCode(500, new { message = "登入失敗", error = ex.Message });
            }
        }

        // 獲取 Google OAuth URL
        [HttpGet("google-url")]
        public IActionResult GetGoogleAuthUrl()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = $"{Request.Scheme}://{Request.Host}/api/users/google-callback",
                Items = 
                { 
                    { "returnUrl", FrontendUrl }
                }
            };

            var challengeUrl = $"{Request.Scheme}://{Request.Host}/api/users/google-login";
            return Ok(new { url = challengeUrl });
        }

        [HttpGet("google-login")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = "https://api.zero.com/api/users/google-callback",
                Items =
                {
                    { "returnUrl", "https://zero.com" }
                }
            };

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            try
            {
                var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                if (!authenticateResult.Succeeded)
                {
                    return Redirect("https://zero.com/login?error=authentication_failed");
                }

                var claims = authenticateResult.Principal.Claims;
                var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
                var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

                var userInfo = new
                {
                    Email = email,
                    Name = name,
                    Picture = claims.FirstOrDefault(c => c.Type == "picture")?.Value,
                    Authenticated = true
                };

                return Redirect($"https://zero.com/login?userInfo={Uri.EscapeDataString(JsonSerializer.Serialize(userInfo))}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google callback error");
                return Redirect($"https://zero.com/login?error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        // 獲取當前用戶信息
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            if (!authenticateResult.Succeeded)
            {
                return Unauthorized(new { message = "Not authenticated" });
            }

            var claims = authenticateResult.Principal.Claims;
            var userInfo = new
            {
                Email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value,
                Name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value,
                Picture = claims.FirstOrDefault(c => c.Type == "picture")?.Value,
                Authenticated = true
            };

            return Ok(userInfo);
        }

        // 添加一個測試端點
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { message = "API is working" });
        }

        [HttpGet("example")]
        public async Task<IActionResult> ExampleMethod()
        {
            await Task.Delay(1000); // 示例非同步操作
            return Ok();
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            try
            {
                _logger.LogInformation($"收到忘記密碼請求: {model.Email}");
                
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (user == null)
                {
                    // 為了安全性，即使用戶不存在也返回成功
                    return Ok(new { message = "如果此電子郵件地址存在，您將收到重設密碼的說明。" });
                }

                var resetToken = Guid.NewGuid().ToString();
                user.ResetPasswordToken = resetToken;
                user.ResetPasswordTokenExpires = DateTime.UtcNow.AddHours(24);
                
                await _context.SaveChangesAsync();

                // 修正重設密碼連結
                var domain = _configuration["AppSettings:ProductDomain"] ?? "localhost:8080";
                var protocol = "http";  // 強制使用 HTTP 協議用於本地開發
                var clientUrl = $"{protocol}://{domain}";
                var resetLink = $"{clientUrl}/#/reset-password?token={resetToken}";
                
                _logger.LogInformation($"重設密碼連結: {resetLink}");

                var subject = "【ProductWebsite】重設密碼驗證";
                var body = $@"
<div style='padding:20px;'>
    <p style='font-size:16px;color:#333;'>親愛的用戶：</p>
    <p style='font-size:16px;color:#333;'>我們收到了您的重設密碼請求。請點擊下方按鈕重設密碼：</p>
    
    <div style='text-align:center;margin:30px 0;'>
        <a href='{resetLink}' 
           style='display:inline-block;
                  padding:12px 30px;
                  background-color:#007bff;
                  color:#ffffff !important;
                  text-decoration:none;
                  border-radius:5px;
                  font-size:16px;'>
            重設密碼
        </a>
    </div>
    
    <p style='font-size:14px;color:#666;'>
        如果按鈕無法點擊，請複製以下連結至瀏覽器：<br>
        <a href='{resetLink}' style='color:#007bff;word-break:break-all;'>
            {resetLink}
        </a>
    </p>
    
    <p style='font-size:14px;color:#666;margin-top:20px;'>
        • 此連結將在24小時後失效<br>
        • 如果您沒有要求重設密碼，請忽略此郵件
    </p>
</div>";

                await _emailService.SendEmailAsync(model.Email, subject, body);
                
                return Ok(new { message = "重設密碼說明已發送至您的電子郵件。" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"發送郵件時發生錯誤: {ex.Message}");
                _logger.LogError($"錯誤詳情: {ex.StackTrace}");
                return StatusCode(500, new { message = "發送重設密碼郵件時發生錯誤。" });
            }
        }
    }

    public class LoginModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class SmtpSettings
    {
        [Required]
        public string Host { get; set; } = string.Empty;

        [Required]
        public int Port { get; set; } = 587; // 添加 Port 屬性，默認值為 587

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public bool EnableSsl { get; set; } = true; // 添加 SSL 支持
    }
}