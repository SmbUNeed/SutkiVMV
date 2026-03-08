using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext    _context;
    private readonly IConfiguration _config;
    private readonly ITelegramService _telegramService;

    public AuthController(AppDbContext context, IConfiguration config, ITelegramService telegramService)
    {
        _context = context;
        _config  = config;
        _telegramService = telegramService;
    }

    // ──────────────────────────────────────────────────
    // POST api/auth/register
    // ──────────────────────────────────────────────────
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto, CancellationToken ct)
    {
        dto.Email = dto.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return Conflict(new { message = "Пользователь с таким email уже существует" });

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest(new { message = "Пароль должен содержать минимум 6 символов" });

        // Создаём пользователя
        var user = new User
        {
            Email        = dto.Email,
            PasswordHash = HashPassword(dto.Password),
            Role         = "User",
            CreatedAt    = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct); // нужен Id для клиента

        // Создаём связанный Client (для системы бронирований)
        var client = new Client
        {
            FullName    = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            Email       = dto.Email,
            UserId      = user.Id
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync(ct);

        return Ok(BuildAuthResponse(user, client));
    }

    // ──────────────────────────────────────────────────
    // POST api/auth/login
    // ──────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        dto.Email = dto.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !VerifyPassword(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Неверный email или пароль" });

        // Ищем связанного клиента
        var client = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == user.Id);

        return Ok(BuildAuthResponse(user, client));
    }

    // ──────────────────────────────────────────────────
    // GET api/auth/me
    // ──────────────────────────────────────────────────
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var userId = GetUserId();
        var user   = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var client = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId);

        return Ok(ToUserDto(user, client));
    }

    // ──────────────────────────────────────────────────
    // PUT api/auth/me  — обновить профиль
    // ──────────────────────────────────────────────────
    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileDto dto, CancellationToken ct)
    {
        var userId = GetUserId();
        var user   = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        // Обновляем пароль если передан
        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Пароль должен содержать минимум 6 символов" });
            user.PasswordHash = HashPassword(dto.NewPassword);
        }

        // Обновляем данные в связанном Client
        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (client != null)
        {
            client.FullName    = dto.FullName.Trim();
            client.PhoneNumber = dto.PhoneNumber.Trim();
        }

        await _context.SaveChangesAsync(ct);
        return Ok(ToUserDto(user, client));
    }

    // ──────────────────────────────────────────────────
    // GET api/auth/my-bookings  — мои брони
    // ──────────────────────────────────────────────────
    [HttpGet("my-bookings")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<BookingDetailDto>>> GetMyBookings()
    {
        var userId = GetUserId();
        var client = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (client == null)
            return Ok(Array.Empty<BookingDetailDto>());

        var bookings = await _context.Bookings
            .Where(b => b.ClientId == client.Id)
            .Include(b => b.Apartment)
                .ThenInclude(a => a.ApartmentGallery)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Ok(bookings.Select(b => new BookingDetailDto
        {
            Id             = b.Id,
            ApartmentId    = b.ApartmentId,
            ApartmentTitle = b.Apartment.Title,
            ApartmentImage = b.Apartment.ApartmentGallery
                               .FirstOrDefault(g => g.IsMain)?.ImagePath
                          ?? b.Apartment.ApartmentGallery.FirstOrDefault()?.ImagePath,
            StartDate   = b.StartDate,
            EndDate     = b.EndDate,
            TotalPrice  = b.TotalPrice,
            Status      = b.Status,
            CreatedAt   = b.CreatedAt
        }));
    }

    // ──────────────────────────────────────────────────
    // POST api/auth/book  — забронировать (для залогиненных)
    // ──────────────────────────────────────────────────
    [HttpPost("book")]
    [Authorize]
    public async Task<ActionResult> Book(UserCreateBookingDto dto, CancellationToken ct)
    {
        if (dto.StartDate >= dto.EndDate)
            return BadRequest(new { message = "Дата заезда должна быть раньше даты выезда" });

        var userId = GetUserId();
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
        if (client == null)
            return BadRequest(new { message = "Профиль клиента не найден. Обратитесь в поддержку." });

        var apartment = await _context.Apartments.FindAsync(dto.ApartmentId);
        if (apartment == null || !apartment.IsActive || apartment.IsDeleted)
            return NotFound(new { message = "Квартира не найдена или недоступна" });

        bool overlap = await _context.Bookings.AnyAsync(b =>
            b.ApartmentId == dto.ApartmentId &&
            b.Status      != "Cancelled"     &&
            b.StartDate.ToUniversalTime()   <  dto.EndDate.ToUniversalTime() &&
            b.EndDate.ToUniversalTime() >  dto.StartDate.ToUniversalTime());

        if (overlap) return Conflict(new { message = "Квартира занята в выбранные даты" });

        var nights  = (dto.EndDate - dto.StartDate).Days;
        var booking = new Booking
        {
            ApartmentId = dto.ApartmentId,
            ClientId    = client.Id,
            StartDate   = dto.StartDate.ToUniversalTime(),
            EndDate     = dto.EndDate.ToUniversalTime(),
            TotalPrice  = apartment.PricePerNight * nights,
            Status      = "Pending",
            CreatedAt   = DateTime.UtcNow.ToUniversalTime()
        };
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(ct);

        await _telegramService.SendBookingNotificationAsync(
        $"🔔 <b>Новое бронирование!</b>\n" +
        $"🏠 Объект: {apartment.Title}\n" +
        $"📅 Даты: {dto.StartDate:dd.MM} - {dto.EndDate:dd.MM}\n" +
        $"Номер телефона: {client.PhoneNumber}", booking.Id);

        return Ok(booking);
    }

    // ──────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ──────────────────────────────────────────────────

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private AuthResponseDto BuildAuthResponse(User user, Client? client) => new()
    {
        Token = GenerateToken(user),
        User  = ToUserDto(user, client)
    };

    private static UserDto ToUserDto(User user, Client? client) => new()
    {
        Id          = user.Id,
        Email       = user.Email,
        FullName    = client?.FullName    ?? "",
        PhoneNumber = client?.PhoneNumber ?? "",
        Role        = user.Role,
        ClientId    = client?.Id
    };

    private string GenerateToken(User user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var token = new JwtSecurityToken(
            claims:            claims,
            expires:           DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // PBKDF2 — встроенный хэш, не требует внешних пакетов
    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key  = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(key)}";
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var parts = hash.Split(':');
        if (parts.Length != 2) return false;
        try
        {
            var salt    = Convert.FromBase64String(parts[0]);
            var stored  = Convert.FromBase64String(parts[1]);
            var attempt = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(stored, attempt);
        }
        catch { return false; }
    }
}
