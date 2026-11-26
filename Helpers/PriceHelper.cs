namespace CleanHub.Helpers
{
    public static class PriceHelper
    {
        // Method to calculate price with tax and apply rounding
        public static double CalculatePriceWithTax(double price, float? taxRate)
        {
            // If the taxRate is null, assume no tax (or some default value)
            if (!taxRate.HasValue)
            {
                return Math.Round(price, MidpointRounding.AwayFromZero);
            }

            // Calculate the tax amount
            double taxAmount = price * taxRate.Value / 100.0;  // Tax is a float, but result is double
            // Calculate the total price with tax
            double totalPrice = price + taxAmount;

            // Цел број: 76,6 → 77, 73,43 → 73 (0,5 нагоре)
            return Math.Round(totalPrice, MidpointRounding.AwayFromZero);
        }
    }
}
