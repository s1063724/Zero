using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using Microsoft.Extensions.Configuration;

public class EmailService : IEmailService
{
    private readonly SmtpSettings _smtpSettings;
    private readonly ILogger<EmailService> _logger;
    private readonly SemaphoreSlim _throttler;
    private readonly int _maxEmailsPerMinute = 5;
    private readonly string _productDomain;

    public EmailService(IOptions<SmtpSettings> smtpSettings, ILogger<EmailService> logger, IConfiguration configuration)
    {
        _smtpSettings = smtpSettings.Value;
        _logger = logger;
        _throttler = new SemaphoreSlim(_maxEmailsPerMinute);
        _productDomain = configuration["AppSettings:ProductDomain"] ?? "productwebsite.com";
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            await _throttler.WaitAsync();
            
            var email = new MimeMessage();
            
            // 設置發件人
            var fromAddress = new MailboxAddress("ProductWebsite", _smtpSettings.Username);
            email.From.Add(fromAddress);
            email.Sender = fromAddress;
            email.To.Add(MailboxAddress.Parse(to));
            
            // 添加基本郵件標頭
            email.Headers.Add("Organization", "ProductWebsite");
            email.Headers.Add("Precedence", "bulk");
            email.Headers.Add("Auto-Submitted", "auto-generated");
            
            // 設置主旨
            email.Subject = $"[ProductWebsite] {subject}";
            
            var builder = new BodyBuilder();
            
            // 純文字版本
            builder.TextBody = $@"
{StripHtml(body)}

---
此郵件由 ProductWebsite 系統自動發送
© {DateTime.Now.Year} ProductWebsite";
            
            // HTML 版本
            builder.HtmlBody = $@"
<!DOCTYPE html>
<html lang='zh-TW'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width'>
</head>
<body style='font-family:Arial,sans-serif;line-height:1.6;color:#333;max-width:600px;margin:0 auto;padding:20px;'>
    <div style='background-color:#f8f9fa;padding:20px;border-radius:5px;margin-bottom:20px;'>
        <h1 style='color:#333;margin:0;font-size:24px;text-align:center;'>ProductWebsite</h1>
    </div>
    
    <div style='background-color:#fff;padding:20px;border-radius:5px;margin-bottom:20px;'>
        {body}
    </div>
    
    <div style='text-align:center;font-size:12px;color:#666;margin-top:20px;padding-top:20px;border-top:1px solid #eee;'>
        <p>此郵件由系統自動發送，請勿直接回覆</p>
        <p>© {DateTime.Now.Year} ProductWebsite</p>
    </div>
</body>
</html>";

            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation($"郵件已成功發送至 {to}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"發送郵件失敗: {ex.Message}");
            throw;
        }
        finally
        {
            _throttler.Release();
            await Task.Delay(TimeSpan.FromSeconds(12));
        }
    }

    private string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        
        return System.Text.RegularExpressions.Regex.Replace(
            html,
            "<[^>]*>",
            string.Empty
        ).Replace("&nbsp;", " ")
         .Replace("&amp;", "&")
         .Replace("&lt;", "<")
         .Replace("&gt;", ">")
         .Trim();
    }
}
