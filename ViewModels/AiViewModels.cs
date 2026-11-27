using CleanHub.Services.Ai;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CleanHub.ViewModels
{
    public class AiRemindersViewModel
    {
        public List<DebtorInfo> Debtors { get; set; } = new();
        public SelectList? Buildings { get; set; }
        public int? BuildingId { get; set; }
        public bool AiEnabled { get; set; }
        public bool SmsEnabled { get; set; }
        public string? SmsFrom { get; set; }
    }

    public class AiAnomaliesViewModel
    {
        public AnomalyReport Report { get; set; } = new();
        public string? Recipient { get; set; }
        public bool AiEnabled { get; set; }
    }

    public class AiAnnualReportViewModel
    {
        public SelectList? Buildings { get; set; }
        public int? BuildingId { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
        public string? Notes { get; set; }
        public bool AiEnabled { get; set; }
        /// <summary>Претходно генерирани (зачувани) извештаи.</summary>
        public List<AnnualReport> Saved { get; set; } = new();
    }

    /// <summary>Внес од формата за уредување на годишен извештај.</summary>
    public class AiAnnualReportEditInput
    {
        public string? Title { get; set; }
        public string? Narrative { get; set; }
        // Редови по месеци — паралелни листи (иста позиција = ист ред)
        public List<int> Month { get; set; } = new();
        public List<string?> Description { get; set; } = new();
        public List<string?> Amount { get; set; } = new();
        public List<int> Items { get; set; } = new();
        /// <summary>Што да се направи по зачувување: "print", "pdf" или ништо.</summary>
        public string? After { get; set; }
    }

    public class AiIdsRequest
    {
        public List<int> CustomerIds { get; set; } = new();
    }

    public class AiSendRequest
    {
        public List<ReminderMessage> Items { get; set; } = new();
    }

    public class AiAskRequest
    {
        public List<ChatTurn> Messages { get; set; } = new();
    }
}
