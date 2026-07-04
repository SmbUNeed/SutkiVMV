using SutkiVMV.Models;
using System.ComponentModel.DataAnnotations;

namespace SutkiVMV.ModelsDTO;

// ── Apartment ──────────────────────────────────────────────────────────
public class ApartmentDto
{
    public int Id { get; set; }
    public string AdminTag { get; set; } = "";
    public string Title { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public int Guests { get; set; }
    public decimal PricePerNight { get; set; }
    public int RoomsCount { get; set; }
    public decimal Area { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? MainImage { get; set; }

    public List<GalleryItemDto> Gallery { get; set; } = [];
    public List<AmenityDto> Amenities { get; set; } = [];
    public List<BookingDto> Bookings { get; set; } = [];
    public List<RuleDto> Rules { get; set; } = [];
    public List<NearbyPlaceDto> Nearby { get; set; } = [];
}
public class CreateApartmentDto
{
    public string AdminTag { get; set; } = "";
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    [Required, MaxLength(300)] public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    [Range(1, 50)] public int Guests { get; set; }
    [Range(0, 100000)] public decimal PricePerNight { get; set; }
    [Range(1, 20)] public int RoomsCount { get; set; }
    [Range(1, 10000)] public decimal Area { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ApartmentShortDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string AdminTag { get; set; } = "";
    public List<BusyPeriodDto> BusyDates { get; set; } = new List<BusyPeriodDto>();
}

public record ApartmentSummaryDto(
    int Id,
    string Title,
    string? MainImage,
    decimal PricePerNight,
    int Guests,
    decimal Area,
    int RoomsCount,
    List<string> Amenities 
);

public class ApartmentDetailDto()
{
    public int Id { get; set; }
    public string AdminTag { get; set; } = "";
    public string Title { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public int Guests { get; set; }
    public decimal PricePerNight { get; set; }
    public int RoomsCount { get; set; }
    public decimal Area { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? MainImage { get; set; }

    public List<GalleryItemDto> Gallery { get; set; } = [];
    public List<AmenityDto> Amenities { get; set; } = [];
    public List<BusyPeriodDto> BusyPeriods { get; set; } = [];
    public List<RuleDto> Rules { get; set; } = [];
    public List<NearbyPlaceDto> Nearby { get; set; } = [];
}

public class RuleDto
{
    public int Id { get; set; }
    public string Icon { get; set; } = "";
    public string Text { get; set; } = "";
}

public class CreateRuleDto
{
    [Required] public string Icon { get; set; } = "";
    [Required] public string Text { get; set; } = "";
}

public class NearbyPlaceDto
{
    public int Id { get; set; }
    public string Icon { get; set; } = "";
    public string Name { get; set; } = "";
    public string Distance { get; set; } = "";
    public int? WikiPlaceId { get; set; }
}

public class CreateNearbyDto
{
    public string Icon { get; set; } = "";
    public string Name { get; set; } = "";
    public int? WikiPlaceId { get; set; }
}

public class AddNearbyToApartmentDto
{
    public int NearbyPlaceId { get; set; }
    public string Distance { get; set; } = "";
}

public class WikiPlaceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string PhotoPath { get; set; } = "";
    public string Type { get; set; } = "";
    public int? NearbyPlaceId { get; set; }

    public List<ApartmentDto> NearbyApartments { get; set; } = [];
}

public class CreateWikiPlaceDto
{
    [Required] public string Name { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    public string PhotoPath { get; set; } = "";
    [Required] public string Type { get; set; } = "other";
    public int? NearbyPlaceId { get; set; }
}

public class UpdateWikiPlaceDto
{
    [Required] public string Name { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    public string PhotoPath { get; set; } = "";
    [Required] public string Type { get; set; } = "other";
    public int? NearbyPlaceId { get; set; }
}
public record SetIdsDto(List<int> Ids);

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

// ── Booking ────────────────────────────────────────────────────────────
public class BookingDto
{
    public int      Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate   { get; set; }
    public string   Status    { get; set; } = "";
}
public class CreateBookingDto
{
    public int      ApartmentId { get; set; }
    public int      ClientId    { get; set; }
    public DateTime StartDate   { get; set; }
    public DateTime EndDate     { get; set; }
}
public class ChangeStatusDto
{
    public string Status { get; set; } = "";
}
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
public class AdminBookingDto
{
    public int      Id             { get; set; }
    public int      ApartmentId    { get; set; }
    public string   ApartmentTitle { get; set; } = "";
    public int      ClientId       { get; set; }
    public string   ClientTag      { get; set; } = "";
    public string   ClientName     { get; set; } = "";
    public string   ClientPhone    { get; set; } = "";
    public DateTime StartDate      { get; set; }
    public DateTime EndDate        { get; set; }
    public decimal  TotalPrice     { get; set; }
    public string   Status         { get; set; } = "";
    public DateTime CreatedAt      { get; set; }
    public int      Nights         => (EndDate - StartDate).Days;
    public string   Source         { get; set; } = "Вручную";
}
public class UserCreateBookingDto
{
    public int      ApartmentId { get; set; }
    public DateTime StartDate   { get; set; }
    public DateTime EndDate     { get; set; }
}
// ── Client ─────────────────────────────────────────────────────────────
public class ClientDto
{
    public int    Id          { get; set; }
    public string FullName    { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email       { get; set; } = "";
    public int?   UserId      { get; set; }
    public string? AdminTag { get; set; }
    public string? AdminNote { get; set; }
    public bool InBlackList { get; set; } = false;
}
public class CreateClientDto
{
    [MaxLength(200)] public string FullName { get; set; } = "";
    [Required, MaxLength(20)] public string PhoneNumber { get; set; } = "";
    [EmailAddress] public string Email { get; set; } = "";
    [MaxLength(200)] public string? AdminTag { get; set; }
    [MaxLength(200)] public string? AdminNote { get; set; }
    public bool InBlackList { get; set; } = false;
}
public class UpdateProfileDto
{
    public string  FullName    { get; set; } = "";
    public string  PhoneNumber { get; set; } = "";
    public string? NewPassword { get; set; }
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
public class ConfirmEmailDto
{
    public string Email { get; set; } = "";
    public string Token { get; set; } = "";
}
public class AddExternalCalendarDto
{
    public string Name { get; set; } = "";
    public string ICalUrl { get; set; } = "";
}

public record ResetPasswordDto(string Email, string Token, string NewPassword);
public record BusyPeriodDto(string StartDate, string EndDate);