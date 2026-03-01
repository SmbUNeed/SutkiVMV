using Microsoft.EntityFrameworkCore;
using SutkiVMV.Models;

namespace SutkiVMV.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Apartment> Apartments => Set<Apartment>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Amenitie> Amenities => Set<Amenitie>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ApartmentAmenitie> ApartmentsAmenities => Set<ApartmentAmenitie>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Говорим базе: "Ключ состоит из двух колонок"
        modelBuilder.Entity<ApartmentAmenitie>()
            .HasKey(aa => new { aa.ApartmentId, aa.AmenityId });
    }
}