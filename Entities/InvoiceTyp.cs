using System.ComponentModel;

namespace CleanHub.Entities
{
    public enum InvoiceTyp
    {
        [Description("Струја")]
        Energy = 4001,
        [Description("Редовен Фонд")]
        Recieve = 1200,
        [Description("Резервен Фонд")]
        Reserve = 1201
    }
}
