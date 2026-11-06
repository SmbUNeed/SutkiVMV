using Microsoft.EntityFrameworkCore;
using SutkiVMV.Models;
using SutkiVMVCS.Api.Services.Security;
using SutkiVMVCS.Services.Security;
using System.Security.Cryptography;

namespace SutkiVMV.Data;

public class AppDbContext : DbContext
{
    private readonly FieldEncryptor _encryptor;
    private readonly TrigramService _trigramService;
    public AppDbContext(DbContextOptions<AppDbContext> options, FieldEncryptor encryptor, TrigramService trigramService) : base(options) {
        _encryptor = encryptor;
        _trigramService = trigramService;
    }

    public DbSet<Apartment>            Apartments           => Set<Apartment>();
    public DbSet<Booking>              Bookings             => Set<Booking>();
    public DbSet<Payment>              Payments             => Set<Payment>();
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
    public DbSet<AppRelease> AppReleases => Set<AppRelease>();

    public static byte[] BitwiseAndBytes(byte[] a, byte[] b)
        => throw new NotSupportedException("Этот метод можно вызывать только внутри LINQ-запросов EF Core.");

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Booking>())
        {
            if (entry.State == EntityState.Added
                && string.IsNullOrWhiteSpace(entry.Entity.PublicToken))
            {
                entry.Entity.PublicToken = GenerateSecureToken();
            }
        }
        UpdateClientTrigrams();
        return await base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        foreach (var entry in ChangeTracker.Entries<Booking>())
        {
            if (entry.State == EntityState.Added
                && string.IsNullOrWhiteSpace(entry.Entity.PublicToken))
            {
                entry.Entity.PublicToken = GenerateSecureToken();
            }
        }
        UpdateClientTrigrams();
        return base.SaveChanges();
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApartmentAmenity>()
            .HasKey(aa => new { aa.ApartmentId, aa.AmenityId });

        modelBuilder.Entity<Client>()
            .HasOne(c => c.User)
            .WithOne(u => u.Client)
            .HasForeignKey<User>(u => u.ClientId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Client>(entity =>
        {
            entity.Property(c => c.FullName)
                .HasConversion(v => _encryptor.Encrypt(v), v => _encryptor.Decrypt(v));

            entity.Property(c => c.PhoneNumber)
                .HasConversion(v => _encryptor.Encrypt(v), v => _encryptor.Decrypt(v));
#pragma warning disable
            entity.Property(c => c.Email)
                .HasConversion(v => _encryptor.Encrypt(v), v => _encryptor.Decrypt(v));
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(b => b.TotalPrice).HasPrecision(12, 2);
            entity.Property(b => b.BasePrice).HasPrecision(12, 2);
            entity.Property(b => b.DiscountAmount).HasPrecision(12, 2);
            entity.Property(b => b.PrepaymentAmount).HasPrecision(12, 2);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(x => x.Source)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.HasIndex(b => b.ClientId);
            entity.HasIndex(b => b.ApartmentId);

            entity.Property(b => b.PublicToken)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.PublicToken);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(p => p.BookingId);
            entity.HasIndex(x => x.PaymentId).IsUnique();
            entity.Property(p => p.Paid).HasPrecision(12, 2);
            entity.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);
            entity.Property(x => x.Type)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(x => x.PaymentId)
                .HasMaxLength(100)
                .IsRequired();
            entity.HasOne(x => x.Booking)
                .WithMany(b => b.Payments)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Apartment>()
            .HasIndex(a => new { a.IsActive, a.IsDeleted });   

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

        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(BitwiseAndBytes))!)
        .HasTranslation(args => new Microsoft.EntityFrameworkCore.Query.SqlExpressions.SqlBinaryExpression(
            System.Linq.Expressions.ExpressionType.And,
            args[0],
            args[1],
            args[0].Type,
            args[0].TypeMapping));
    }

    private void UpdateClientTrigrams()
    {
        var changedClients = ChangeTracker.Entries<Client>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in changedClients)
        {
            var client = entry.Entity;

            if (entry.State == EntityState.Added ||
                entry.Property(c => c.FullName).IsModified ||
                entry.Property(c => c.PhoneNumber).IsModified ||
                entry.Property(c => c.Email).IsModified)
            {
                client.UpdateSearchFilters(_trigramService);
            }
        }
    }
}
