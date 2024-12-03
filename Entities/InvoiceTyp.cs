using System.ComponentModel;

namespace CleanHub.Entities
{
    public enum InvoiceTyp
    {
        [Description("Струја")]
        Energy = 741007,
        [Description("Редовен Фонд")]
        Recieve = 1200,
        [Description("Резервен Фонд")]
        Reserve = 1201
    }
}
