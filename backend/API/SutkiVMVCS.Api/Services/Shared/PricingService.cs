using SutkiVMV.Models;
using SutkiVMV.ModelsDTO;

namespace SutkiVMVCS.Services.Shared
{
    public static class PricingService
    {
        public static decimal CalculateTotal(
            DateTime startDate, DateTime endDate, int guests, Apartment apartment, List<PriceRule> rules)
        {
            decimal additional = 0;
            DateTime sd = startDate;
            DateTime ed = endDate;

            int nights = (ed.Date - sd.Date).Days;

            int overguest = guests - apartment.Guests;

            if (overguest > 0) additional += overguest * (apartment.GuestOverpayment > 0 ? apartment.GuestOverpayment : 700) * nights;

            DateOnly rangeStart = DateOnly.FromDateTime(sd.Date);

            decimal nightsTotal = GetTotalNightsPrice(apartment.Id, apartment.PricePerNight, rangeStart, nights, rules);

            return nightsTotal + additional;
        }

        private static decimal GetTotalNightsPrice(
            int apartmentId, decimal basePrice, DateOnly checkIn, int nights, List<PriceRule> rules)
        {
            decimal total = 0;
            for (int i = 0; i < nights; i++)
                total += GetNightlyPrice(apartmentId, basePrice, checkIn.AddDays(i), rules);
            return total;
        }

        private static decimal GetNightlyPrice(int apartmentId, decimal basePrice, DateOnly date, List<PriceRule> rules)
        {
            var match = rules
                .Where(r => r.ApartmentId == null || r.ApartmentId == apartmentId)
                .FirstOrDefault(r => Matches(r, date));

            if (match == null) return basePrice;
            if (match.Multiplier != null) return basePrice * match.Multiplier.Value;
            if (match.FixedMarkup != null) return basePrice + match.FixedMarkup.Value;
            return basePrice;
        }

        private static bool Matches(PriceRule r, DateOnly date)
        {
            var end = r.EndDate ?? r.StartDate;
            if (!r.IsRecurringYearly)
                return date >= r.StartDate && date <= end;

            bool wraps = (end.Month, end.Day).CompareTo((r.StartDate.Month, r.StartDate.Day)) < 0;
            for (int y = date.Year - 1; y <= date.Year; y++)
            {
                var s = Safe(y, r.StartDate.Month, r.StartDate.Day);
                var e = Safe(wraps ? y + 1 : y, end.Month, end.Day);
                if (date >= s && date <= e) return true;
            }
            return false;
        }

        private static DateOnly Safe(int y, int m, int d) =>
            new(y, m, Math.Min(d, DateTime.DaysInMonth(y, m)));
    }
}
