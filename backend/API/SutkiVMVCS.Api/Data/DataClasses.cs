using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SutkiVMV.Models;

public class Apartment
{
    public int     Id            { get; set; }
    public string  AdminTag      { get; set; } = "";
    public string  Title         { get; set; } = "";
    public string  Address       { get; set; } = "";
    public string  Description   { get; set; } = "";
    public int     Guests        { get; set; }
    public decimal GuestOverpayment { get; set; }
    public int     Kids          { get; set; } = 0;
    public decimal KidOverpayment  { get; set; } = 0;
    public decimal PricePerNight { get; set; }
    public decimal HourOverpayment { get; set; } = 100;
    public int     RoomsCount    { get; set; }
    public decimal Area          { get; set; }
    public bool    IsActive      { get; set; }
    public bool    IsDeleted     { get; set; }

    public ICollection<Booking>           Bookings           { get; set; } = [];
    public ICollection<ApartmentAmenity> ApartmentAmenities { get; set; } = [];
    public ICollection<ApartmentGallery>  ApartmentGallery   { get; set; } = [];
    public ICollection<ApartmentRule> Rules { get; set; } = new List<ApartmentRule>();
    public ICollection<ApartmentNearbyPlace> Nearby { get; set; } = new List<ApartmentNearbyPlace>();

    [NotMapped]
    public ApartmentGallery? MainImage => ApartmentGallery?.FirstOrDefault(g => g.IsMain);
}

// Глобальное правило
public class Rule
{
    public int Id { get; set; }
    public string Icon { get; set; } = "";
    public string Text { get; set; } = "";

    public ICollection<ApartmentRule> Apartments { get; set; } = new List<ApartmentRule>();
}
public class Booking
{
    public int      Id          { get; set; }
    public int      ApartmentId { get; set; }
    public int      ClientId    { get; set; }
    public int Kids { get; set; } = 0;
    public int Guests { get; set; } = 0;
    public DateTime StartDate   { get; set; }
    public DateTime EndDate     { get; set; }

    [Precision(18, 2)]
    public decimal  TotalPrice  { get; set; }

    public string   Status    { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public Apartment Apartment { get; set; } = null!;
    public Client    Client    { get; set; } = null!;
    public string    Source    { get; set; } = null!;

    public string? PaymentId { get; set; }
    public DateTime? PaidAt { get; set; }
}

// Глобальное место рядом
public class NearbyPlace
{
    public int Id { get; set; }
    public string Icon { get; set; } = "";
    public string Name { get; set; } = "";
    public int? WikiPlaceId { get; set; }
    public WikiPlace? WikiPlace { get; set; }
    public ICollection<ApartmentNearbyPlace> Apartments { get; set; } = [];
}

// Связующие
public class ApartmentRule
{
    public int ApartmentId { get; set; }
    public int RuleId { get; set; }

    public Apartment Apartment { get; set; } = null!;
    public Rule Rule { get; set; } = null!;
}

public class ApartmentNearbyPlace
{
    public int ApartmentId { get; set; }
    public int NearbyPlaceId { get; set; }
    public string Distance { get; set; } = "";

    public Apartment Apartment { get; set; } = null!;
    public NearbyPlace NearbyPlace { get; set; } = null!;
}


public class Amenity
{
    public int    Id   { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "fa-check";   // Font Awesome class
}

public class Client
{
    public int     Id          { get; set; }
    public string  FullName    { get; set; } = "";
    public string  PhoneNumber { get; set; } = "";
    public string?  Email       { get; set; } = "";
    public string? AdminTag    { get; set; } = null;
    public string? AdminNote { get; set; } = null;
    public bool InBlackList { get; set; } = false;

    // null = гостевое бронирование без регистрации
    public int?  UserId { get; set; }
    public User? User   { get; set; }

    public ICollection<Booking> Bookings { get; set; } = [];
}

/// <summary>
/// Учётная запись для входа.
/// Разделена с Client намеренно:
///   Client = контактные данные для бронирования,
///   User   = credentials для аутентификации.
/// Связь через Client.UserId (один-к-одному, необязательная).
/// </summary>
public class User
{
    public int      Id                    { get; set; }
    public string   Email                 { get; set; } = "";
    public string   PasswordHash          { get; set; } = "";
    public string   Role                  { get; set; } = "User";
    public DateTime CreatedAt             { get; set; }
    public bool     EmailConfirmed        { get; set; } = false;
    public string?  EmailConfirmToken     { get; set; }
    public DateTime? EmailConfirmTokenExp { get; set; }
    public bool      IsBanned             { get; set; } = false;
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }
}

public class ApartmentAmenity
{
    public int       ApartmentId { get; set; }
    public Apartment Apartment   { get; set; } = null!;
    public int       AmenityId  { get; set; }
    public Amenity  Amenity    { get; set; } = null!;
}

public class ApartmentGallery
{
    public int       Id          { get; set; }
    public int       ApartmentId { get; set; }
    public string    ImagePath   { get; set; } = "";
    public bool      IsMain      { get; set; }
    public Apartment Apartment   { get; set; } = null!;
}

public class PendingRegistration
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class WikiPlace
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string PhotoPath { get; set; } = "";
    public WikiPlaceType Type { get; set; }
    public int? NearbyPlaceId { get; set; }
    public NearbyPlace? NearbyPlace { get; set; }
}

public enum WikiPlaceType { Sight=0, Nature=1, Resort=2, Food=3, Shopping=4, Transport=5, Other=6 }

public class ExternalCalendar
{
    public int Id { get; set; }
    public int ApartmentId { get; set; }
    public string Name { get; set; } = "";
    public string ICalUrl { get; set; } = "";
    public DateTime? LastSync { get; set; }

    public Apartment Apartment { get; set; } = null!;
}

public class ExternalBooking
{
    public int Id { get; set; }
    public int ApartmentId { get; set; }
    public string ExternalId { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Apartment Apartment { get; set; } = null!;
}

public class DeviceToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SiteSettings
{
    public int Id { get; set; } = 1;
    public string HeaderDescription { get; set; } = "";
    public int MinBookingDaysAhead { get; set; } = 0;
    public int MaxBookingRangeDays { get; set; } = 365;
    public decimal DefaultHourOverpayment { get; set; } = 0;
}

public class PriceRule
{
    public int Id { get; set; }
    public int? ApartmentId { get; set; } // null = на все квартиры
    public string Label { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; } // null = одна дата
    public bool IsRecurringYearly { get; set; }
    public decimal? Multiplier { get; set; } // 2.0 = цена x2
    public decimal? FixedMarkup { get; set; } // добавка к цене за ночь
}