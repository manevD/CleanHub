using CleanHub.Entities;
using CleanHub.ViewModels;

namespace CleanHub.Helpers
{
    public class PriceCalculator(List<Customer> customers)
    {
        public void CalculatePrices(
            List<BuildingProductViewModel> buildingProducts,
            Customer customer)
        {
            foreach (var product in buildingProducts)
            {
                // =========================================================
                // БРОЈ НА СТАНАРИ ЗА ОВОЈ КОНКРЕТЕН ПРОДУКТ
                // =========================================================

                int customerCount = GetCustomerCountForProduct(product);

                // =========================================================
                // ГАРАЖА
                // =========================================================

                if (product.ArticleNotes != null &&
                    product.ArticleNotes.Contains(
                        "гаража",
                        StringComparison.OrdinalIgnoreCase))
                {
                    customerCount = customers.Count(x =>
                        x.Inactive != true &&
                        !x.Hide &&
                        x.Garage);
                }

                // =========================================================
                // БЕЗ СТАНАРИ -> НЕМА СО ШТО ДА СЕ ДЕЛИ
                // =========================================================

                if (customerCount <= 0)
                {
                    customerCount = 1;
                }

                // =========================================================
                // STEP 1:
                // Calculate price per customer if total is provided
                // =========================================================

                if (product.Total.HasValue &&
                    product.Total.Value > 0)
                {
                    float pricePerCustomer =
                        product.Total.Value / customerCount;

                    product.Price = pricePerCustomer;
                }

                // =========================================================
                // STEP 2:
                // Calculate PriceWithTax
                // =========================================================

                // За овие два продукти Tax може да биде 18%,
                // но НЕ се додава во цената.
                //
                // Потрошена електрична енергија
                // Комунална такса за јавно осветлување

                string notes =
                    product.ArticleNotes?.Trim() ?? "";

                bool noTaxCalculation =
                    notes.Contains(
                        "потрошена електрична енергија",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    notes.Contains(
                        "комунална такса за јавно осветлување",
                        StringComparison.OrdinalIgnoreCase);

                if (noTaxCalculation)
                {
                    // Tax може да остане 18 за приказ,
                    // но PriceWithTax останува ист како Price.
                    product.PriceWithTax = product.Price;
                }
                else if (product.Tax.HasValue &&
                         product.Tax.Value > 0)
                {
                    float taxAmount =
                        (product.Price * product.Tax.Value) / 100;

                    product.PriceWithTax =
                        product.Price + taxAmount;
                }
                else
                {
                    product.PriceWithTax =
                        product.Price;
                }
            }
        }

        // =============================================================
        // GET CUSTOMER COUNT FOR SPECIFIC PRODUCT
        // =============================================================

        private int GetCustomerCountForProduct(
            BuildingProductViewModel product)
        {
            string notes =
                product.ArticleNotes?.Trim().ToLowerInvariant() ?? "";

            // =========================================================
            // АДМИНИСТРАТИВНИ ТРОШОЦИ
            // =========================================================

            if (notes.Contains("административни трошоци"))
            {
                return customers.Count(x =>
                    x.Inactive != true &&
                    !x.Hide &&
                    x.PresmetajAdministrativniTrosoci);
            }

            // =========================================================
            // КОМУНАЛНА ТАКСА ЗА ЈАВНО ОСВЕТЛУВАЊЕ
            // =========================================================

            if (notes.Contains(
                "комунална такса за јавно осветлување"))
            {
                return customers.Count(x =>
                    x.Inactive != true &&
                    !x.Hide &&
                    x.PresmetajKomunalnaTaksaJavnoOsvetluvanje);
            }

            // =========================================================
            // ОДРЖУВАЊЕ НА ЛИФТ
            // =========================================================

            if (notes.Contains("одржување на лифт"))
            {
                return customers.Count(x =>
                    x.Inactive != true &&
                    !x.Hide &&
                    x.PresmetajOdrzuvanjeLift);
            }

            // =========================================================
            // ОДРЖУВАЊЕ НА СМЕТКИ
            // =========================================================

            if (notes.Contains("одржување на сметки"))
            {
                return customers.Count(x =>
                    x.Inactive != true &&
                    !x.Hide &&
                    x.PresmetajOdrzuvanjeSmetki);
            }

            // =========================================================
            // ПОТРОШЕНА ЕЛЕКТРИЧНА ЕНЕРГИЈА
            // =========================================================

            if (notes.Contains(
                "потрошена електрична енергија"))
            {
                return customers.Count(x =>
                    x.Inactive != true &&
                    !x.Hide &&
                    x.PresmetajPotrosenaElektricnaEnergija);
            }

            // =========================================================
            // РЕЗЕРВЕН ФОНД
            // =========================================================

            if (notes.Contains("резервен фонд"))
            {
                return customers.Count(x =>
                    x.Inactive != true &&
                    !x.Hide &&
                    x.PresmetajRezervenFond);
            }

            // =========================================================
            // УПРАВИТЕЛ
            // =========================================================

            if (notes.Contains("управител"))
            {
                return customers.Count(x =>
                    x.Inactive != true &&
                    !x.Hide &&
                    x.PresmetajUpravitel);
            }

            // =========================================================
            // ЧИСТЕЊЕ НА ВЛЕЗ
            // =========================================================

            if (notes.Contains("чистење на влез"))
            {
                return customers.Count(x =>
                    x.Inactive != true &&
                    !x.Hide &&
                    x.PresmetajCistenjeVlez);
            }

            // =========================================================
            // СТАНДАРДНИ ПРОДУКТИ
            // =========================================================

            return customers.Count(x =>
                x.Inactive != true &&
                !x.Hide);
        }

        // =============================================================
        // CALCULATE PRICE WITH TAX - DOUBLE
        // =============================================================

        static double CalculatePriceWithTaxDouble(
            double price,
            int taxRate)
        {
            double taxAmount =
                price * taxRate / 100.0;

            double totalPrice =
                price + taxAmount;

            if (totalPrice % 1 <= 0.5)
            {
                return Math.Floor(totalPrice);
            }
            else
            {
                return Math.Ceiling(totalPrice);
            }
        }

        // =============================================================
        // CALCULATE TOTAL PRICE WITH TAX SUM
        // =============================================================

        public float CalculateTotalPriceWithTaxSum(
            List<BuildingProductViewModel> buildingProducts)
        {
            float totalSum =
                buildingProducts.Sum(product =>
                    product.PriceWithTaxTotal.Value);

            return (float)Math.Round(totalSum);
        }
    }
}