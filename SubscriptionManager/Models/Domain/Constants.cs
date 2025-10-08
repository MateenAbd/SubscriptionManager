using System;

namespace SubscriptionManager.Models.Domain
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Subscriber = "Subscriber";
    }

    public static class SubscriptionStatuses
    {
        public const string Active = "Active";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
    }

    public static class PaymentStatuses
    {
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Refunded = "Refunded";
    }
}