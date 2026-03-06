using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SutkiVMV.Data;
using SutkiVMV.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace SutkiVMV.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BookingsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPatch("{id:int}/cancel")]
        public async Task<ActionResult> Cancel(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            booking.Status = "Cancelled";
            await _context.SaveChangesAsync();
            return NoContent();
        }

        //[Authorize(Roles = "User")]
        [HttpPost]
        public async Task<ActionResult<Booking>> Create(Booking booking)
        {
            bool overlap = await _context.Bookings.AnyAsync(b =>
                b.ApartmentId == booking.ApartmentId &&
                b.Status != "Cancelled" &&
                b.StartDate < booking.EndDate &&
                b.EndDate > booking.StartDate);

            if (overlap)
                return Conflict(new { message = "Квартира занята в эти даты" });

            var apartment = await _context.Apartments.FindAsync(booking.ApartmentId);
            if (apartment == null) return NotFound(new { message = "Квартира не найдена" });

            var nights = (booking.EndDate - booking.StartDate).Days;
            booking.TotalPrice = apartment.PricePerNight * nights;
            booking.CreatedAt = DateTime.UtcNow;
            booking.Status = "Pending";

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            return Ok(booking);
        }

        [HttpGet("apartment/{apartmentId:int}")]
        public async Task<ActionResult<IEnumerable<Booking>>> GetByApartment(int apartmentId)
        => Ok(await _context.Bookings
            .Where(b => b.ApartmentId == apartmentId)
            .OrderBy(b => b.StartDate)
            .ToListAsync());

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Booking>>> GetAll()
        => Ok(await _context.Bookings.OrderByDescending(b => b.StartDate).ToListAsync());

        [HttpGet("client/{clientId:int}")]
        public async Task<ActionResult<IEnumerable<Booking>>> GetByClient(int clientId)
        => Ok(await _context.Bookings
            .Where(b => b.ClientId == clientId)
            .ToListAsync());
    }
}
