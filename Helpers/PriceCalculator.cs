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
                if (product.Total > 0)
                {
                    float pricePerCustomer = product.Total.Value / customerCount;

                    if (product.ArticleNotes.Contains("гаража"))
                    {
                        pricePerCustomer = product.Total.Value / customerGarage;
                    }
                    product.Price = (float)Math.Round(pricePerCustomer, 2); // Round to 2 decimal places
                }

                // Step 2: Calculate PriceWithTax based on the new Price and Tax
                if (product.Tax == 0)
                {
                    // If tax is 0, set PriceWithTax to Price
                    product.PriceWithTax = product.Price;
                }
                else
                {
                    // Calculate tax amount and add it to the price
                    float taxAmount = (product.Price * product.Tax.Value) / 100;
                    product.PriceWithTax = (float)Math.Round(product.Price + taxAmount, 2); // Price + VAT, rounded
                }
            }
        }

        public float CalculateTotalPriceWithTaxSum(List<BuildingProductViewModel> buildingProducts)
        {
            // Sum all PriceWithTax values and round up/down if needed
            float totalSum = buildingProducts.Sum(product => product.PriceWithTax.Value);

            // Round the sum to an integer (up if >= .5, down otherwise)
            return (float)Math.Round(totalSum);
        }
    }
}
