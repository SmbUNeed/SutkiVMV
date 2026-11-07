using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using SutkiVMV.Models;
using SutkiVMVCS.Api.Services.Security;
using SutkiVMVCS.Services.Security;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;

namespace SutkiVMV.Data;

public class AppDbContext : DbContext
{
    private readonly FieldEncryptor _encryptor;
    private readonly TrigramService _ts;
    public AppDbContext(DbContextOptions<AppDbContext> options, FieldEncryptor encryptor, TrigramService ts) : base(options) {
        _encryptor = encryptor;
        _ts = ts;
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
        return base.SaveChanges();
    }

    private static string GenerateSecureToken()
    {
        // 32 байта = 64 hex-символа, криптографически стойкий
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();

        // Альтернатива — URL-safe base64 (короче, 43 символа):
        // return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        //     .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Составной ключ для join-таблицы ───────────────────────────
        modelBuilder.Entity<ApartmentAmenity>()
            .HasKey(aa => new { aa.ApartmentId, aa.AmenityId });

        // ── User: уникальный email ────────────────────────────────────
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasConversion(v => _encryptor.Encrypt(v), v => _encryptor.Decrypt(v));

        //── Client → User(один - к - одному, необязательный) ────────────
        //UserId = null означает гостевое бронирование без регистрации
        modelBuilder.Entity<Client>()
            .HasOne(c => c.User)
            .WithOne()
            .HasForeignKey<Client>(c => c.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

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

        // ── Индексы для частых запросов ───────────────────────────────
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

        var methodInfo = typeof(AppDbContext).GetMethod(nameof(AppDbContext.BitwiseAndBytes))!;
        modelBuilder.HasDbFunction(methodInfo)
            .HasTranslation(args => new SqlFunctionExpression(
                functionName: "bytea_bitwise_and",
                arguments: args,
                nullable: true,
                argumentsPropagateNullability: new[] { true, true },
                type: typeof(byte[]),
                typeMapping: args[0].TypeMapping // Сохраняем маппинг типа bytea
            ));
    }

    public async Task<List<Client>> FilterClientsByTrigramsAsync(
        Expression<Func<Client, byte[]>> trigramSelector,
        Expression<Func<Client, string?>> exactSelector,
        string filterString)
    {
        string exactParameterName = GetPropertyName(exactSelector);

        var predicate = GetClientTrigramExpression(
            trigramSelector,
            filterString
        );

        var potential = await Clients.Where(predicate).ToListAsync();

        string cleanFilter = filterString.Replace(" ", "").Replace("+", "");

        var parameter = Expression.Parameter(typeof(Client), "c");
        var propertyExpression = Expression.Property(parameter, exactParameterName);

        var notNullCheck = Expression.NotEqual(propertyExpression, Expression.Constant(null, typeof(string)));

        MethodInfo replaceMethod = typeof(string).GetMethod(
            nameof(string.Replace),
            new[] { typeof(string), typeof(string) }
        )!;

        MethodInfo containsMethod = typeof(string).GetMethod(
            nameof(string.Contains),
            new[] { typeof(string), typeof(StringComparison) }
        )!;

        var withoutSpaces = Expression.Call(
            propertyExpression,
            replaceMethod,
            Expression.Constant(" "),
            Expression.Constant("")
        );

        var cleanedProperty = Expression.Call(
            withoutSpaces,
            replaceMethod,
            Expression.Constant("+"),
            Expression.Constant("")
        );

        var containsCall = Expression.Call(
            cleanedProperty,
            containsMethod,
            Expression.Constant(cleanFilter, typeof(string)),
            Expression.Constant(StringComparison.OrdinalIgnoreCase)
        );

        var finalBody = Expression.AndAlso(notNullCheck, containsCall);
        var finalPredicate = Expression.Lambda<Func<Client, bool>>(finalBody, parameter).Compile();

        return potential.Where(finalPredicate).ToList();
    }

    private Expression<Func<Client, bool>> GetClientTrigramExpression(
        Expression<Func<Client, byte[]>> trigramSelector,
        string filterString) 
    {
        string trigramParameterName = GetPropertyName(trigramSelector);

        var filter = _ts.GenerateBloomFilter(filterString);
        var bitwiseAndMethod = typeof(AppDbContext).GetMethod(nameof(AppDbContext.BitwiseAndBytes))!;

        var parameter = Expression.Parameter(typeof(Client), "c");

        var clientExpression = Expression.Property(parameter, trigramParameterName);
        var bloomExpression = Expression.Constant(filter);

        var bitwiseExpression = Expression.Call(
            bitwiseAndMethod,
            bloomExpression,
            clientExpression
        );

        var resultExpression = Expression.Equal(bitwiseExpression, bloomExpression);

        return Expression.Lambda<Func<Client, bool>>(resultExpression, parameter);
    }

    private string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty>> selector) {
        var body = selector.Body;

        if (body is UnaryExpression unaryExpression) {
            body = unaryExpression.Operand;
        }

        if (body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        throw new ArgumentException("Выражение должно указывать на свойство", nameof(selector));
    }

    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _targetParameter;

        public ParameterReplacer(ParameterExpression targetParameter)
        {
            _targetParameter = targetParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return _targetParameter;
        }
    }

    public static byte[] BitwiseAndBytes(byte[] a, byte[] b)
        => throw new NotSupportedException("Этот метод предназначен только для трансляции в SQL.");
}
