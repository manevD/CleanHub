using CleanHub.Core.Config;
using System.ComponentModel.DataAnnotations.Schema;

namespace CleanHub.Core.Entities
{
    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public string Notes { get; set; } = @"
                Со зборови:
                Ве молиме да назначенит износ го платите во валутниот рок. За секое задоцнување пресметуваме законска затезна камата. Во случај
                на спор надлежен е Основен Суд - Струмица.
                Начин на плаќање:
                -Во наплатен центар на ДПТУ,,МАРТИ ХИГИЕНА,,ДООЕЛ-Струмица
                -Во пошта и банки
                -ONLINE плаќање на https://martihigiena.mk/
                Напомена:Плаќањето во ТТК Банка А.Д. Скопје е со 20 ден. провизија";

        public decimal AmountDue { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public decimal Discount { get; set; } = 0;
        // Foreign key for the associated resident
        public int ResidentId { get; set; }
        // Navigation property for the associated resident
        public Resident Resident { get; set; }

        [NotMapped]
        public CompanyConfig Company { get; set; }
    }

    public enum InvoiceTyp
    {
        Резервен = 1201,
        Редовен = 1201
    }

    public enum PaymentStatus
    {
        Неплатено,
        Платено,
        Задоцнето
    }
}
