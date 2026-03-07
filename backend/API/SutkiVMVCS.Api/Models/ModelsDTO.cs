namespace SutkiVMV.ModelsDTO;

// ── Apartment ──────────────────────────────────────────────────────────

public class ApartmentDto
{
    public int     Id            { get; set; }
    public string  Title         { get; set; } = "";
    public string  Address       { get; set; } = "";
    public string  Description   { get; set; } = "";
    public int     Guests        { get; set; }
    public decimal PricePerNight { get; set; }
    public int     RoomsCount    { get; set; }
    public decimal Area          { get; set; }
    public bool    IsActive      { get; set; }
    public bool    IsDeleted     { get; set; }
    public string? MainImage     { get; set; }

    public List<GalleryItemDto> Gallery   { get; set; } = [];
    public List<AmenityDto>    Amenities { get; set; } = [];
    public List<BookingDto>     Bookings  { get; set; } = [];
}

public class GalleryItemDto
{
    public int    Id        { get; set; }
    public string ImagePath { get; set; } = "";
    public bool   IsMain    { get; set; }
}

// ── Amenities ──────────────────────────────────────────────────────────

public class AmenityDto
{
    public int    Id   { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "fa-check";
}

public class CreateAmenityDto
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "fa-check";
}

public class SetAmenitiesDto
{
    public List<int> AmenityIds { get; set; } = [];
}

// ── Booking ────────────────────────────────────────────────────────────

/// <summary>Компактный DTO для встраивания в ApartmentDto (список занятых дат).</summary>
public class BookingDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate   { get; set; }
    public string   Status    { get; set; } = "";
}

/// <summary>Входящий DTO для создания брони администратором.</summary>
public class CreateBookingDto
{
    public int      ApartmentId { get; set; }
    public int      ClientId    { get; set; }
    public DateTime StartDate   { get; set; }
    public DateTime EndDate     { get; set; }
}

/// <summary>Входящий DTO для создания брони залогиненным пользователем.</summary>
public class UserCreateBookingDto
{
    public int      ApartmentId { get; set; }
    public DateTime StartDate   { get; set; }
    public DateTime EndDate     { get; set; }
}

/// <summary>Полный DTO брони для личного кабинета.</summary>
public class BookingDetailDto
{
    public int      Id             { get; set; }
    public int      ApartmentId    { get; set; }
    public string   ApartmentTitle { get; set; } = "";
    public string?  ApartmentImage { get; set; }
    public DateTime StartDate      { get; set; }
    public DateTime EndDate        { get; set; }
    public decimal  TotalPrice     { get; set; }
    public string   Status         { get; set; } = "";
    public DateTime CreatedAt      { get; set; }
    public int      Nights         => (EndDate - StartDate).Days;
}

/// <summary>Расширенный DTO брони для административной панели.</summary>
public class AdminBookingDto
{
    public int      Id             { get; set; }
    public int      ApartmentId    { get; set; }
    public string   ApartmentTitle { get; set; } = "";
    public int      ClientId       { get; set; }
    public string   ClientName     { get; set; } = "";
    public string   ClientPhone    { get; set; } = "";
    public DateTime StartDate      { get; set; }
    public DateTime EndDate        { get; set; }
    public decimal  TotalPrice     { get; set; }
    public string   Status         { get; set; } = "";
    public DateTime CreatedAt      { get; set; }
    public int      Nights         => (EndDate - StartDate).Days;
}

// ── Client ─────────────────────────────────────────────────────────────

public class ClientDto
{
    public int    Id          { get; set; }
    public string FullName    { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email       { get; set; } = "";
    public int?   UserId      { get; set; }
}

// ── Auth ───────────────────────────────────────────────────────────────

public class RegisterDto
{
    public string FullName    { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email       { get; set; } = "";
    public string Password    { get; set; } = "";
}

public class LoginDto
{
    public string Email    { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AuthResponseDto
{
    public string  Token { get; set; } = "";
    public UserDto User  { get; set; } = null!;
}

public class UserDto
{
    public int    Id          { get; set; }
    public string Email       { get; set; } = "";
    public string FullName    { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Role        { get; set; } = "";
    public int?   ClientId    { get; set; }
}

public class UpdateProfileDto
{
    public string  FullName    { get; set; } = "";
    public string  PhoneNumber { get; set; } = "";
    public string? NewPassword { get; set; }
}
