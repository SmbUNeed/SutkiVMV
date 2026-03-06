using Microsoft.EntityFrameworkCore;
using SutkiVMV.Models;

namespace SutkiVMV.ModelsDTO;

public class ApartmentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public int Guests { get; set; }
    public decimal PricePerNight { get; set; }
    public int RoomsCount { get; set; }
    [Precision(18,2)]
    public decimal Area { get; set; }
    public string? MainImage { get; set; }
    public List<string> Gallery { get; set; } = [];
    public List<string> Amenities { get; set; } = [];
    public List<BookingDto> Bookings { get; set; } = [];
}

public class BookingDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "";
}