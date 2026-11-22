using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Services.Ai
{
    public class AnnualReport
    {
        public BuildingYearStats Stats { get; set; } = new();
        public string Narrative { get; set; } = "";
        public bool AiGenerated { get; set; }
        public CompanyConfig Company { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Годишен извештај за собрание на станари. Сите бројки ги пресметува C# (FinanceDataService),
    /// AI го пишува само објаснувачкиот текст.
    /// </summary>
    public class AnnualReportService
    {
        private readonly FinanceDataService _data;
        private readonly ClaudeClient _ai;
        private readonly CompanyConfig _company;

        public AnnualReportService(FinanceDataService data, ClaudeClient ai, IOptions<CompanyConfig> company)
        {
            _data = data;
            _ai = ai;
            _company = company.Value;
        }

        public async Task<AnnualReport?> BuildAsync(int buildingId, int year, string? notes, CancellationToken ct = default)
        {
            var stats = await _data.GetBuildingYearStatsAsync(buildingId, year, ct);
            if (stats == null) return null;

            var report = new AnnualReport { Stats = stats, Company = _company, Narrative = Fallback(stats) };

            if (_ai.IsConfigured)
            {
                var system =
                    "Ти пишуваш годишен извештај за собрание на станари во зграда во Северна Македонија, во име на управителот. " +
                    "Пиши на македонски (кирилица), јасно и разбирливо за обични станари, професионално и транспарентно.\n" +
                    "Структура (без markdown, само пасуси, наслови на посебен ред завршуваат со две точки):\n" +
                    "Вовед: — 1–2 реченици.\nПриходи и наплата: — колку е фактурирано и собрано, процент на наплата.\n" +
                    "Резервен фонд: — колку е собрано, потрошено и состојбата на крајот на годината.\n" +
                    "Изведени работи: — кратко за трошоците (групирај слични ставки).\n" +
                    "Неплатени обврски: — без имиња на станари, само вкупно и број на станари.\n" +
                    "Заклучок: — 1–2 реченици и повик за редовно плаќање.\n" +
                    "Користи ТОЧНО дадените бројки (формат 1.234,56 МКД). Не измислувај работи, бројки или планови што не се дадени. " +
                    "Максимум 350 зборови.";

                var user = ClaudeClient.ToJson(new
                {
                    company = _company.Name,
                    building = stats.BuildingName,
                    year = stats.Year,
                    activeTenants = stats.TenantsCount,
                    invoices = stats.InvoicesCount,
                    invoiced = FinanceDataService.Money(stats.Invoiced),
                    collectedRegular = FinanceDataService.Money(stats.CollectedRegular),
                    collectionRatePercent = stats.CollectionRate,
                    collectedReserve = FinanceDataService.Money(stats.CollectedReserve),
                    reserveCosts = FinanceDataService.Money(stats.ReserveCosts),
                    reserveBalanceEndOfYear = FinanceDataService.Money(stats.ReserveBalanceEndOfYear),
                    unpaidTotal = FinanceDataService.Money(stats.UnpaidTotal),
                    tenantsWithDebt = stats.TenantsWithDebt,
                    costs = stats.Costs.Take(60).Select(c => new { date = c.Date?.ToString("dd.MM.yyyy"), c.Description, amount = FinanceDataService.Money(c.Amount) }),
                    managerNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
                });

                var text = await _ai.CompleteAsync(system, user, 1800, ct);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    report.Narrative = text;
                    report.AiGenerated = true;
                }
            }

            return report;
        }

        private static string Fallback(BuildingYearStats s)
        {
            var m = FinanceDataService.Money;
            return
                $"Вовед:\nВо продолжение е прегледот на финансиското работење на зградата „{s.BuildingName}“ за {s.Year} година.\n\n" +
                $"Приходи и наплата:\nВо текот на годината се издадени {s.InvoicesCount} фактури во вкупен износ од {m(s.Invoiced)}. " +
                $"Собрани се {m(s.CollectedRegular)}, што претставува наплата од {s.CollectionRate.ToString("0.#", FinanceDataService.Mk)}%.\n\n" +
                $"Резервен фонд:\nВо резервниот фонд се собрани {m(s.CollectedReserve)}, а потрошени се {m(s.ReserveCosts)}. " +
                $"Состојбата на резервниот фонд на крајот на годината изнесува {m(s.ReserveBalanceEndOfYear)}.\n\n" +
                $"Изведени работи:\n{(s.Costs.Count == 0 ? "Во текот на годината немаше трошоци од резервниот фонд." : $"Евидентирани се {s.Costs.Count} ставки на трошоци — деталите се во табелата подолу.")}\n\n" +
                $"Неплатени обврски:\nНеплатени фактури за годината: {m(s.UnpaidTotal)} кај {s.TenantsWithDebt} станари.\n\n" +
                "Заклучок:\nВи благодариме на сите станари што редовно ги плаќаат обврските. Редовната уплата овозможува навремено одржување на зградата.";
        }
    }
}
