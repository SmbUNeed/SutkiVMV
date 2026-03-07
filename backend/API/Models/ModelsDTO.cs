// ╔══════════════════════════════════════════════════════════╗
// ║  ModelsDTO.cs — исправления                              ║
// ║                                                          ║
// ║  БАГ #1: [Precision(18,2)] — это атрибут EF Core,       ║
// ║  он управляет схемой БД. Ставить его на DTO-класс        ║
// ║  бессмысленно и вводит в заблуждение.                    ║
// ║                                                          ║
// ║  БАГ #2: CreateBookingDto — BookingsController           ║
// ║  принимал сырой Booking entity, позволяя клиенту         ║
// ║  самому задать TotalPrice, Status, CreatedAt.            ║
// ╚══════════════════════════════════════════════════════════╝

namespace SutkiVMV.ModelsDTO;

/// DTO для отображения квартиры (публичный сайт + админка)
public class ApartmentDto
{
    public int     Id            { get; set; }
    public string  Title         { get; set; } = "";
    public string  Address       { get; set; } = "";
    public string  Description   { get; set; } = "";
    public int     Guests        { get; set; }
    public decimal PricePerNight { get; set; }
    public int     RoomsCount    { get; set; }
    public decimal Area          { get; set; }   // FIX: убран [Precision(18,2)] — не нужен в DTO
    public bool    IsActive      { get; set; }
    public bool    IsDeleted     { get; set; }
    public string? MainImage     { get; set; }
    public List<GalleryItemDto> Gallery   { get; set; } = [];
    public List<string>         Amenities { get; set; } = [];
    public List<BookingDto>     Bookings  { get; set; } = [];
}

/// Используется в календаре фронтенда для подсветки занятых дат
public class BookingDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate   { get; set; }
    public string   Status    { get; set; } = "";
}

/// Элемент галереи — передаём Id, чтобы фронт мог удалить/сменить главное
public class GalleryItemDto
{
    public int    Id        { get; set; }
    public string ImagePath { get; set; } = "";
    public bool   IsMain    { get; set; }
}

/// FIX: DTO для создания бронирования.
/// Сервер сам вычисляет TotalPrice, Status, CreatedAt — клиент не может их подменить.
public class CreateBookingDto
{
    public int      ApartmentId { get; set; }
    public int      ClientId    { get; set; }
    public DateTime StartDate   { get; set; }
    public DateTime EndDate     { get; set; }
}
