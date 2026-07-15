namespace Xavissa.Frontend.Helpers
{
    public static class PaymentMapper
    {
        public static int MapPaymentMethodToInt(string method) =>
            method?.Trim().ToLowerInvariant() switch
            {
                "cash" => 0,
                "card" => 1,
                "mpesa" => 2,
                "mobile" => 2,
                "mobilepayment" => 2,
                "m-pesa" => 2,
                "other" => 99,
                _ => 0,
            };

        public static string MapPaymentMethodValueToString(int value) =>
            value switch
            {
                0 => "Cash",
                1 => "Card",
                2 => "MobilePayment",
                99 => "Other",
                _ => "Unknown",
            };

        public static int MapStringToPaymentMethodValue(string method) =>
            MapPaymentMethodToInt(method);
    }
}
