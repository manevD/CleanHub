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
                return price;  // No tax applied, just return the price as is
            }

            // Calculate the tax amount
            double taxAmount = price * taxRate.Value / 100.0;  // Tax is a float, but result is double
            // Calculate the total price with tax
            double totalPrice = price + taxAmount;

            // Round according to the condition
            if (totalPrice % 1 <= 0.5)
            {
                return Math.Floor(totalPrice);  // Round down
            }
            else
            {
                return Math.Ceiling(totalPrice);  // Round up
            }
        }
    }
}
