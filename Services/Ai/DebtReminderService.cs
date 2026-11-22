using System.Text.Json;
using System.Text.Json.Nodes;
using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Services.Ai
{
    public class ReminderMessage
    {
        public int CustomerId { get; set; }
        public string CustomerInfo { get; set; } = "";
        public string Level { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public bool AiGenerated { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class ReminderSendResult
    {
        public int CustomerId { get; set; }
        public bool Sent { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Паметни потсетници: AI пишува лична порака според бројот на неплатени месеци.
    /// 1 месец → љубезно, 2 → потсетник, 3+ → опомена. Без AI клуч се користи шаблон.
    /// До AI се праќаат само име, периоди и износ — без е-пошта, телефон или други лични податоци.
    /// </summary>
    public class DebtReminderService
    {
        private const int BatchSize = 12;

        private readonly FinanceDataService _data;
        private readonly ClaudeClient _ai;
        private readonly EmailSender _email;
        private readonly CompanyConfig _company;
        private readonly ILogger<DebtReminderService> _logger;

        public DebtReminderService(FinanceDataService data, ClaudeClient ai, EmailSender email,
            IOptions<CompanyConfig> company, ILogger<DebtReminderService> logger)
        {
            _data = data;
            _ai = ai;
            _email = email;
            _company = company.Value;
            _logger = logger;
        }

        public async Task<List<ReminderMessage>> GenerateAsync(IReadOnlyCollection<int> customerIds, CancellationToken ct = default)
        {
            var debtors = (await _data.GetDebtorsAsync(null, 1, ct))
                .Where(d => customerIds.Contains(d.CustomerId))
                .ToList();

            var messages = debtors.Select(Template).ToDictionary(m => m.CustomerId);

            if (_ai.IsConfigured)
            {
                // Групи од 12 станари, најмногу 4 барања паралелно
                using var gate = new SemaphoreSlim(4);
                var tasks = debtors.Chunk(BatchSize).Select(async batch =>
                {
                    await gate.WaitAsync(ct);
                    try { return await GenerateBatchWithAiAsync(batch, ct); }
                    finally { gate.Release(); }
                }).ToList();

                foreach (var generated in await Task.WhenAll(tasks))
                {
                    foreach (var g in generated)
                    {
                        if (messages.TryGetValue(g.Key, out var m) && !string.IsNullOrWhiteSpace(g.Value.body))
                        {
                            m.Subject = string.IsNullOrWhiteSpace(g.Value.subject) ? m.Subject : g.Value.subject.Trim();
                            m.Body = g.Value.body.Trim();
                            m.AiGenerated = true;
                        }
                    }
                }
            }

            return debtors.Select(d => messages[d.CustomerId]).ToList();
        }

        public async Task<List<ReminderSendResult>> SendEmailsAsync(IEnumerable<ReminderMessage> items, CancellationToken ct = default)
        {
            var results = new List<ReminderSendResult>();
            var debtors = (await _data.GetDebtorsAsync(null, 1, ct)).ToDictionary(d => d.CustomerId);

            foreach (var item in items)
            {
                // Е-поштата секогаш се зема од базата, никогаш од прелистувачот.
                if (!debtors.TryGetValue(item.CustomerId, out var debtor))
                {
                    results.Add(new ReminderSendResult { CustomerId = item.CustomerId, Error = "Станарот нема неплатени фактури." });
                    continue;
                }
                if (string.IsNullOrWhiteSpace(debtor.Email))
                {
                    results.Add(new ReminderSendResult { CustomerId = item.CustomerId, Error = "Нема е-пошта." });
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.Body))
                {
                    results.Add(new ReminderSendResult { CustomerId = item.CustomerId, Error = "Празна порака." });
                    continue;
                }

                try
                {
                    var subject = string.IsNullOrWhiteSpace(item.Subject) ? $"Известување од {_company.Name}" : item.Subject.Trim();
                    await _email.SendAsync(debtor.Email!.Trim(), subject, item.Body.Trim(), false, ct);
                    results.Add(new ReminderSendResult { CustomerId = item.CustomerId, Sent = true });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Потсетникот не е пратен до станар {CustomerId}", item.CustomerId);
                    results.Add(new ReminderSendResult { CustomerId = item.CustomerId, Error = "Грешка при праќање: " + ex.Message });
                }
            }

            return results;
        }

        private ReminderMessage Template(DebtorInfo d)
        {
            var periods = d.Periods.Count > 0 ? string.Join(", ", d.Periods) : $"{d.UnpaidCount} месец(и)";
            var amount = FinanceDataService.Money(d.UnpaidTotal);
            var iban = string.IsNullOrWhiteSpace(_company.IBAN) ? "" : $"\nЖиро-сметка: {_company.IBAN}";
            var signature = $"\n\nСо почит,\n{_company.Name}\n{_company.PhoneNumber}\n{_company.Email}";

            var (subject, text) = d.Level switch
            {
                ReminderLevel.Polite => (
                    $"Пријателско известување за сметка — {_company.Name}",
                    $"Почитувани {d.CustomerInfo},\n\nВе известуваме дека во нашата евиденција сè уште не е евидентирана уплатата за: {periods}, во износ од {amount}.\n\nДоколку веќе сте платиле, ве молиме занемарете ја оваа порака. Ви благодариме на редовноста.{iban}"),
                ReminderLevel.Reminder => (
                    $"Потсетник за неплатени сметки — {_company.Name}",
                    $"Почитувани {d.CustomerInfo},\n\nВе потсетуваме дека имате неплатени сметки за: {periods}, во вкупен износ од {amount}.\n\nВе молиме уплатата да ја извршите во наредните 8 дена. За прашања или договор за плаќање на рати, слободно контактирајте нè.{iban}"),
                _ => (
                    $"Опомена пред преземање мерки — {_company.Name}",
                    $"Почитувани {d.CustomerInfo},\n\nИ покрај претходните известувања, сè уште имате неплатени сметки за: {periods}, во вкупен износ од {amount}.\n\nВе молиме долгот да го подмирите во рок од 8 дена од приемот на оваа опомена. Во спротивно, ќе бидеме принудени да преземеме понатамошни чекори за наплата согласно законот. Доколку имате тешкотии, јавете ни се за да најдеме решение.{iban}")
            };

            return new ReminderMessage
            {
                CustomerId = d.CustomerId,
                CustomerInfo = d.CustomerInfo,
                Level = FinanceDataService.LevelName(d.Level),
                Subject = subject,
                Body = text + signature,
                Email = d.Email,
                PhoneNumber = d.PhoneNumber,
                AiGenerated = false
            };
        }

        private async Task<Dictionary<int, (string subject, string body)>> GenerateBatchWithAiAsync(IEnumerable<DebtorInfo> batch, CancellationToken ct)
        {
            var result = new Dictionary<int, (string subject, string body)>();

            var input = batch.Select(d => new
            {
                id = d.CustomerId,
                name = d.CustomerInfo,
                level = FinanceDataService.LevelName(d.Level),
                unpaidMonths = d.UnpaidCount,
                periods = d.Periods,
                amount = FinanceDataService.Money(d.UnpaidTotal)
            });

            var system =
                "Ти си асистент на фирма за управување со згради во Северна Македонија. Пишуваш пораки за станари со неплатени сметки " +
                "на македонски јазик, кирилица, учтиво и професионално (персирање со „Вие“).\n" +
                "Правила:\n" +
                "- Тонот зависи од нивото: „Љубезно“ = пријателско известување, можеби само заборавиле; " +
                "„Потсетник“ = поодлучно, барај уплата во 8 дена и понуди договор за рати; " +
                "„Опомена“ = сериозно и официјално, рок од 8 дена и најава на понатамошни законски чекори за наплата, но без закани и без навреди.\n" +
                "- Користи ги ТОЧНО дадените износи и периоди. Не измислувај камати, датуми, броеви на сметки или факти.\n" +
                "- Секоја порака да биде различна и природна, 60–130 зборови, без markdown.\n" +
                "- Не ставај потпис — потписот се додава автоматски.\n" +
                "- Врати САМО JSON низа без друг текст: [{\"id\": <број>, \"subject\": \"...\", \"body\": \"...\"}]";

            var user = $"Фирма: {_company.Name}\nЖиро-сметка: {_company.IBAN ?? "-"}\nТелефон: {_company.PhoneNumber ?? "-"}\n\nСтанари:\n{ClaudeClient.ToJson(input)}";

            var text = await _ai.CompleteAsync(system, user, 4000, ct);
            if (string.IsNullOrWhiteSpace(text)) return result;

            try
            {
                var start = text.IndexOf('[');
                var end = text.LastIndexOf(']');
                if (start < 0 || end <= start) return result;

                var array = JsonNode.Parse(text[start..(end + 1)]) as JsonArray;
                if (array == null) return result;

                var signature = $"\n\nСо почит,\n{_company.Name}\n{_company.PhoneNumber}\n{_company.Email}";
                foreach (var node in array)
                {
                    var id = node?["id"]?.GetValue<int>();
                    var subject = node?["subject"]?.GetValue<string>() ?? "";
                    var body = node?["body"]?.GetValue<string>() ?? "";
                    if (id.HasValue && body.Length > 0)
                        result[id.Value] = (subject, body.Trim() + signature);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                _logger.LogWarning(ex, "AI одговорот за потсетници не е валиден JSON — се користи шаблон.");
            }

            return result;
        }
    }
}
