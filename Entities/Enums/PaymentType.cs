using System.ComponentModel;

namespace CleanHub.Entities.Enums
{
    public enum PaymentType
    {
        [Description("Банка")] 
        Bank,
        [Description("Пошта")]
        Post,
        [Description("Фактура")]
        Invoice,
        [Description("Веб")]
        Web,
        [Description("Претплата")]
        Subscription
    }
}
