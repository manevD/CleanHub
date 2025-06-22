using CleanHub.Entities;
using CleanHub.ViewModels;

namespace CleanHub.Helpers
{
    public class PriceCalculator(int customerCount, int customerGarage)
    {
        public void CalculatePrices(List<BuildingProductViewModel> buildingProducts, Customer customer)
        {
            foreach (var product in buildingProducts)
            {
                // Step 1: Calculate price per customer if total is provided
                if (product.Total.HasValue && product.Total.Value > 0) // Null check for Total
                {
                    float pricePerCustomer = product.Total.Value / customerCount;

                    if (product.ArticleNotes != null && product.ArticleNotes.Contains("гаража")) // Null check for ArticleNotes
                    {
                        pricePerCustomer = product.Total.Value / customerGarage;
                    }

                    product.Price = pricePerCustomer; // Direct assignment without rounding
                }

                // Step 2: Calculate PriceWithTax based on the new Price and Tax
                if (product.Tax.HasValue && product.Tax.Value > 0) // Null check for Tax
                {
                    // Calculate tax amount and add it to the price
                    float taxAmount = (product.Price * product.Tax.Value) / 100;
                    product.PriceWithTax = product.Price + taxAmount; // Direct calculation without rounding
                }
                else
                {
                    // If no tax, set PriceWithTax equal to Price
                    product.PriceWithTax = product.Price;
                }
            }
        }

        static double CalculatePriceWithTaxDouble(double price, int taxRate)
        {
            // Calculate the tax amount
            double taxAmount = price * taxRate / 100.0;  // Tax is an int, but the result is double
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

        public float CalculateTotalPriceWithTaxSum(List<BuildingProductViewModel> buildingProducts)
        {
            // Sum all PriceWithTax values and round up/down if needed
            float totalSum = buildingProducts.Sum(product => product.PriceWithTaxTotal.Value);

            // Round the sum to an integer (up if >= .5, down otherwise)
            return (float)Math.Round(totalSum);
        }
    }
}
