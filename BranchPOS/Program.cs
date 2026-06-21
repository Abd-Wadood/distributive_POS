using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Repositories;
using BranchPOS.Services;
using BranchPOS.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDataProtection();
builder.Services.Configure<PosOperationalOptions>(builder.Configuration.GetSection("PosOperations"));
builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection("Dashboard"));
builder.Services.Configure<SecurityRateLimitOptions>(builder.Configuration.GetSection("SecurityRateLimits"));
builder.Services.Configure<IdempotencyOptions>(builder.Configuration.GetSection("Idempotency"));
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long?>("RequestLimits:MaxRequestBodyBytes") ?? 1_048_576;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int?>("RequestLimits:RequestHeadersTimeoutSeconds") ?? 15);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int?>("RequestLimits:KeepAliveTimeoutSeconds") ?? 60);
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = builder.Configuration.GetValue<long?>("RequestLimits:MaxFormBytes") ?? 65_536;
    options.ValueLengthLimit = builder.Configuration.GetValue<int?>("RequestLimits:MaxFormValueLength") ?? 16_384;
});
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int?>("RequestLimits:DefaultTimeoutSeconds") ?? 20),
        TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable
    };
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var audit = context.HttpContext.RequestServices.GetRequiredService<IAuditLogService>();
        var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "UnknownPolicy";
        await audit.LogSecurityAsync("RateLimitHit", "Warning", $"Rate limit exceeded for {policy}.", cancellationToken: cancellationToken);

        var message = policy switch
        {
            "LoginPolicy" => "Too many login attempts. Please wait and try again.",
            "OrderFinalizePolicy" => "You are submitting orders too quickly. Please wait.",
            "ProductSearchPolicy" => "Too many product searches. Please wait.",
            "SessionStartPolicy" => "Too many session start attempts. Please wait.",
            "TerminalHeartbeatPolicy" => "Heartbeat received too frequently.",
            "ReportsPolicy" => "Too many report requests. Please wait.",
            _ => "Too many requests. Please wait and try again."
        };

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new { success = false, message }, cancellationToken);
    };

    var rateOptions = builder.Configuration.GetSection("SecurityRateLimits").Get<SecurityRateLimitOptions>() ?? new SecurityRateLimitOptions();
    options.AddFixedWindowLimiter("LoginPolicy", limiter =>
    {
        limiter.PermitLimit = rateOptions.LoginIpPermitLimit;
        limiter.Window = TimeSpan.FromSeconds(rateOptions.WindowSeconds);
        limiter.QueueLimit = 0;
    });
    options.AddPolicy("OrderFinalizePolicy", httpContext => RateLimitPartition.GetFixedWindowLimiter(GetTerminalUserIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = rateOptions.OrderFinalizePermitLimit,
        Window = TimeSpan.FromSeconds(rateOptions.OrderFinalizeWindowSeconds),
        QueueLimit = 0
    }));
    options.AddPolicy("ProductSearchPolicy", httpContext => RateLimitPartition.GetFixedWindowLimiter(GetTerminalUserIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = rateOptions.ProductSearchPermitLimit,
        Window = TimeSpan.FromSeconds(rateOptions.WindowSeconds),
        QueueLimit = 0
    }));
    options.AddPolicy("SessionStartPolicy", httpContext => RateLimitPartition.GetFixedWindowLimiter(GetTerminalUserIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = rateOptions.SessionStartPermitLimit,
        Window = TimeSpan.FromSeconds(rateOptions.WindowSeconds),
        QueueLimit = 0
    }));
    options.AddPolicy("TerminalHeartbeatPolicy", httpContext => RateLimitPartition.GetFixedWindowLimiter(GetTerminalUserIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = rateOptions.TerminalHeartbeatPermitLimit,
        Window = TimeSpan.FromSeconds(rateOptions.HeartbeatWindowSeconds),
        QueueLimit = 0
    }));
    options.AddPolicy("ReportsPolicy", httpContext => RateLimitPartition.GetFixedWindowLimiter(GetTerminalUserIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = rateOptions.ReportsPermitLimit,
        Window = TimeSpan.FromSeconds(rateOptions.WindowSeconds),
        QueueLimit = 0
    }));
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 6;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IRestaurantInventoryService, RestaurantInventoryService>();
builder.Services.AddScoped<IInventoryTransactionService, InventoryTransactionService>();
builder.Services.AddScoped<IInventoryAdjustmentService, InventoryAdjustmentService>();
builder.Services.AddScoped<IManualKitchenUsageService, ManualKitchenUsageService>();
builder.Services.AddScoped<IStockCountService, StockCountService>();
builder.Services.AddScoped<IPreparationService, PreparationService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderStockReservationService, OrderStockReservationService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductAvailabilityService, ProductAvailabilityService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IBranchContextService, BranchContextService>();
builder.Services.AddScoped<IUserSessionService, UserSessionService>();
builder.Services.AddScoped<ITerminalContextService, TerminalContextService>();
builder.Services.AddScoped<ITerminalService, TerminalService>();
builder.Services.AddScoped<IIdentitySeedService, IdentitySeedService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IErrorLoggingService, ErrorLoggingService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISessionCodeGeneratorService, SessionCodeGeneratorService>();
builder.Services.AddScoped<IRequestIdentityService, RequestIdentityService>();
builder.Services.AddScoped<ILoginSecurityService, LoginSecurityService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
builder.Services.AddSingleton<IPosMenuCacheInvalidator, PosMenuCacheInvalidator>();
builder.Services.AddHostedService<IdempotencyCleanupService>();
builder.Services.AddHostedService<AuditLogCleanupService>();

var app = builder.Build();

app.UseMiddleware<FriendlyExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isBypassPath =
        path.StartsWithSegments("/TerminalSetup") ||
        path.StartsWithSegments("/Account") ||
        path.StartsWithSegments("/css") ||
        path.StartsWithSegments("/js") ||
        path.StartsWithSegments("/lib") ||
        path.StartsWithSegments("/BranchPOS.styles.css") ||
        path.StartsWithSegments("/favicon.ico");

    if (!isBypassPath)
    {
        var terminalContextService = context.RequestServices.GetRequiredService<ITerminalContextService>();
        if (await terminalContextService.GetCurrentTerminalAsync() is null)
        {
            context.Response.Redirect($"/TerminalSetup?returnUrl={Uri.EscapeDataString(context.Request.PathBase + context.Request.Path + context.Request.QueryString)}");
            return;
        }
    }

    await next();
});

app.UseRequestTimeouts();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<IIdentitySeedService>();
    await seedService.SeedAsync();
}

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

static string GetTerminalUserIpKey(HttpContext context)
{
    var terminalCode = context.RequestServices.GetRequiredService<ITerminalContextService>().GetTerminalCodeFromRequest() ?? "no-terminal";
    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    var ip = string.IsNullOrWhiteSpace(forwardedFor)
        ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip"
        : forwardedFor.Split(',')[0].Trim();
    return $"{terminalCode}:{userId}:{ip}";
}
