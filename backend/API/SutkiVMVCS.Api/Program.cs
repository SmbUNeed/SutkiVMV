using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Resend;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.Services;
using SutkiVMVCS.Api.Services.Security;
using SutkiVMVCS.Controllers;
using SutkiVMVCS.Services;
using SutkiVMVCS.Services.YooKassa;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════
//  DATABASE
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ═══════════════════════════════════════════════════════════════════════
//  FILE UPLOADS
// ═══════════════════════════════════════════════════════════════════════

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
//  SECURITY
// ═══════════════════════════════════════════════════════════════════════

var certPath = Environment.GetEnvironmentVariable("DP_CERT_PATH")!;
var certPassword = Environment.GetEnvironmentVariable("DP_CERT_PASSWORD")!;
var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/etc/sutkivmv/keys"))
    .ProtectKeysWithCertificate(cert);

builder.Services.AddSingleton<FieldEncryptor>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════════════════════════
//  MVC & API
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SutkiVMV API",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
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
//  APP SERVICES
// ═══════════════════════════════════════════════════════════════════════

builder.Services.AddSingleton<FcmService>();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<SettingsService>();

builder.Services.AddHttpClient<ResendClient>();
builder.Services.AddHttpClient<PaymentsController>();
builder.Services.Configure<ResendClientOptions>(options =>
    options.ApiToken = builder.Configuration["Resend:ApiKey"]!);
builder.Services.AddTransient<IResend, ResendClient>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddSingleton<MaxBotWorker>();
builder.Services.AddSingleton<IMaxService>(sp => sp.GetRequiredService<MaxBotWorker>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MaxBotWorker>());

builder.Services.AddScoped<ICalService>();
builder.Services.AddHostedService<ICalSyncService>();

builder.Services.AddHttpClient<IYooKassaService, YooKassaService>();

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
//  HOST OPTIONS — не останавливать хост если BackgroundService упал
// ═══════════════════════════════════════════════════════════════════════

builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

// ═══════════════════════════════════════════════════════════════════════
//  PIPELINE
// ═══════════════════════════════════════════════════════════════════════

var app = builder.Build();

if (Environment.GetEnvironmentVariable("RUN_DECRYPT_ONCE") == "true")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var encryptor = scope.ServiceProvider.GetRequiredService<FieldEncryptor>();

    var clients = await db.Clients.ToListAsync();
    foreach (var c in clients)
    {
        c.FullName = encryptor.Decrypt(c.FullName);
        c.PhoneNumber = encryptor.Decrypt(c.PhoneNumber);
        c.Email = encryptor.Decrypt(c.Email);
    }

    var users = await db.Users.ToListAsync();
    foreach (var u in users)
        u.Email = encryptor.Decrypt(u.Email);

    await db.SaveChangesAsync();
    Console.WriteLine($"Decrypt-once done: {clients.Count} clients, {users.Count} users");
}

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(apartmentsUploadAbsPath),
    RequestPath = "/uploads",
    ContentTypeProvider = new FileExtensionContentTypeProvider()
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wikiUploadAbsPath),
    RequestPath = "/wiki_places",
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

// ── Settings API ─────────────────────────────────────────────────────
app.MapGet("/api/settings", async (SettingsService s) => await s.GetAsync());

app.MapPut("/api/admin/settings", async (SiteSettings dto, SettingsService s) =>
{
    await s.UpdateAsync(dto);
    return Results.Ok();
}).RequireAuthorization(policy => policy.RequireRole("Admin"));

app.Run();

// ═══════════════════════════════════════════════════════════════════════
//  RECORDS
// ═══════════════════════════════════════════════════════════════════════

public record UploadSettings(string ApartmentsUploadPath, string WikiUploadPath);