using System.Text;
using System.Threading.RateLimiting;
using DaLat.API;
using DaLat.API.Middleware;
using DaLat.API.Services;
using DaLat.BLL.Services;
using DaLat.DAL;
using DaLat.DAL.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- CSDL (MySQL 8.4 + Pomelo) ----------
var connectionString = cfg.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default");
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));

// ---------- Repository + Service (DAL/BLL) ----------
builder.Services.AddScoped<IPlaceRepository, PlaceRepository>();
builder.Services.AddScoped<ILanguageRepository, LanguageRepository>();
builder.Services.AddScoped<IVisitorRepository, VisitorRepository>();
builder.Services.AddScoped<IAdminUserRepository, AdminUserRepository>();
builder.Services.AddScoped<LanguageService>();
builder.Services.AddScoped<PlaceService>();
builder.Services.AddScoped<PlaceAdminService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<VisitorService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddHostedService<VisitCleanupService>();

// ---------- JWT ----------
builder.Services.Configure<JwtOptions>(cfg.GetSection("Jwt"));
var jwt = cfg.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key phải có ít nhất 32 ký tự (đặt JWT_KEY trong .env).");
builder.Services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false; // giữ nguyên tên claim: sub, name, role
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "name", RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();

// ---------- Ollama (dịch tự động) ----------
builder.Services.AddOptions<OllamaOptions>()
    .Bind(cfg.GetSection(OllamaOptions.Section))
    .Validate(OllamaOptions.IsValid,
        "Cấu hình Ollama không hợp lệ: BaseUrl phải là http/https, Model không rỗng, TimeoutSeconds từ 30 đến 3600.")
    .ValidateOnStart();
builder.Services.AddHttpClient<ITranslationService, OllamaTranslationService>((sp, client) =>
{
    var o = cfg.GetSection(OllamaOptions.Section).Get<OllamaOptions>() ?? new OllamaOptions();
    client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = Timeout.InfiniteTimeSpan; // thời gian chờ do CancellationTokenSource trong service quản lý
});

// ---------- Giới hạn tần suất theo IP (BR13) ----------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút." }, ct);
    };
    AddIpPolicy(o, "login", 10);
    AddIpPolicy(o, "ping", 240);
    AddIpPolicy(o, "playlog", 120);
    AddIpPolicy(o, "translate", 120);
});

static void AddIpPolicy(RateLimiterOptions o, string name, int perMinute) =>
    o.AddPolicy(name, ctx => RateLimitPartition.GetFixedWindowLimiter(
        $"{name}:{ctx.Connection.RemoteIpAddress}",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));

// ---------- Proxy (Cloudflare Tunnel) ----------
if (cfg.GetValue<bool>("TrustProxy"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

// ---------- Thư mục uploads ----------
var uploadPath = cfg["Uploads:Path"];
if (string.IsNullOrWhiteSpace(uploadPath)) uploadPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadPath);
builder.Services.AddSingleton(new UploadSettings(uploadPath));

builder.Services.AddControllers();

var app = builder.Build();

// ---------- Tạo bảng và nạp dữ liệu mẫu ----------
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();

// ---------- Pipeline ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<AdminLocalOnlyMiddleware>(); // trước ForwardedHeaders để còn thấy header gốc của tunnel
if (cfg.GetValue<bool>("TrustProxy")) app.UseForwardedHeaders();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadPath),
    RequestPath = "/uploads"
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

