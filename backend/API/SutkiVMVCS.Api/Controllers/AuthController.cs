using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SutkiVMV.Data;
using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;
using SutkiVMVCS.Api.Services.Security;
using SutkiVMVCS.Data;
using SutkiVMVCS.Services.Security;
using SutkiVMVCS.Services.Shared;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SutkiVMV.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext    _context;
    private readonly FcmService _fcm;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;
    private readonly IMaxService _maxBotService;

    public AuthController(AppDbContext context, FcmService fcm, IConfiguration config, IMaxService maxService, IEmailService emailService)
    {
        _context = context;
        _fcm = fcm;
        _config  = config;
        _maxBotService = maxService;
	    _emailService = emailService;
    }

    // ──────────────────────────────────────────────────
    // POST api/auth/register
    // ──────────────────────────────────────────────────
    [HttpPost("register")]
    public async Task<ActionResult> Register(RegisterDto dto, TrigramService ts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Email не заполнен" });

        dto.Email = dto.Email.Trim().ToLowerInvariant();
        dto.PhoneNumber = PhoneHelper.Normalize(dto.PhoneNumber)!;

        byte[] searchFilter = ts.GenerateBloomFilter(dto.Email);

        var potentialMatches = await _context.Clients.Where(c => AppDbContext.BitwiseAndBytes(c.EmailTrigram, searchFilter) == searchFilter).ToListAsync();

        var actuallyMatches = potentialMatches.Where(pm => pm.Email == dto.Email).Any(pm => pm.Email == dto.Email);

        if (actuallyMatches)
            return Conflict(new { message = "Пользователь с таким email уже существует" });

        if (await _context.PendingRegistrations.AsNoTracking().AnyAsync(p => p.Email == dto.Email, ct))
        {
            var old = await _context.PendingRegistrations
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Email == dto.Email, ct);
            if (old != null)
                _context.PendingRegistrations.Remove(old);
        }

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest(new { message = "Пароль должен содержать минимум 6 символов" });

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var pending = new PendingRegistration
        {
            Email = dto.Email,
            PasswordHash = HashPassword(dto.Password),
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        _context.PendingRegistrations.Add(pending);
        await _context.SaveChangesAsync(ct);

        var confirmUrl = $"https://sutkivmv.ru/confirm-email.html?token={token}&email={Uri.EscapeDataString(dto.Email)}";
        try
        {
            await _emailService.SendConfirmationEmailAsync(dto.Email, confirmUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL ERROR] {ex}");
            return StatusCode(502, new { message = "Не удалось отправить письмо, попробуйте ещё раз." });
        }

        return Ok(new { message = "Проверьте почту — мы отправили письмо для подтверждения." });
    }

    [HttpGet("users-list")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UsersList()
    {
        var users = await _context.Users.AsNoTracking().OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Role, u.CreatedAt, u.EmailConfirmed })
            .ToListAsync();

        var clients = await _context.Clients.AsNoTracking()
            .Where(c => c.UserId != null)
            .Select(c => new { c.UserId, c.FullName, c.PhoneNumber })
            .ToListAsync();

        var result = users.Select(u => new
        {
            u.Id,
            u.Role,
            u.CreatedAt,
            u.EmailConfirmed,
            Client = clients.FirstOrDefault(c => c.UserId == u.Id)
        });

        return Ok(result);
    }

    // ──────────────────────────────────────────────────
    // POST api/auth/login
    // ──────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto, TrigramService ts)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Email не заполнен" });

        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "Пароль не заполнен" });

        var filter = ts.GenerateBloomFilter(dto.Email);

        var potential = await _context.Clients.Where(c => AppDbContext.BitwiseAndBytes(c.EmailTrigram, filter) == filter).ToListAsync();
        foreach(var d in potential) {
            Console.WriteLine(d.Email);
        }
        var user = potential.Where(u => u.Email == dto.Email).FirstOrDefault()?.User;

        if (user?.Client == null || user.Client.EmailTrigram == null)
        {
            // Проблема в данных: у пользователя нет связанного Client или поле пустое
            Console.WriteLine($"DEBUG: У пользователя {user.Email} отсутствует Client или EmailTrigram");
        }

        if (!VerifyPassword(dto.Password, user.PasswordHash))
        {
            // Если дошли сюда, значит пользователь найден, но пароль не совпадает
            Console.WriteLine("DEBUG: Пользователь найден, но пароль не совпадает");
            return Unauthorized(new { message = "Неверный email или пароль" });
        }

        // Ищем связанного клиента
        var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == user.Id);

        if (client == null) return Unauthorized(new { message = "Клиент не найден" });

        return Ok(BuildAuthResponse(user));
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

        return Ok(ToUserDto(user));
    }

    // ──────────────────────────────────────────────────
    // PUT api/auth/me  — обновить профиль
    // ──────────────────────────────────────────────────
    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileDto dto, CancellationToken ct)
    {
        var userId = GetUserId();
        var user = await _context.Users.Include(u => u.Client).FirstAsync(u => u.Id == userId);
        if (user == null) return NotFound();

        // Обновляем пароль если передан
        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Пароль должен содержать минимум 6 символов" });
            user.PasswordHash = HashPassword(dto.NewPassword);
        }
        user.Client!.FullName    = dto.FullName.Trim();
        user.Client.PhoneNumber = dto.PhoneNumber.Trim();

        await _context.SaveChangesAsync(ct);
        return Ok(ToUserDto(user));
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
            .Include(b => b.Payments)
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
            PrepaymentAmount = b.PrepaymentAmount,
            PaidAmount  = b.PaidAmount,
            Status      = b.Status.ToString(),
            CreatedAt   = b.CreatedAt
        }));
    }

    // ──────────────────────────────────────────────────
    // POST api/auth/book  — забронировать (для залогиненных)
    // ──────────────────────────────────────────────────
    [HttpPost("book")]
    [Authorize]
    public async Task<ActionResult> Book(UserCreateBookingDto dto, BookingService bookingService, CancellationToken ct)
    {
        if (dto.StartDate >= dto.EndDate)
            return BadRequest(new { message = "Дата заезда должна быть раньше даты выезда" });

        var userId = GetUserId();
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (client == null || client.InBlackList)
            return BadRequest(new { message = "Профиль клиента не найден. Обратитесь в поддержку." });

        if ((dto.EndDate - dto.StartDate).Days > 10) return BadRequest(new { message = "Бесплатное бронирование доступно на срок не более 10 суток. Пожалуйста, выберите оплату." });

        bool hasRecentPendingBooking = await _context.Bookings.AnyAsync(b =>
            b.ClientId == client.Id &&
            b.Status == BookingStatus.Pending &&
            (DateTime.UtcNow - b.CreatedAt).TotalHours <= 24,
            ct);
        if (hasRecentPendingBooking)
        {
            return BadRequest(new { message = "Вы уже создавали заявку на бесплатное бронирование в последние 24 часа. Попробуйте позже или выберите оплату." });
        }

        var apartment = await _context.Apartments.FindAsync(new object?[] { dto.ApartmentId }, ct);
        if (apartment == null || !apartment.IsActive || apartment.IsDeleted)
            return NotFound(new { message = "Квартира не найдена или недоступна" });

        var (booking, error) = await bookingService.CreateAsync(
            apartment, client.Id, dto.Guests, dto.StartDate, dto.EndDate, BookingSource.SiteUser, ct);
        if (booking == null) return Conflict(new { message = error });

        try
        {
            await _maxBotService.SendBookingNotificationAsync(
                $"🔔 Новое бронирование!\n" +
                $"🏠 Объект: {apartment.Title}\n" +
                $"📅 Даты: {dto.StartDate:dd.MM} - {dto.EndDate:dd.MM}\n" +
                $"Номер телефона: {client.PhoneNumber}", booking.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MaxBot] SendBookingNotification error: {ex.Message}");
        }

        var data = new Dictionary<string, string>
        {
            { "bookingId", booking.Id.ToString() }
        };

        var tokens = await _context.DeviceTokens.Select(t => t.Token).ToListAsync(ct);

        foreach (var token in tokens)
        {
            await _fcm.SendAsync(token, "Новая бронь", $"{apartment.Title}: {dto.StartDate:dd.MM} — {dto.EndDate:dd.MM}", data);
        }

        return Ok(booking);
    }

    [HttpPost("confirm-email")]
    public async Task<ActionResult> ConfirmEmail(ConfirmEmailDto dto, TrigramService ts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Token))
            return BadRequest(new { message = "Неверные данные" });

        var email = dto.Email.Trim().ToLowerInvariant();

        var pending = await _context.PendingRegistrations
            .FirstOrDefaultAsync(p => p.Email == email && p.Token == dto.Token, ct);

        if (pending == null || pending.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "Ссылка недействительна или истекла" });

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var user = new User
        {
            PasswordHash = pending.PasswordHash,
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);   // теперь user.Id настоящий

        byte[] searchFilter = ts.GenerateBloomFilter(pending.PhoneNumber);

        var potentialMatches = await _context.Clients.Where(c => AppDbContext.BitwiseAndBytes(c.PhoneTrigram, searchFilter) == searchFilter).ToListAsync();

        var client = potentialMatches.Where(pm => pm.PhoneNumber == pending.PhoneNumber).FirstOrDefault(pm => pm.PhoneNumber == pending.PhoneNumber);

        if (client == null)
        {
            _context.Clients.Add(new Client
            {
                FullName = pending.FullName,
                PhoneNumber = pending.PhoneNumber,
                Email = pending.Email,
                UserId = user.Id
            });
        }
        else
        {
            client.UserId = user.Id;
            client.FullName = pending.FullName;
            client.Email = pending.Email;
        }

        _context.PendingRegistrations.Remove(pending);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(new { message = "Email подтверждён! Теперь вы можете войти." });
    }

    [HttpPost("change-password-email")]
    public async Task<ActionResult> ChangeUserPassword([FromBody] string email, TrigramService ts)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email не заполнен" });

        email = email.Trim().ToLowerInvariant();

        byte[] searchFilter = ts.GenerateBloomFilter(email);

        var potentialMatches = await _context.Clients.Include(c => c.User).Where(c => AppDbContext.BitwiseAndBytes(c.EmailTrigram, searchFilter) == searchFilter).ToListAsync();

        var actuallyMatches = potentialMatches.Where(pm => pm.Email == email).FirstOrDefault(pm => pm.Email == email);

        var user = actuallyMatches?.User; //REMAKE
        if (user == null)
            return BadRequest(new { message = "Данный email не зарегистрирован" });


        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetToken = token;
        user.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);
        await _context.SaveChangesAsync();

        var confirmUrl = $"https://sutkivmv.ru/change-password.html?token={token}&email={Uri.EscapeDataString(email)}";
        try { await _emailService.SendChangePasswordAsync(email, confirmUrl); }
        catch (Exception ex) { Console.WriteLine($"[EMAIL ERROR] {ex.Message}"); }

        return Ok(new { message = "Проверьте почту — мы отправили письмо для изменения пароля." });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordDto dto, TrigramService ts)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Token) ||
            string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest(new { message = "Неверные данные" });

        var email = dto.Email.Trim().ToLowerInvariant();

        var searchFilter = ts.GenerateBloomFilter(dto.Email);

        var potentialMatches = await _context.Users.Include(u => u.Client).Where(u => AppDbContext.BitwiseAndBytes(u.Client!.EmailTrigram, searchFilter) == searchFilter).ToListAsync();

        var user = potentialMatches.Where(
            pm => pm.Email == dto.Email &&
            pm.PasswordResetToken == dto.Token &&
            pm.PasswordResetExpiry > DateTime.UtcNow).FirstOrDefault();

        if (user == null)
            return BadRequest(new { message = "Ссылка недействительна или истекла" });

        user.PasswordHash = HashPassword(dto.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Пароль успешно изменён" });
    }

    // ──────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ──────────────────────────────────────────────────

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private AuthResponseDto BuildAuthResponse(User user) => new()
    {
        Token = GenerateToken(user),
        User  = ToUserDto(user)
    };

    private static UserDto ToUserDto(User user) => new()
    {
        Id          = user.Id,
        Email       = user.Email ?? "",
        FullName    = user.Client!.FullName    ?? "",
        PhoneNumber = user.Client.PhoneNumber ?? "",
        Role        = user.Role,
        ClientId    = user.Client.Id
    };

    private string GenerateToken(User user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email!),
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
