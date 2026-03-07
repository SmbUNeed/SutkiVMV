using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SutkiVMV.Models;

public class Apartment
{
    public int     Id            { get; set; }
    public string  Title         { get; set; } = "";
    public string  Address       { get; set; } = "";
    public string  Description   { get; set; } = "";
    public int     Guests        { get; set; }
    public decimal PricePerNight { get; set; }
    public int     RoomsCount    { get; set; }
    public bool    IsActive      { get; set; }
    public decimal Area          { get; set; }
    public bool    IsDeleted     { get; set; }

    public ICollection<Booking>           Bookings           { get; set; } = [];
    public ICollection<ApartmentAmenity> ApartmentAmenities { get; set; } = [];
    public ICollection<ApartmentGallery>  ApartmentGallery   { get; set; } = [];

    [NotMapped]
    public ApartmentGallery? MainImage => ApartmentGallery?.FirstOrDefault(g => g.IsMain);
}

public class Booking
{
    public int      Id          { get; set; }
    public int      ApartmentId { get; set; }
    public int      ClientId    { get; set; }
    public DateTime StartDate   { get; set; }
    public DateTime EndDate     { get; set; }

    [Precision(18, 2)]
    public decimal  TotalPrice  { get; set; }

    public string   Status    { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public Apartment Apartment { get; set; } = null!;
    public Client    Client    { get; set; } = null!;
}

public class Amenity
{
    public int    Id   { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "fa-check";   // Font Awesome class
}

public class Client
{
    public int    Id          { get; set; }
    public string FullName    { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email       { get; set; } = "";

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
    public int      Id           { get; set; }
    public string   Email        { get; set; } = "";
    public string   PasswordHash { get; set; } = "";
    public string   Role         { get; set; } = "User";   // "User" | "Admin"
    public DateTime CreatedAt    { get; set; }
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
