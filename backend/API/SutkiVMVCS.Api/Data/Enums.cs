namespace SutkiVMVCS.Data
{
    public enum BookingStatus {
        Canceled,
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
        Cash,
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
