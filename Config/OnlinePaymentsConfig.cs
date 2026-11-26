namespace CleanHub.Config
{
    /// <summary>
    /// Онлајн уплати (cPay / CASYS) — читање на е-пораките со orderInfo.xml.
    /// Поставки во user-secrets / env (секција "OnlinePayments"), никогаш во appsettings.json:
    ///   OnlinePayments:Username  — е-пошта што ги прима пораките од cPay (на пр. Gmail)
    ///   OnlinePayments:Password  — App Password (Google сметка → Безбедност → Лозинки за апликации)
    /// </summary>
    public class OnlinePaymentsConfig
    {
        public string ImapHost { get; set; } = "imap.gmail.com";
        public int ImapPort { get; set; } = 993;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string Folder { get; set; } = "INBOX";

        /// <summary>Испраќач на пораките за уплата.</summary>
        public string FromAddress { get; set; } = "cpay@casys.com.mk";

        /// <summary>При прво читање: колку дена наназад.</summary>
        public int InitialDays { get; set; } = 60;

        /// <summary>Час на секојдневното читање (време во Скопје). 0 = полноќ.</summary>
        public int DailyHour { get; set; } = 0;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    }
}
