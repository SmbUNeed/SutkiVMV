using Microsoft.EntityFrameworkCore;
using SutkiVMV.Models;

namespace SutkiVMV.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Apartment>           Apartments           => Set<Apartment>();
    public DbSet<Booking>             Bookings             => Set<Booking>();
    public DbSet<Amenity>             Amenities            => Set<Amenity>();
    public DbSet<Client>              Clients              => Set<Client>();
    public DbSet<User>                Users                => Set<User>();
    public DbSet<ApartmentAmenity>    ApartmentAmenities   => Set<ApartmentAmenity>();
    public DbSet<ApartmentGallery>    ApartmentGallery     => Set<ApartmentGallery>();
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Составной ключ для join-таблицы ───────────────────────────
        modelBuilder.Entity<ApartmentAmenity>()
            .HasKey(aa => new { aa.ApartmentId, aa.AmenityId });

        // ── User: уникальный email ────────────────────────────────────
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // ── Client → User (один-к-одному, необязательный) ────────────
        // UserId = null означает гостевое бронирование без регистрации
        modelBuilder.Entity<Client>()
            .HasOne(c => c.User)
            .WithOne()
            .HasForeignKey<Client>(c => c.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Индексы для частых запросов ───────────────────────────────
        modelBuilder.Entity<Apartment>()
            .HasIndex(a => new { a.IsActive, a.IsDeleted });

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.ApartmentId);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.ClientId);
    }
}
