using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Entities.Enums;
using CleanHub.Infrastructure.Data;
using CleanHub.Services.OnlinePayments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CleanHub.Controllers
{
    public enum OnlinePaymentMatch
    {
        Match,          // фактурата е пронајдена и сумата се совпаѓа
        AmountMismatch, // фактурата е пронајдена, но сумата е различна
        NoInvoice       // нема фактура со тој број
    }

    public class OnlinePaymentRow
    {
        public OnlinePayment Payment { get; set; } = new();
        public OnlinePaymentMatch Match { get; set; }
        public int? DocumentId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerInfo { get; set; }
        public string? BuildingName { get; set; }
        public double? InvoiceTotal { get; set; }
        public string? InvoicePeriod { get; set; }
        public bool InvoicePaid { get; set; }
        public DateOnly PaymentDate { get; set; }
        public string SuggestedDescription { get; set; } = "";
    }

    public class OnlinePaymentsViewModel
    {
        public List<OnlinePaymentRow> Rows { get; set; } = new();
        public string Show { get; set; } = "new";
        public bool Configured { get; set; }
        public DateTime? LastRunUtc { get; set; }
        public string? LastError { get; set; }
        public int CountNew { get; set; }
        public int CountMatchNew { get; set; }
        public int CountAll { get; set; }
    }

    /// <summary>Онлајн уплати (cPay): преглед и внесување како плаќање.</summary>
    [RequireLogin]
    public class OnlinePaymentsController(
        OnlinePaymentStore _store,
        CpayMailImporter _importer,
        ApplicationDbContext _db,
        IOptions<OnlinePaymentsConfig> _config) : Controller
    {
        [Route("ОнлајнУплати")]
        public async Task<IActionResult> Index(string show = "new")
        {
            var data = _store.Snapshot();
            var all = data.Payments.OrderByDescending(p => p.ReceivedAt).ToList();

            IEnumerable<OnlinePayment> list = show switch
            {
                "all" => all,
                "done" => all.Where(p => p.Status != OnlinePaymentStatus.New),
                _ => all.Where(p => p.Status == OnlinePaymentStatus.New)
            };
            var payments = list.ToList();

            // Фактури по број (само читање)
            var numbers = payments.Select(p => (int?)p.InvoiceNumber).Distinct().ToList();
            var docs = numbers.Count == 0
                ? new List<DocRow>()
                : await _db.Documents.AsNoTracking()
                    .Where(d => numbers.Contains(d.Number))
                    .Select(d => new DocRow
                    {
                        Id = d.Id,
                        Number = d.Number,
                        Date = d.Date,
                        ToDocument = d.ToDocument,
                        TotalOutput = d.TotalOutput,
                        PaymentStatus = d.PaymentStatus,
                        CustomerId = d.CustomerId,
                        CustomerInfo = d.Customer != null ? d.Customer.CustomerInfo : null,
                        BuildingName = d.Customer != null && d.Customer.Building != null ? d.Customer.Building.Name : null
                    })
                    .ToListAsync();

            var tz = SkopjeTimeZone();
            var rows = payments.Select(p => BuildRow(p, docs, tz)).ToList();

            return View(new OnlinePaymentsViewModel
            {
                Rows = rows,
                Show = show,
                Configured = _config.Value.IsConfigured,
                LastRunUtc = data.LastRunUtc,
                LastError = data.LastError,
                CountAll = all.Count,
                CountNew = all.Count(p => p.Status == OnlinePaymentStatus.New),
                CountMatchNew = show == "new" ? rows.Count(r => r.Match == OnlinePaymentMatch.Match) : -1
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Refresh()
        {
            var result = await _importer.ImportAsync(HttpContext.RequestAborted);
            if (result.Ok)
                TempData["Success"] = result.Added > 0
                    ? $"Прочитани {result.Read} пораки — {result.Added} нови уплати."
                    : $"Прочитани {result.Read} пораки — нема нови уплати.";
            else
                TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetStatus(string key, OnlinePaymentStatus status)
        {
            var ok = _store.SetStatus(key, status, User.Identity?.Name);
            return ok ? Json(new { success = true }) : NotFound(new { success = false, message = "Уплатата не е пронајдена." });
        }

        private static OnlinePaymentRow BuildRow(OnlinePayment p, List<DocRow> docs, TimeZoneInfo tz)
        {
            var candidates = docs.Where(d => d.Number == p.InvoiceNumber).ToList();
            var doc = candidates.Where(d => p.InvoiceYear.HasValue && d.Date.HasValue && d.Date.Value.Year == p.InvoiceYear.Value)
                          .OrderByDescending(d => d.Date).FirstOrDefault()
                      ?? candidates.FirstOrDefault(d => p.InvoiceYear.HasValue && (d.ToDocument ?? "").Contains(p.InvoiceYear.Value.ToString()))
                      ?? (candidates.Count == 1 ? candidates[0] : null);

            var local = TimeZoneInfo.ConvertTime(p.ReceivedAt, tz);
            var row = new OnlinePaymentRow
            {
                Payment = p,
                PaymentDate = DateOnly.FromDateTime(local.DateTime)
            };

            // Опис: „за MM/YYYY по веб“ според месецот на фактурата
            var month = doc?.Date?.Month;
            var year = doc?.Date?.Year ?? p.InvoiceYear;
            row.SuggestedDescription = month.HasValue && year.HasValue
                ? $"за {month.Value:00}/{year.Value} по веб"
                : $"за фактура {p.InvoiceNumber}{(p.InvoiceYear.HasValue ? "-" + p.InvoiceYear : "")} по веб";

            if (doc == null)
            {
                row.Match = OnlinePaymentMatch.NoInvoice;
                return row;
            }

            row.DocumentId = doc.Id;
            row.CustomerId = doc.CustomerId;
            row.CustomerInfo = doc.CustomerInfo;
            row.BuildingName = doc.BuildingName;
            row.InvoiceTotal = doc.TotalOutput.HasValue ? Math.Round((double)doc.TotalOutput.Value, 2) : null;
            row.InvoicePeriod = doc.ToDocument;
            row.InvoicePaid = doc.PaymentStatus == PaymentStatus.Платено;
            row.Match = row.InvoiceTotal.HasValue && Math.Abs(row.InvoiceTotal.Value - p.Amount) < 0.5
                ? OnlinePaymentMatch.Match
                : OnlinePaymentMatch.AmountMismatch;
            return row;
        }

        private static TimeZoneInfo SkopjeTimeZone()
        {
            foreach (var id in new[] { "Europe/Skopje", "Central European Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.Local;
        }

        public class DocRow
        {
            public int Id { get; set; }
            public int? Number { get; set; }
            public DateOnly? Date { get; set; }
            public string? ToDocument { get; set; }
            public float? TotalOutput { get; set; }
            public PaymentStatus PaymentStatus { get; set; }
            public int? CustomerId { get; set; }
            public string? CustomerInfo { get; set; }
            public string? BuildingName { get; set; }
        }
    }
}
