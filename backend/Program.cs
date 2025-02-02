using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Net;
using System.Security.Claims;
using Serilog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

const string FrontendUrl = "http://localhost:8080";

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog 日誌
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/UI_Mgmt_.log",
                  rollingInterval: RollingInterval.Day,
                  outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// 載入 `appsettings.json`
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// 添加資料庫上下文
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 註冊 SMTP 設定
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IEmailService, EmailService>();

// 配置 Kestrel 使用隨機端口
builder.WebHost.UseUrls(); // 清除所有預設 URL

// 配置 Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // 使用 IP 地址而不是 localhost
    serverOptions.Listen(IPAddress.Parse("127.0.0.1"), 0, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

// 添加 CORS 服務
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins("http://localhost:8080")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 添加控制器
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// 配置 Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// 認證 (Google + Cookie)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/api/users/google-login";
    options.Cookie.Name = ".AspNetCore.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    
    // 禁用自動重定向
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
    };
})
.AddGoogle(options =>
{
    var clientId = builder.Configuration["Authentication:Google:ClientId"] ?? 
        throw new InvalidOperationException("Google ClientId not configured");
    var clientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? 
        throw new InvalidOperationException("Google ClientSecret not configured");
    
    options.ClientId = clientId;
    options.ClientSecret = clientSecret;
    options.CallbackPath = "/api/users/google-callback";
    
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.HttpOnly = true;
    options.CorrelationCookie.IsEssential = true;
    
    // Google 登入成功後觸發 Email 通知
    options.Events = new OAuthEvents
    {
        OnTicketReceived = async context =>
        {
            var emailService = context.HttpContext.RequestServices.GetRequiredService<IEmailService>();

            var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrEmpty(email))
            {
                await emailService.SendEmailAsync(email, "登入成功通知", "您已成功登入商品網站！");
            }

            context.Properties.IsPersistent = true;
        },
        OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.Redirect($"{FrontendUrl}?error={Uri.EscapeDataString(context.Failure?.Message ?? "Unknown error")}");
            return Task.CompletedTask;
        }
    };

    options.SaveTokens = true;
});

// 添加日誌記錄
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// 自動應用遷移
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();  // 這行會自動更新數據庫結構
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// 中間件順序
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// CORS 中間件
app.UseCors("AllowVueApp");

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// 端點映射
app.MapControllers();

// 記錄應用程序啟動的端口
app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Urls;
    foreach (var address in addresses)
    {
        Log.Information($"正在監聽: {address}");
    }
});

app.Run();
