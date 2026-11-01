namespace SutkiVMVCS.Data
{
    public enum BookingStatus {
        Cancelled,
        Pending,
        Confirmed,
        AwaitingPayment
    }

    public enum PaymentStatus {
        Canceled,
        Pending,
        Succeeded,
        Refunded
    }

    public enum PaymentType {
        Full,
        Prepayment,
        Balance,
        Refund
    }

    public enum BookingSource {
        SiteGuest,
        SiteUser,
        AdminManual
    }
}
