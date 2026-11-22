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
        public AnnualReport? Report { get; set; }
        public string? ReportKey { get; set; }
        public bool AiEnabled { get; set; }
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
