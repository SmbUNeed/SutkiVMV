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

            if (overguest > 0) additional += overguest * (apartment.GuestOverpayment > 0 ? apartment.GuestOverpayment : 700);

            DateOnly rangeStart = sd.Hour <= 7
                ? DateOnly.FromDateTime(sd.Date).AddDays(-1)
                : DateOnly.FromDateTime(sd.Date);

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

            var start = new DateOnly(date.Year, r.StartDate.Month, r.StartDate.Day);
            var endThisYear = new DateOnly(date.Year, end.Month, end.Day);
            if (endThisYear < start) endThisYear = endThisYear.AddYears(1);
            return date >= start && date <= endThisYear;
        }

        public static decimal CalculatePrepayment(DateTime start, int guests, Apartment apt,
            List<PriceRule> rules, decimal basePrice, decimal total)
        {
            var firstNight = CalculateTotal(start, start.AddDays(1), guests, apt, rules);
            return basePrice == 0 ? 0 : Math.Round(firstNight * total / basePrice, 2);
        }
    }
}
