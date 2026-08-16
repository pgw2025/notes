using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Notes.Api.Data;
using Notes.Api.Infrastructure;
using Notes.Api.Models;
using Notes.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== Database (MySQL via Pomelo) =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("未配置数据库连接字符串 DefaultConnection");

// 使用固定版本，避免启动时强制连接数据库（AutoDetect 需要在线连接）
var serverVersion = new MySqlServerVersion(new Version(8, 0, 30));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// ===== ASP.NET Identity =====
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ===== JWT 认证 =====
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("未配置 Jwt:Key");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NotesApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NotesClient";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
        // 允许通过 query 参数 access_token 传递令牌，用于 <img>/文件下载等无法设置请求头的场景
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ===== 业务服务 =====
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// ===== CORS (允许前端开发服务器访问) =====
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:3000" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// ===== Controllers + Swagger =====
// 全局 JSON 序列化配置：
// 1) DateTime 统一按 UTC 输出带 "Z" 后缀（如 "2026-08-16T07:30:00Z"）
//    原因：MySQL 读回的 DateTime 是 Unspecified Kind，默认序列化不带 Z，
//    前端 new Date() 会当本地时间解析，导致显示比北京时间少 8 小时。
//    设置为 Utc 后前端 new Date() 按 UTC 解析，getHours() 等本地方法自动转换为本地时间。
// 2) PascalCase 实体属性名 → camelCase（与前端 JS 一致）
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonDateTimeUtcConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Notes API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "输入 JWT 令牌"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ===== 中间件管道 =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 自动迁移并创建数据库（Development + Production 均执行）
// 注：发布到生产环境首次启动时会自动执行 EF Core Migrations 建表（包括 Identity 表和业务表）
// 如迁移失败不会中断服务，但需查看日志修复
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        Console.WriteLine("✓ 数据库迁移完成");
    }
    catch (Exception ex)
    {
        Console.WriteLine("⚠ 数据库迁移失败，请确认 MySQL 已启动且连接字符串正确：" + ex.Message);
    }
}

app.Run();
