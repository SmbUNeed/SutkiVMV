using LinqKit;
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

    public async Task<List<Client>> FilterClientsByQuery(string query)
    {
        if (query.Length < 3) return new List<Client>();

        byte[] filter = _ts.GenerateBloomFilter(query);

        var parameter = Expression.Parameter(typeof(Client), "c");

        var byFullName = GetClientTrigramExpression(c => c.FullNameTrigram, filter);
        var byPhone = GetClientTrigramExpression(c => c.PhoneNumberTrigram, filter);
        var byEmail = GetClientTrigramExpression(c => c.EmailTrigram, filter);
        var byAdminTag = GetIgnoreCaseContainsExpression(c => c.AdminTag, query);

        var predicate = PredicateBuilder.New<Client>();
        predicate = predicate.Or(byFullName);
        predicate = predicate.Or(byPhone);
        predicate = predicate.Or(byEmail);
        predicate = predicate.Or(byAdminTag);

        var potential = await Clients
            .AsExpandable()
            .Where((Expression<Func<Client, bool>>)predicate.Expand())
            .ToListAsync();

        return FilterClientsInMemory(potential, query,
            c => c.FullName, c => c.PhoneNumber, c => c.Email);
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

        return FilterClientsInMemory(potential, filterString, exactSelector);
    }

    private List<Client> FilterClientsInMemory(
    List<Client> clients,
    string filterString,
    params Expression<Func<Client, string?>>[] propertySelectors)
    {
        string cleanFilter = filterString.Replace(" ", "").Replace("+", "");

        var compiledSelectors = propertySelectors
            .Select(selector => selector.Compile())
            .ToList();

        return clients.Where(client =>
        {
            foreach (var selector in compiledSelectors)
            {
                var value = selector(client);
                if (value == null) continue;

                var cleanedValue = value.Replace(" ", "").Replace("+", "");
                if (cleanedValue.Contains(cleanFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }).ToList();
    }

    private static Expression<Func<Client, bool>> GetIgnoreCaseContainsExpression(
    Expression<Func<Client, string?>> selector,
    string filterString)
    {
        var propertyName = GetPropertyName(selector);
        return GetIgnoreCaseContainsExpression(propertyName, filterString);
    }

    private static Expression<Func<Client, bool>> GetIgnoreCaseContainsExpression(string propertyName, string filterString)
    {
        var parameter = Expression.Parameter(typeof(Client), "c");
        var property = Expression.Property(parameter, propertyName);

        var pattern = Expression.Constant($"%{filterString}%", typeof(string));

        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));

        var iLikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            new[] { typeof(DbFunctions), typeof(string), typeof(string) }
        )!;

        var iLikeCall = Expression.Call(iLikeMethod, efFunctions, property, pattern);

        return Expression.Lambda<Func<Client, bool>>(iLikeCall, parameter);
    }

    private Expression<Func<Client, bool>> GetClientTrigramExpression(
        Expression<Func<Client, byte[]>> trigramSelector,
        string filterString
    ) {
        var filter = _ts.GenerateBloomFilter(filterString);
        return GetClientTrigramExpression(trigramSelector, filter);
    }

    private Expression<Func<Client, bool>> GetClientTrigramExpression(
        Expression<Func<Client, byte[]>> trigramSelector,
        byte[] filter) 
    {
        string trigramParameterName = GetPropertyName(trigramSelector);

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

    private static string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty>> selector) {
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

    private class ParameterReplacer(ParameterExpression oldParam, ParameterExpression newParam) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == oldParam ? newParam : base.VisitParameter(node);
    }

    public static byte[] BitwiseAndBytes(byte[] a, byte[] b)
        => throw new NotSupportedException("Этот метод предназначен только для трансляции в SQL.");
}
