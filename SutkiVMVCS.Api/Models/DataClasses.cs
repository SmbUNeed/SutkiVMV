namespace SutkiVMV.Models;
public class Apartment
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal PricePerNight { get; set; }
    public int RoomsCount { get; set; }
    public bool IsActive { get; set; }
    public decimal Area { get; set; }
    public bool IsDeleted { get; set; }
}

public class Booking
{
    public int Id { get; set; }
    public int ApartmentId { get; set; }
    public int ClientId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "";
    public DateTime CrreatedAt { get; set; }
}

public class Amenitie
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class Client
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
}

public class ApartmentAmenitie
{
    public int ApartmentId { get; set; }
    public int AmenityId { get; set; }
}

public class ApartmentGallery
{
    public int Id { get; set; }
    public int ApartmentId { get; set; }
    public string ImagePath { get; set; } = "";
    public bool IsMain {  get; set; }
}