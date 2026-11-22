using CleanHub.Attribute;
using CleanHub.Config;
using CleanHub.Services.Ai;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SelectPdf;

namespace CleanHub.Controllers
{
    /// <summary>AI функции: потсетници за долг, асистент, неделен извештај, годишен извештај.</summary>
    [RequireLogin]
    public class AiController(
        FinanceDataService _data,
        DebtReminderService _reminders,
        AssistantService _assistant,
        AnomalyService _anomalies,
        AnnualReportService _annual,
        EmailSender _email,
        IMemoryCache _cache,
        ICompositeViewEngine _viewEngine,
        IOptions<AiConfig> _aiConfig,
        IOptions<SMTPConfig> _smtpConfig) : Controller
    {
        private const string AnomalyCacheKey = "ai-anomaly-report";

        private async Task<SelectList> BuildingsSelectAsync(int? selected)
        {
            var list = await _data.GetBuildingsAsync(HttpContext.RequestAborted);
            return new SelectList(list.Select(b => new { b.Id, b.Name }), "Id", "Name", selected);
        }

        // ------------------------------------------------------------------ 3. Потсетници
        [HttpGet]
        public async Task<IActionResult> Reminders(int? buildingId)
        {
            var model = new AiRemindersViewModel
            {
                BuildingId = buildingId,
                Buildings = await BuildingsSelectAsync(buildingId),
                Debtors = await _data.GetDebtorsAsync(buildingId, 1, HttpContext.RequestAborted),
                AiEnabled = _aiConfig.Value.IsConfigured
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReminders([FromBody] AiIdsRequest request)
        {
            var ids = (request?.CustomerIds ?? new List<int>()).Distinct().Take(200).ToList();
            if (ids.Count == 0) return BadRequest(new { error = "Изберете барем еден станар." });

            var messages = await _reminders.GenerateAsync(ids, HttpContext.RequestAborted);
            return Json(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReminders([FromBody] AiSendRequest request)
        {
            var items = request?.Items ?? new List<ReminderMessage>();
            if (items.Count == 0) return BadRequest(new { error = "Нема пораки за праќање." });

            var results = await _reminders.SendEmailsAsync(items.Take(200), HttpContext.RequestAborted);
            return Json(results);
        }

        // ------------------------------------------------------------------ 4. Асистент
        [HttpGet]
        public IActionResult Assistant()
        {
            ViewBag.AiEnabled = _aiConfig.Value.IsConfigured;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ask([FromBody] AiAskRequest request)
        {
            var answer = await _assistant.AskAsync(request?.Messages ?? new List<ChatTurn>(), HttpContext.RequestAborted);
            return Json(new { answer });
        }

        // ------------------------------------------------------------------ 5. Извештај за грешки
        [HttpGet]
        public async Task<IActionResult> Anomalies(bool refresh = false)
        {
            if (refresh) _cache.Remove(AnomalyCacheKey);

            var report = await _cache.GetOrCreateAsync(AnomalyCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                return await _anomalies.BuildAsync(withAiSummary: true, HttpContext.RequestAborted);
            });

            return View(new AiAnomaliesViewModel
            {
                Report = report!,
                Recipient = ReportRecipient(),
                AiEnabled = _aiConfig.Value.IsConfigured
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendAnomalyEmail()
        {
            var recipient = ReportRecipient();
            if (string.IsNullOrWhiteSpace(recipient))
            {
                TempData["Error"] = "Не е поставена е-пошта за извештајот (AI:WeeklyReportRecipient).";
                return RedirectToAction(nameof(Anomalies));
            }

            var report = _cache.Get<AnomalyReport>(AnomalyCacheKey) ?? await _anomalies.BuildAsync(true, HttpContext.RequestAborted);
            var url = Url.Action(nameof(Anomalies), "Ai", null, Request.Scheme);
            var html = AnomalyService.BuildEmailHtml(report, url);

            try
            {
                foreach (var to in recipient.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    await _email.SendAsync(to, $"Извештај за грешки — {report.GeneratedAt:dd.MM.yyyy}", html, true, HttpContext.RequestAborted);
                TempData["Success"] = $"Извештајот е пратен на {recipient}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Извештајот не е пратен: " + ex.Message;
            }

            return RedirectToAction(nameof(Anomalies));
        }

        private string? ReportRecipient() =>
            !string.IsNullOrWhiteSpace(_aiConfig.Value.WeeklyReportRecipient)
                ? _aiConfig.Value.WeeklyReportRecipient
                : _smtpConfig.Value.Recipient;

        // ------------------------------------------------------------------ 6. Годишен извештај
        [HttpGet]
        public async Task<IActionResult> AnnualReport(int? buildingId, int? year)
        {
            return View(new AiAnnualReportViewModel
            {
                BuildingId = buildingId,
                Year = year ?? DateTime.Now.Year - (DateTime.Now.Month < 3 ? 1 : 0),
                Buildings = await BuildingsSelectAsync(buildingId),
                AiEnabled = _aiConfig.Value.IsConfigured
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnnualReport(int buildingId, int year, string? notes)
        {
            var model = new AiAnnualReportViewModel
            {
                BuildingId = buildingId,
                Year = year,
                Notes = notes,
                Buildings = await BuildingsSelectAsync(buildingId),
                AiEnabled = _aiConfig.Value.IsConfigured
            };

            var report = await _annual.BuildAsync(buildingId, year, notes, HttpContext.RequestAborted);
            if (report == null)
            {
                TempData["Error"] = "Објектот не е пронајден.";
                return View(model);
            }

            model.Report = report;
            model.ReportKey = Guid.NewGuid().ToString("N");
            _cache.Set("ai-annual-" + model.ReportKey, report, TimeSpan.FromHours(2));
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AnnualReportPdf(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !_cache.TryGetValue("ai-annual-" + key, out AnnualReport? report) || report == null)
            {
                TempData["Error"] = "Извештајот истече. Генерирајте го повторно.";
                return RedirectToAction(nameof(AnnualReport));
            }

            var html = await RenderViewToStringAsync("~/Views/Ai/_AnnualReportPdf.cshtml", report);
            var converter = new HtmlToPdf();
            converter.Options.PdfPageSize = PdfPageSize.A4;
            converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
            converter.Options.MarginTop = 28;
            converter.Options.MarginBottom = 28;
            converter.Options.MarginLeft = 28;
            converter.Options.MarginRight = 28;
            converter.Options.WebPageWidth = 794;

            var baseUrl = $"{Request.Scheme}://{Request.Host.Value}/";
            var doc = converter.ConvertHtmlString(html, baseUrl);
            using var ms = new MemoryStream();
            doc.Save(ms);
            doc.Close();

            var fileName = $"Godisen_izvestaj_{report.Stats.Year}_{report.Stats.BuildingId}.pdf";
            return File(ms.ToArray(), "application/pdf", fileName);
        }

        private async Task<string> RenderViewToStringAsync(string viewPath, object model)
        {
            ViewData.Model = model;
            using var writer = new StringWriter();
            var viewResult = _viewEngine.GetView("", viewPath, false);
            if (viewResult.View == null)
                throw new InvalidOperationException($"Погледот '{viewPath}' не е пронајден.");

            var viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, writer, new HtmlHelperOptions());
            await viewResult.View.RenderAsync(viewContext);
            return writer.ToString();
        }
    }
}
