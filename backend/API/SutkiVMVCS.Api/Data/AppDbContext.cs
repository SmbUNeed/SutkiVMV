using Microsoft.EntityFrameworkCore;
using SutkiVMV.Models;
using SutkiVMVCS.Api.Services.Security;

namespace SutkiVMV.Data;

public class AppDbContext : DbContext
{
    private readonly FieldEncryptor _encryptor;
    public AppDbContext(DbContextOptions<AppDbContext> options, FieldEncryptor encryptor) : base(options) {
        _encryptor = encryptor;
    }

    public DbSet<Apartment>            Apartments           => Set<Apartment>();
    public DbSet<Booking>              Bookings             => Set<Booking>();
    public DbSet<Amenity>              Amenities            => Set<Amenity>();
    public DbSet<Client>               Clients              => Set<Client>();
    public DbSet<User>                 Users                => Set<User>();
    public DbSet<ApartmentAmenity>     ApartmentAmenities   => Set<ApartmentAmenity>();
    public DbSet<ApartmentGallery>     ApartmentGallery     => Set<ApartmentGallery>();
    public DbSet<PendingRegistration>  PendingRegistrations => Set<PendingRegistration>();
    public DbSet<Rule>                 Rules                => Set<Rule>();
    public DbSet<NearbyPlace>          NearbyPlaces         => Set<NearbyPlace>();
    public DbSet<ApartmentRule>        ApartmentRules       => Set<ApartmentRule>();
    public DbSet<ApartmentNearbyPlace> ApartmentNearby      => Set<ApartmentNearbyPlace>();
    public DbSet<WikiPlace>            WikiPlaces           => Set<WikiPlace>();
    public DbSet<ExternalCalendar>     ExternalCalendars    => Set<ExternalCalendar>();
    public DbSet<ExternalBooking>      ExternalBookings     => Set<ExternalBooking>();
    public DbSet<DeviceToken>          DeviceTokens         => Set<DeviceToken>();
    public DbSet<SiteSettings>         SiteSettings         => Set<SiteSettings>();
    public DbSet<PriceRule>            PriceRules           => Set<PriceRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Составной ключ для join-таблицы ───────────────────────────
        modelBuilder.Entity<ApartmentAmenity>()
            .HasKey(aa => new { aa.ApartmentId, aa.AmenityId });

        // ── User: уникальный email ────────────────────────────────────
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        //modelBuilder.Entity<User>()
        //    .Property(u => u.Email)
        //    .HasConversion(v => _encryptor.Encrypt(v), v => _encryptor.Decrypt(v));

        // ── Client → User (один-к-одному, необязательный) ────────────
        // UserId = null означает гостевое бронирование без регистрации
        modelBuilder.Entity<Client>()
            .HasOne(c => c.User)
            .WithOne()
            .HasForeignKey<Client>(c => c.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        //modelBuilder.Entity<Client>(entity =>
        //{
        //    entity.Property(c => c.FullName)
        //        .HasConversion(v => _encryptor.Encrypt(v), v => _encryptor.Decrypt(v));

        //    entity.Property(c => c.PhoneNumber)
        //        .HasConversion(v => _encryptor.Encrypt(v), v => _encryptor.Decrypt(v));

        //    entity.Property(c => c.Email)
        //        .HasConversion(v => _encryptor.Encrypt(v), v => _encryptor.Decrypt(v));
        //});

        // ── Индексы для частых запросов ───────────────────────────────
        modelBuilder.Entity<Apartment>()
            .HasIndex(a => new { a.IsActive, a.IsDeleted });

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.ApartmentId);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.ClientId);

        modelBuilder.Entity<ApartmentRule>()
            .HasKey(ar => new { ar.ApartmentId, ar.RuleId });

        modelBuilder.Entity<ApartmentNearbyPlace>()
            .HasKey(an => new { an.ApartmentId, an.NearbyPlaceId });

        modelBuilder.Entity<WikiPlace>()
            .HasOne(w => w.NearbyPlace)
            .WithMany()
            .HasForeignKey(w => w.NearbyPlaceId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<NearbyPlace>()
            .HasOne(n => n.WikiPlace)
            .WithMany()
            .HasForeignKey(n => n.WikiPlaceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
