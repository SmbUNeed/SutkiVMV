using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SutkiVMV.Data;
using SutkiVMV.Models;

namespace SutkiVMVCS.Services.Shared
{
    public class SettingsService
    {
        private readonly AppDbContext _ctx;
        private readonly IMemoryCache _cache;

        public SettingsService(AppDbContext ctx, IMemoryCache cache)
        {
            _ctx = ctx;
            _cache = cache;
            Init();
        }

        private void Init()
        {
            if (!_ctx.SiteSettings.Any())
            {
                _ctx.SiteSettings.Add(new SiteSettings());
                _ctx.SaveChanges();
            }
        }

        public async Task<SiteSettings> GetAsync()
        {
            return (await _cache.GetOrCreateAsync<SiteSettings?>("site_settings", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await _ctx.SiteSettings.FirstAsync();
            }))!;
        }

        public async Task UpdateAsync(SiteSettings updated)
        {
            var s = await _ctx.SiteSettings.FirstAsync();
            s.HeaderDescription = updated.HeaderDescription;
            s.MinBookingDaysAhead = updated.MinBookingDaysAhead;
            s.MaxBookingRangeDays = updated.MaxBookingRangeDays;
            s.DefaultHourOverpayment = updated.DefaultHourOverpayment;
            await _ctx.SaveChangesAsync();
            _cache.Remove("site_settings");
        }
    }
}
