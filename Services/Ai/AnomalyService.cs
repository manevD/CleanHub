using System.Net;
using System.Text;
using CleanHub.Entities;
using CleanHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CleanHub.Services.Ai
{
    public class AnomalyItem
    {
        public string Category { get; set; } = "";
        public string Severity { get; set; } = "medium"; // high | medium | low
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public int? CustomerId { get; set; }
        public int? BuildingId { get; set; }
    }

    public class AnomalyReport
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public List<AnomalyItem> Items { get; set; } = new();
        public string Summary { get; set; } = "";
        public bool AiGenerated { get; set; }
        public Dictionary<string, int> Counts => Items.GroupBy(i => i.Category).ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Неделен извештај за можни грешки. Проверките се правила во C# (точни и повторливи),
    /// AI само пишува кратко резиме и приоритети.
    /// </summary>
    public class AnomalyService
    {
        public const string CatDuplicate = "Двојни уплати";
        public const string CatMissingInvoice = "Згради без фактура";
        public const string CatUnusualCost = "Необични трошоци";
        public const string CatNegativeSaldo = "Негативно салдо";

        private readonly ApplicationDbContext _db;
        private readonly ClaudeClient _ai;

        public AnomalyService(ApplicationDbContext db, ClaudeClient ai)
        {
            _db = db;
            _ai = ai;
        }

        public async Task<AnomalyReport> BuildAsync(bool withAiSummary = true, CancellationToken ct = default)
        {
            var report = new AnomalyReport();

            var customerNames = await _db.Customers.AsNoTracking()
                .Select(c => new { c.Id, c.CustomerInfo, c.Hide, c.Inactive, c.BuildingId, c.Subscription })
                .ToDictionaryAsync(c => c.Id, ct);

            var buildings = await _db.Buildings.AsNoTracking()
                .Select(b => new { b.Id, b.Name, b.CustomerRefId })
                .ToListAsync(ct);

            string CustomerName(int? id) => id.HasValue && customerNames.TryGetValue(id.Value, out var c) ? c.CustomerInfo : $"#{id}";
            string BuildingName(int? id) => buildings.FirstOrDefault(b => b.Id == id)?.Name ?? $"Објект {id}";

            var today = DateOnly.FromDateTime(DateTime.Today);

            // 1. Двојни уплати (последни 120 дена): ист станар, ист фонд, иста фактура/опис, ист износ и датум
            var since = today.AddDays(-120);
            var payments = await _db.BookFinancials.AsNoTracking()
                .Where(x => x.CustomerId != null && x.Demands > 0 && x.DatumF >= since
                            && (x.InvoiceId == (int)InvoiceTyp.Recieve || x.InvoiceId == (int)InvoiceTyp.Reserve))
                .Select(x => new { x.CustomerId, x.InvoiceId, x.DocumentId, x.Description, x.Demands, x.DatumF })
                .ToListAsync(ct);

            foreach (var g in payments
                         .GroupBy(x => new { x.CustomerId, x.InvoiceId, x.DocumentId, Description = (x.Description ?? "").Trim(), Amount = Math.Round(x.Demands, 2), x.DatumF })
                         .Where(g => g.Count() > 1)
                         .OrderByDescending(g => g.Key.Amount)
                         .Take(30))
            {
                report.Items.Add(new AnomalyItem
                {
                    Category = CatDuplicate,
                    Severity = "high",
                    CustomerId = g.Key.CustomerId,
                    Title = CustomerName(g.Key.CustomerId),
                    Detail = $"{g.Count()}× уплата од {FinanceDataService.Money(g.Key.Amount)} на {g.Key.DatumF:dd.MM.yyyy} ({g.Key.InvoiceId}){(g.Key.Description.Length > 0 ? " — " + g.Key.Description : "")}"
                });
            }

            // 2. Згради без фактура за минатиот месец — само партнери со дејност 2 (згради)
            var monthStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var invoicedCustomers = (await _db.Documents.AsNoTracking()
                    .Where(d => d.CustomerId != null && d.Date >= monthStart && d.Date <= monthEnd)
                    .Select(d => d.CustomerId!.Value)
                    .Distinct()
                    .ToListAsync(ct))
                .ToHashSet();

            var buildingCustomers = await _db.Customers.AsNoTracking()
                .Where(c => c.ActivityId == 2 && c.Inactive != true)
                .Select(c => new { c.Id, c.CustomerInfo, c.BuildingId })
                .ToListAsync(ct);

            var monthName = monthStart.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", FinanceDataService.Mk);
            foreach (var bc in buildingCustomers.Where(c => !invoicedCustomers.Contains(c.Id)).OrderBy(c => c.CustomerInfo))
            {
                report.Items.Add(new AnomalyItem
                {
                    Category = CatMissingInvoice,
                    Severity = "high",
                    BuildingId = bc.BuildingId,
                    Title = bc.CustomerInfo,
                    Detail = $"Нема фактура за {monthName}."
                });
            }

            // 3. Необично големи трошоци (резервен фонд) во последните 60 дена
            var costSince = today.AddDays(-425);
            var refToBuilding = buildings.GroupBy(b => b.CustomerRefId ?? b.Id).ToDictionary(g => g.Key, g => g.First().Id);
            var costs = await _db.BookFinancials.AsNoTracking()
                .Where(x => x.CustomerId != null && x.InvoiceId == (int)InvoiceTyp.Reserve && x.DocumentTypId == 5
                            && x.Owes > 0 && x.DatumF >= costSince)
                .Select(x => new { CustomerId = x.CustomerId!.Value, x.Description, x.Owes, x.DatumF })
                .ToListAsync(ct);

            var recentFrom = today.AddDays(-60);
            foreach (var cost in costs.Where(c => c.DatumF >= recentFrom).OrderByDescending(c => c.Owes))
            {
                var history = costs
                    .Where(h => h.CustomerId == cost.CustomerId && h.DatumF < cost.DatumF && h.DatumF >= cost.DatumF!.Value.AddDays(-365))
                    .ToList();
                var sameKind = history.Where(h => string.Equals((h.Description ?? "").Trim(), (cost.Description ?? "").Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                var baseline = sameKind.Count >= 3 ? sameKind : history;
                if (baseline.Count < 3) continue;

                var median = Median(baseline.Select(h => h.Owes));
                if (median <= 0 || cost.Owes < median * 2.5 || cost.Owes - median < 1000) continue;

                var buildingId = refToBuilding.TryGetValue(cost.CustomerId, out var bId) ? bId : (int?)null;
                report.Items.Add(new AnomalyItem
                {
                    Category = CatUnusualCost,
                    Severity = "medium",
                    BuildingId = buildingId,
                    Title = buildingId.HasValue ? BuildingName(buildingId) : CustomerName(cost.CustomerId),
                    Detail = $"{cost.DatumF:dd.MM.yyyy}: {cost.Description} — {FinanceDataService.Money(cost.Owes)} (вообичаено ≈ {FinanceDataService.Money(median)})"
                });
                if (report.Items.Count(i => i.Category == CatUnusualCost) >= 20) break;
            }

            // 4. Негативно салдо (уплатено повеќе од задолжено) — иста логика како картицата 1200
            var from2021 = new DateOnly(2021, 1, 1);
            var docSums = await _db.Documents.AsNoTracking()
                .Where(d => d.CustomerId != null && d.Date > from2021)
                .GroupBy(d => d.CustomerId!.Value)
                .Select(g => new { CustomerId = g.Key, Sum = g.Sum(d => (double)(d.TotalOutput ?? 0)) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Sum, ct);

            var bfSums = await _db.BookFinancials.AsNoTracking()
                .Where(x => x.CustomerId != null
                            && (x.InvoiceId == (int)InvoiceTyp.Recieve || x.DocumentTypId == 11)
                            && x.InvoiceId != (int)InvoiceTyp.Reserve)
                .GroupBy(x => x.CustomerId!.Value)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Demands = g.Sum(x => x.Demands),
                    Owes = g.Sum(x => x.DatumF >= from2021 ? x.Owes : 0)
                })
                .ToListAsync(ct);

            var negatives = bfSums
                .Select(b => new
                {
                    b.CustomerId,
                    Saldo = (docSums.TryGetValue(b.CustomerId, out var d) ? d : 0) + b.Owes - b.Demands
                })
                // Станарите во претплата нормално имаат негативно салдо — не се грешка
                .Where(x => x.Saldo < -1 && customerNames.TryGetValue(x.CustomerId, out var c)
                            && !c.Hide && c.Inactive != true && !(c.Subscription > 0))
                .OrderBy(x => x.Saldo)
                .Take(30);

            foreach (var n in negatives)
            {
                report.Items.Add(new AnomalyItem
                {
                    Category = CatNegativeSaldo,
                    Severity = "low",
                    CustomerId = n.CustomerId,
                    Title = CustomerName(n.CustomerId),
                    Detail = $"Салдо {FinanceDataService.Money(n.Saldo)} — уплатено повеќе од задолжено, а не е во претплата. Проверете го книжењето."
                });
            }

            report.Summary = FallbackSummary(report);
            if (withAiSummary && _ai.IsConfigured && report.Items.Count > 0)
            {
                var ai = await _ai.CompleteAsync(
                    "Ти си сметководствен асистент за фирма за управување со згради во Северна Македонија. " +
                    "Добиваш листа на автоматски пронајдени можни грешки. Напиши кратко резиме на македонски (кирилица), " +
                    "4–8 реченици или точки без markdown наслови: што е најитно, што веројатно е лажна тревога и што да се провери прво. " +
                    "Не измислувај бројки — користи само дадените.",
                    ClaudeClient.ToJson(new
                    {
                        counts = report.Counts,
                        top = report.Items
                            .OrderBy(i => i.Severity == "high" ? 0 : i.Severity == "medium" ? 1 : 2)
                            .Take(25)
                            .Select(i => new { i.Category, i.Severity, i.Title, i.Detail })
                    }),
                    900, ct);

                if (!string.IsNullOrWhiteSpace(ai))
                {
                    report.Summary = ai;
                    report.AiGenerated = true;
                }
            }

            return report;
        }

        public static string BuildEmailHtml(AnomalyReport report, string? appUrl)
        {
            var sb = new StringBuilder();
            sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#0f172a;max-width:760px\">");
            sb.Append($"<h2 style=\"margin:0 0 4px\">Неделен извештај — {report.GeneratedAt:dd.MM.yyyy}</h2>");
            sb.Append($"<p style=\"color:#475569;margin:0 0 16px\">Пронајдени ставки: {report.Items.Count}</p>");
            sb.Append("<div style=\"background:#ecfdf5;border-radius:12px;padding:14px 16px;margin-bottom:18px;white-space:pre-line\">")
              .Append(WebUtility.HtmlEncode(report.Summary)).Append("</div>");

            foreach (var group in report.Items.GroupBy(i => i.Category))
            {
                sb.Append($"<h3 style=\"margin:18px 0 8px\">{WebUtility.HtmlEncode(group.Key)} ({group.Count()})</h3>");
                sb.Append("<table style=\"border-collapse:collapse;width:100%;font-size:13px\">");
                foreach (var item in group.Take(25))
                {
                    sb.Append("<tr><td style=\"padding:6px 8px;border-bottom:1px solid #e5e9f0;font-weight:600;white-space:nowrap\">")
                      .Append(WebUtility.HtmlEncode(item.Title))
                      .Append("</td><td style=\"padding:6px 8px;border-bottom:1px solid #e5e9f0\">")
                      .Append(WebUtility.HtmlEncode(item.Detail))
                      .Append("</td></tr>");
                }
                sb.Append("</table>");
            }

            if (!string.IsNullOrWhiteSpace(appUrl))
                sb.Append($"<p style=\"margin-top:20px\"><a href=\"{WebUtility.HtmlEncode(appUrl)}\">Отвори го извештајот во апликацијата</a></p>");

            sb.Append("</div>");
            return sb.ToString();
        }

        private static string FallbackSummary(AnomalyReport report)
        {
            if (report.Items.Count == 0) return "Не се пронајдени можни грешки. 👍";
            var parts = report.Counts.Select(c => $"• {c.Key}: {c.Value}");
            var high = report.Items.Count(i => i.Severity == "high");
            return $"Пронајдени се {report.Items.Count} ставки за проверка{(high > 0 ? $", од кои {high} се итни" : "")}.\n" + string.Join("\n", parts);
        }

        private static double Median(IEnumerable<double> values)
        {
            var list = values.OrderBy(v => v).ToList();
            if (list.Count == 0) return 0;
            var mid = list.Count / 2;
            return list.Count % 2 == 0 ? (list[mid - 1] + list[mid]) / 2 : list[mid];
        }
    }
}
