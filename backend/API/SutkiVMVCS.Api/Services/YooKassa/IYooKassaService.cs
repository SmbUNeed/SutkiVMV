using SutkiVMV.Models;

namespace SutkiVMVCS.Services.YooKassa
{
    public interface IYooKassaService
    {
        Task<YooKassaPaymentResult> CreatePaymentAsync(Booking booking, string description, CancellationToken ct);
        Task<YooKassaPaymentStatus> GetPaymentStatusAsync(string paymentId, CancellationToken ct);
    }

    public record YooKassaPaymentResult(bool Success, string? ConfirmationUrl, string? PaymentId, string? Error);

    public record YooKassaPaymentStatus(bool Success, string? Status, int? BookingId, string? Error);
}
