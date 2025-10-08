using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SubscriptionManager.Background;
using SubscriptionManager.Channels;
using SubscriptionManager.Data;
using SubscriptionManager.Middleware;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Services.Implementations;
using SubscriptionManager.Services.Interfaces;
using System.Text;
using System.Threading.Channels;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// MVC + Razor
builder.Services.AddControllersWithViews();

// Options
builder.Services.Configure<JwtSettings>(config.GetSection("Jwt"));
builder.Services.Configure<ChannelSettings>(config.GetSection("ChannelOptions"));

// Dapper connection factory
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

// Channels
builder.Services.AddSingleton<ChannelProvider>();
builder.Services.AddSingleton<ChannelReader<LogMessage>>(sp => sp.GetRequiredService<ChannelProvider>().LogReader);
builder.Services.AddSingleton<ChannelWriter<LogMessage>>(sp => sp.GetRequiredService<ChannelProvider>().LogWriter);
builder.Services.AddSingleton<ChannelReader<NotificationMessage>>(sp => sp.GetRequiredService<ChannelProvider>().NotificationReader);
builder.Services.AddSingleton<ChannelWriter<NotificationMessage>>(sp => sp.GetRequiredService<ChannelProvider>().NotificationWriter);
builder.Services.AddSingleton<IChannelProducer<LogMessage>>(sp => new ChannelProducer<LogMessage>(sp.GetRequiredService<ChannelWriter<LogMessage>>()));
builder.Services.AddSingleton<IChannelProducer<NotificationMessage>>(sp => new ChannelProducer<NotificationMessage>(sp.GetRequiredService<ChannelWriter<NotificationMessage>>()));

// Services
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddSingleton<ILogService, LogService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>(); 


builder.Services.AddSingleton<IOutboxService, OutboxService>();
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("webhooks", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: key => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromSeconds(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// Background services
builder.Services.AddHostedService<LogChannelConsumer>();
builder.Services.AddHostedService<NotificationChannelConsumer>();
builder.Services.AddHostedService<SubscriptionExpiryChecker>();

// Auth: Cookies + JWT
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name = "SubscriptionManager.Auth";
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var jwt = config.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key ?? string.Empty)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                // Allow token from Authorization header ("Bearer <token>") or from cookie "AppJwt"
                if (string.IsNullOrEmpty(ctx.Token))
                {
                    var token = ctx.Request.Cookies["AppJwt"];
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        ctx.Token = token;
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Global exception handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
var defaultCultureName = builder.Configuration.GetValue<string>("App:DefaultCulture") ?? "en-IN";
var defaultCulture = new CultureInfo(defaultCultureName);
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
    SupportedCultures = new[] { defaultCulture },
    SupportedUICultures = new[] { defaultCulture }
};
app.UseRequestLocalization(localizationOptions);


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ensure seed hashes are converted from sentinel to real BCrypt
using (var scope = app.Services.CreateScope())
{
    var userSvc = scope.ServiceProvider.GetRequiredService<IUserService>();
    try { await userSvc.EnsureSeededPasswordHashesAsync(); } catch {  }
}

app.Run();