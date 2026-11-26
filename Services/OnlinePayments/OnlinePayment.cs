namespace CleanHub.Services.OnlinePayments
{
    public enum OnlinePaymentStatus
    {
        New = 0,      // Ново — чека внесување
        Created = 1,  // Внесено плаќање
        Ignored = 2,  // Игнорирано
        AlreadyPaid = 3 // Фактурата веќе била платена (рачно во Сметки) — автоматски затворено
    }

    /// <summary>Една онлајн уплата прочитана од cPay е-порака (orderInfo.xml).</summary>
    public class OnlinePayment
    {
        /// <summary>cPayPaymentRef (уникатен) — или MessageId ако го нема.</summary>
        public string Key { get; set; } = "";
        public string? PaymentRef { get; set; }
        public string? MessageId { get; set; }

        /// <summary>Кога е примена е-пораката (се користи како датум на плаќање).</summary>
        public DateTimeOffset ReceivedAt { get; set; }

        public int InvoiceNumber { get; set; }
        public int? InvoiceYear { get; set; }

        /// <summary>„suma“ од cPay — износот на фактурата (без провизија).</summary>
        public double Amount { get; set; }

        /// <summary>„usluga“ — провизија на cPay.</summary>
        public double Fee { get; set; }

        /// <summary>Вкупно наплатено (Amount/100 од XML).</summary>
        public double Charged { get; set; }

        public string? PayerName { get; set; }
        public string? PayerAddress { get; set; }
        public string? RawOrderId { get; set; }

        public OnlinePaymentStatus Status { get; set; } = OnlinePaymentStatus.New;
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public string? ProcessedBy { get; set; }
    }

    public class OnlinePaymentStoreData
    {
        public DateTime? LastRunUtc { get; set; }
        public string? LastError { get; set; }
        public int LastRunAdded { get; set; }
        public List<OnlinePayment> Payments { get; set; } = new();
    }
}
