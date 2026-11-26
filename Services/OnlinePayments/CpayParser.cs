using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CleanHub.Services.OnlinePayments
{
    /// <summary>
    /// Чита orderInfo.xml од cPay. Пример за OrderID:
    /// "Faktura: 15995-2026, suma: 964, imePrezime: Име Презиме ст.28, adresa: Мајка Тереза бр.13/1-28, usluga: 50"
    /// </summary>
    public static partial class CpayParser
    {
        [GeneratedRegex(@"Faktura:?\s*(\d+)\s*-\s*(\d{4})", RegexOptions.IgnoreCase)]
        private static partial Regex InvoiceRx();

        [GeneratedRegex(@"suma:\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
        private static partial Regex SumaRx();

        [GeneratedRegex(@"usluga:\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
        private static partial Regex FeeRx();

        [GeneratedRegex(@"imePrezime:\s*(.*?)\s*,\s*adresa:", RegexOptions.IgnoreCase)]
        private static partial Regex NameRx();

        [GeneratedRegex(@"adresa:\s*(.*?)\s*(?:,\s*usluga:|$)", RegexOptions.IgnoreCase)]
        private static partial Regex AddressRx();

        /// <summary>Враќа null ако XML-от не е успешна уплата или не може да се прочита.</summary>
        public static OnlinePayment? Parse(string xml, DateTimeOffset receivedAt, string? messageId)
        {
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch { return null; }

            var root = doc.Root;
            if (root == null) return null;

            string? Val(params string[] path)
            {
                XElement? el = root;
                foreach (var name in path)
                {
                    el = el?.Elements().FirstOrDefault(e => e.Name.LocalName == name);
                    if (el == null) return null;
                }
                return el?.Value?.Trim();
            }

            // Само успешни уплати (StatusCode 0)
            var statusCode = Val("OrderStatus", "StatusCode");
            if (statusCode != null && statusCode != "0") return null;

            var orderId = Val("OrderID") ?? "";
            var description = Val("OrderDetails", "Description") ?? "";

            var inv = InvoiceRx().Match(orderId);
            if (!inv.Success) inv = InvoiceRx().Match(description);
            if (!inv.Success) return null;

            var payment = new OnlinePayment
            {
                PaymentRef = Val("cPayPaymentRef"),
                MessageId = messageId,
                ReceivedAt = receivedAt,
                InvoiceNumber = int.Parse(inv.Groups[1].Value, CultureInfo.InvariantCulture),
                InvoiceYear = int.Parse(inv.Groups[2].Value, CultureInfo.InvariantCulture),
                Amount = Num(SumaRx().Match(orderId)),
                Fee = Num(FeeRx().Match(orderId)),
                PayerName = Group(NameRx().Match(orderId)),
                PayerAddress = Group(AddressRx().Match(orderId)),
                RawOrderId = orderId
            };

            if (double.TryParse(Val("OrderDetails", "Amount"), NumberStyles.Any, CultureInfo.InvariantCulture, out var cents))
                payment.Charged = Math.Round(cents / 100d, 2);

            // Ако „suma“ ја нема — износ = наплатено - провизија
            if (payment.Amount <= 0 && payment.Charged > 0)
                payment.Amount = Math.Round(payment.Charged - payment.Fee, 2);

            payment.Key = !string.IsNullOrWhiteSpace(payment.PaymentRef)
                ? "ref:" + payment.PaymentRef
                : "msg:" + (messageId ?? Guid.NewGuid().ToString("N"));

            return payment;
        }

        [GeneratedRegex(@"Payment\s*order\s*:\s*(\d+)", RegexOptions.IgnoreCase)]
        private static partial Regex PaymentOrderRx();

        [GeneratedRegex(@"Status\s*:\s*([A-Za-z]+)", RegexOptions.IgnoreCase)]
        private static partial Regex StatusRx();

        [GeneratedRegex(@"<[^>]+>")]
        private static partial Regex HtmlTagRx();

        /// <summary>
        /// Кога пораката нема orderInfo.xml — се чита од текстот на е-пораката:
        ///   Payment order : 70091780
        ///   Details 2 : Faktura: 15976-2026, suma: 964, imePrezime: ..., adresa: ..., usluga: 50
        ///   Status : Successful
        /// </summary>
        public static OnlinePayment? ParseText(string? text, DateTimeOffset receivedAt, string? messageId)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // HTML → обичен текст
            text = System.Net.WebUtility.HtmlDecode(HtmlTagRx().Replace(text, " "));

            var status = StatusRx().Match(text);
            if (status.Success && !status.Groups[1].Value.StartsWith("Success", StringComparison.OrdinalIgnoreCase))
                return null;

            // Ред „Details 2“ (со suma) има предност; инаку целиот текст
            var line = text.Split('\n').FirstOrDefault(l => l.Contains("suma", StringComparison.OrdinalIgnoreCase)) ?? text;

            var inv = InvoiceRx().Match(line);
            if (!inv.Success) inv = InvoiceRx().Match(text);
            if (!inv.Success) return null;

            var payment = new OnlinePayment
            {
                PaymentRef = Group(PaymentOrderRx().Match(text)),
                MessageId = messageId,
                ReceivedAt = receivedAt,
                InvoiceNumber = int.Parse(inv.Groups[1].Value, CultureInfo.InvariantCulture),
                InvoiceYear = int.Parse(inv.Groups[2].Value, CultureInfo.InvariantCulture),
                Amount = Num(SumaRx().Match(line)),
                Fee = Num(FeeRx().Match(line)),
                PayerName = Group(NameRx().Match(line)),
                PayerAddress = Group(AddressRx().Match(line.Trim())),
                RawOrderId = line.Trim()
            };
            payment.Charged = Math.Round(payment.Amount + payment.Fee, 2);

            payment.Key = !string.IsNullOrWhiteSpace(payment.PaymentRef)
                ? "ref:" + payment.PaymentRef
                : "msg:" + (messageId ?? Guid.NewGuid().ToString("N"));

            return payment;
        }

        private static double Num(Match m) =>
            m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static string? Group(Match m) => m.Success ? m.Groups[1].Value.Trim() : null;
    }
}
