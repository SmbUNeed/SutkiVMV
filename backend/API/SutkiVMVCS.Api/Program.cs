using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Resend;
using SutkiVMV.Data;
using SutkiVMV.Services;
using System.Text;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════
//  INFRASTRUCTURE
// ═══════════════════════════════════════════════════════════════════════

// ── Database ──────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ── File uploads ──────────────────────────────────────────────────────
var apartmentsUploadRelPath = builder.Configuration["Storage:ApartmentsUploadPath"]
    ?? "../../../../data/uploads";
var apartmentsUploadAbsPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, apartmentsUploadRelPath));
Directory.CreateDirectory(apartmentsUploadAbsPath);
var wikiUploadRelPath = builder.Configuration["Storage:WikiUploadPath"]
    ?? "../../../../data/wiki_places";
var wikiUploadAbsPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, wikiUploadRelPath));
Directory.CreateDirectory(wikiUploadAbsPath);
builder.Services.AddSingleton(new UploadSettings(apartmentsUploadAbsPath, wikiUploadAbsPath));

// ═══════════════════════════════════════════════════════════════════════
//  AUTHENTICATION & AUTHORISATION
// ═══════════════════════════════════════════════════════════════════════

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key не настроен");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════════════════════════
//  MVC & API
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler    =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title   = "SutkiVMV API",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        In          = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Введите JWT-токен: Bearer {token}"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            []
        }
    });
});

// ═══════════════════════════════════════════════════════════════════════
//  CORS
// ═══════════════════════════════════════════════════════════════════════

var allowedOrigins = (builder.Configuration["AllowedOrigin"] ?? "http://localhost")
    .Split(',', StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()));

// ═══════════════════════════════════════════════════════════════════════
//  BOTS
// ═══════════════════════════════════════════════════════════════════════



// ═══════════════════════════════════════════════════════════════════════
//  EMAIL
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(options =>
    options.ApiToken = builder.Configuration["Resend:ApiKey"]!);
builder.Services.AddTransient<IResend, ResendClient>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddSingleton<MaxBotWorker>();
builder.Services.AddSingleton<IMaxService>(sp => sp.GetRequiredService<MaxBotWorker>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MaxBotWorker>());

builder.Services.AddScoped<ICalService>();
builder.Services.AddHostedService<ICalSyncService>();

// ═══════════════════════════════════════════════════════════════════════
//  HOST OPTIONS — не останавливать хост если BackgroundService упал
// ═══════════════════════════════════════════════════════════════════════

builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.Ignore);

// ═══════════════════════════════════════════════════════════════════════
//  PIPELINE
// ═══════════════════════════════════════════════════════════════════════

var app = builder.Build();

app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider        = new PhysicalFileProvider(apartmentsUploadAbsPath),
    RequestPath         = "/uploads",
    ContentTypeProvider = new FileExtensionContentTypeProvider()
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider        = new PhysicalFileProvider(wikiUploadAbsPath),
    RequestPath         = "/wiki_places",
    ContentTypeProvider = new FileExtensionContentTypeProvider()
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// ═══════════════════════════════════════════════════════════════════════
//  RECORDS
// ═══════════════════════════════════════════════════════════════════════

public record UploadSettings(string ApartmentsUploadPath, string WikiUploadPath);
