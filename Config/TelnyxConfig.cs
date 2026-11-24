namespace CleanHub.Config
{
    /// <summary>
    /// Telnyx SMS (секција "Telnyx" — user-secrets / env, НЕ во appsettings.json):
    ///   Telnyx:ApiKey              — API клуч (Account → API Keys)
    ///   Telnyx:From                — име на испраќач (Alpha sender), до 11 знаци, на пр. MartiHigien
    ///   Telnyx:MessagingProfileId  — ID на Messaging Profile (Messaging → Programmable Messaging)
    /// </summary>
    public class TelnyxConfig
    {
        public string? ApiKey { get; set; }
        public string From { get; set; } = "MartiHigien";
        public string? MessagingProfileId { get; set; }
        public string Endpoint { get; set; } = "https://api.telnyx.com/v2/messages";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(From);
    }
}
