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
    public DbSet<ApartmentAmenitie> ApartmentAmenities => Set<ApartmentAmenitie>();
    public DbSet<ApartmentGallery> ApartmentGallery => Set<ApartmentGallery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApartmentAmenitie>()
            .HasKey(aa => new { aa.ApartmentId, aa.AmenitieId }); // Создает составной ключ

        // Индексы
        modelBuilder.Entity<Apartment>()
            .HasIndex(a => new { a.IsActive, a.IsDeleted }); // главный фильтр в GetDetailed

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.ApartmentId);
    }
}