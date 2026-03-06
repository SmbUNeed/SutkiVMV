namespace SutkiVMV.ModelsDTO;

// DTO для отображения квартиры с деталями (используется на фронте)
public class ApartmentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public int Guests { get; set; }
    public decimal PricePerNight { get; set; }
    public int RoomsCount { get; set; }
    public decimal Area { get; set; }
    public string? MainImage { get; set; }
    public List<string> Gallery { get; set; } = [];
    public List<string> Amenities { get; set; } = [];
    public List<BookingDto> Bookings { get; set; } = [];
}

// DTO для отображения бронирования (для занятых дат в календаре)
public class BookingDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "";
}

// DTO для создания бронирования (принимается от клиента)
public class CreateBookingDto
{
    public int ApartmentId { get; set; }
    public int ClientId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
