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
        AnnualReportStore _annualStore,
        EmailSender _email,
        IMemoryCache _cache,
        ICompositeViewEngine _viewEngine,
        IOptions<AiConfig> _aiConfig,
        IOptions<TelnyxConfig> _telnyxConfig,
        IOptions<SMTPConfig> _smtpConfig,
        IOptions<CompanyConfig> _companyConfig) : Controller
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
                AiEnabled = _aiConfig.Value.IsConfigured,
                SmsEnabled = _telnyxConfig.Value.IsConfigured,
                SmsFrom = _telnyxConfig.Value.From
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendSmsReminders([FromBody] AiSendRequest request)
        {
            if (!_telnyxConfig.Value.IsConfigured)
                return BadRequest(new { error = "SMS не е поставен (Telnyx:ApiKey)." });

            var items = request?.Items ?? new List<ReminderMessage>();
            if (items.Count == 0) return BadRequest(new { error = "Нема пораки за праќање." });

            var results = await _reminders.SendSmsAsync(items.Take(200), HttpContext.RequestAborted);
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
                AiEnabled = _aiConfig.Value.IsConfigured,
                Saved = _annualStore.List(buildingId)
            });
        }

        /// <summary>Генерира нов извештај, го зачувува и го отвора за уредување.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnnualReport(int buildingId, int year, string? notes)
        {
            var report = await _annual.BuildAsync(buildingId, year, notes, HttpContext.RequestAborted);
            if (report == null)
            {
                TempData["Error"] = "Објектот не е пронајден.";
                return RedirectToAction(nameof(AnnualReport), new { buildingId, year });
            }

            report.CreatedBy = User.Identity?.Name;
            try
            {
                _annualStore.Upsert(report);
            }
            catch (IOException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(AnnualReport), new { buildingId, year });
            }

            TempData["Success"] = "Извештајот е генериран и зачуван. Можете да го уредите пред печатење.";
            return RedirectToAction(nameof(AnnualReportEdit), new { id = report.Id });
        }

        [HttpGet]
        public IActionResult AnnualReportEdit(string id)
        {
            var report = _annualStore.Get(id);
            if (report == null)
            {
                TempData["Error"] = "Извештајот не е пронајден (можеби е избришан).";
                return RedirectToAction(nameof(AnnualReport));
            }

            ViewBag.AiEnabled = _aiConfig.Value.IsConfigured;
            return View(report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AnnualReportEdit(string id, AiAnnualReportEditInput input)
        {
            var report = _annualStore.Get(id);
            if (report == null)
            {
                TempData["Error"] = "Извештајот не е пронајден (можеби е избришан).";
                return RedirectToAction(nameof(AnnualReport));
            }

            report.Title = string.IsNullOrWhiteSpace(input.Title) ? $"Годишен извештај за {report.Stats.Year} година" : input.Title.Trim();
            report.Narrative = (input.Narrative ?? "").Replace("\r\n", "\n").Trim();

            var months = new List<MonthCost>();
            for (var i = 0; i < input.Month.Count; i++)
            {
                var desc = (i < input.Description.Count ? input.Description[i] : null)?.Trim() ?? "";
                var amountText = i < input.Amount.Count ? input.Amount[i] : null;
                if (desc.Length == 0 && string.IsNullOrWhiteSpace(amountText)) continue; // празен ред

                if (!TryParseAmount(amountText, out var amount))
                {
                    TempData["Error"] = $"Неважечки износ во ред {i + 1}: „{amountText}“. Пр. 1.234,56";
                    return RedirectToAction(nameof(AnnualReportEdit), new { id });
                }

                months.Add(new MonthCost
                {
                    Month = Math.Clamp(input.Month[i], 1, 12),
                    Description = desc,
                    Amount = Math.Round(amount, 2),
                    Items = i < input.Items.Count ? Math.Max(0, input.Items[i]) : 0
                });
            }
            report.Months = months.OrderBy(m => m.Month).ToList();
            report.UpdatedAt = DateTime.Now;
            report.UpdatedBy = User.Identity?.Name;

            try
            {
                _annualStore.Upsert(report);
            }
            catch (IOException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(AnnualReportEdit), new { id });
            }

            if (input.After == "print") return RedirectToAction(nameof(AnnualReportPrint), new { id });
            if (input.After == "pdf") return RedirectToAction(nameof(AnnualReportPdf), new { id });

            TempData["Success"] = "Промените се зачувани.";
            return RedirectToAction(nameof(AnnualReportEdit), new { id });
        }

        /// <summary>Ги враќа редовите по месеци од книжењата (ги брише рачните промени во табелата).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AnnualReportResetMonths(string id)
        {
            var report = _annualStore.Get(id);
            if (report == null) return RedirectToAction(nameof(AnnualReport));

            report.Months = AnnualReportService.GroupByMonth(report.Stats.Costs);
            report.UpdatedAt = DateTime.Now;
            report.UpdatedBy = User.Identity?.Name;
            _annualStore.Upsert(report);
            TempData["Success"] = "Табелата по месеци е вратена според книжењата.";
            return RedirectToAction(nameof(AnnualReportEdit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AnnualReportDelete(string id)
        {
            var report = _annualStore.Get(id);
            try
            {
                TempData[_annualStore.Delete(id) ? "Success" : "Error"] = report != null
                    ? $"Извештајот „{report.Stats.BuildingName} — {report.Stats.Year}“ е избришан."
                    : "Извештајот не е пронајден.";
            }
            catch (IOException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(AnnualReport), new { buildingId = report?.Stats.BuildingId });
        }

        /// <summary>Чиста A4 страница за печатење (се отвора во нов таб и автоматски го отвора дијалогот за печатење).</summary>
        [HttpGet]
        public IActionResult AnnualReportPrint(string id)
        {
            var report = _annualStore.Get(id);
            if (report == null)
            {
                TempData["Error"] = "Извештајот не е пронајден.";
                return RedirectToAction(nameof(AnnualReport));
            }

            return View("~/Views/Ai/_AnnualReportPdf.cshtml",
                new AnnualReportDocument { Report = report, Company = _companyConfig.Value, AutoPrint = true });
        }

        [HttpGet]
        public async Task<IActionResult> AnnualReportPdf(string id)
        {
            var report = _annualStore.Get(id);
            if (report == null)
            {
                TempData["Error"] = "Извештајот не е пронајден.";
                return RedirectToAction(nameof(AnnualReport));
            }

            var html = await RenderViewToStringAsync("~/Views/Ai/_AnnualReportPdf.cshtml",
                new AnnualReportDocument { Report = report, Company = _companyConfig.Value });
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

        /// <summary>Прифаќа „1.234,56“, „1234,56“ и „1234.56“.</summary>
        private static bool TryParseAmount(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return true; // празно = 0
            var t = text.Replace("МКД", "", StringComparison.OrdinalIgnoreCase).Replace("ден", "").Replace(" ", "").Replace("\u00A0", "").Trim();
            if (t.Contains(','))
                t = t.Replace(".", "").Replace(',', '.');        // мк формат: точка = илјади, запирка = децимали
            else if (t.Count(ch => ch == '.') > 1 || System.Text.RegularExpressions.Regex.IsMatch(t, @"^-?\d{1,3}\.\d{3}$"))
                t = t.Replace(".", "");                          // 1.234.567 или 1.500 = илјади
            return double.TryParse(t, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
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
