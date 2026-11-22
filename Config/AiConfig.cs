namespace CleanHub.Config
{
    /// <summary>
    /// Поставки за AI функциите (секција "AI" во appsettings / user-secrets / environment).
    /// Клучот НЕ се чува во appsettings.json — користете:
    ///   dotnet user-secrets set "AI:ApiKey" "sk-ant-..."
    /// или променлива на околина:  AI__ApiKey=sk-ant-...
    /// </summary>
    public class AiConfig
    {
        /// <summary>Anthropic API клуч. Ако е празен, апликацијата користи шаблони без AI.</summary>
        public string? ApiKey { get; set; }

        /// <summary>Модел за сите AI функции.</summary>
        public string Model { get; set; } = "claude-sonnet-5-5";

        public string Endpoint { get; set; } = "https://api.anthropic.com/v1/messages";

        /// <summary>Е-пошта што го добива неделниот извештај секој понеделник. Празно = исклучено.</summary>
        public string? WeeklyReportRecipient { get; set; }

        /// <summary>Час (локално време во Скопје) кога се праќа неделниот извештај.</summary>
        public int WeeklyReportHour { get; set; } = 8;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
