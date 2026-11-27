using System.Text.Json.Serialization;
using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Services.Ai
{
    /// <summary>Трошоци од резервниот фонд собрани за еден месец (ред во табелата на извештајот).</summary>
    public class MonthCost
    {
        public int Month { get; set; }
        public string Description { get; set; } = "";
        public double Amount { get; set; }
        /// <summary>Колку книжења се собрани во овој ред (0 = рачно додаден ред).</summary>
        public int Items { get; set; }
    }

    /// <summary>
    /// Зачуван годишен извештај. Се креира при генерирање, а потоа корисникот може
    /// да го менува (наслов, текст, редови по месеци), печати или брише.
    /// </summary>
    public class AnnualReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";
        /// <summary>Пресметани бројки во моментот на генерирање (не се менуваат при уредување).</summary>
        public BuildingYearStats Stats { get; set; } = new();
        public string Narrative { get; set; } = "";
        public bool AiGenerated { get; set; }
        public string? Notes { get; set; }
        public List<MonthCost> Months { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [JsonIgnore] public double MonthsTotal => Math.Round(Months.Sum(m => m.Amount), 2);
        /// <summary>Табелата е рачно сменета и не се совпаѓа со книжењата.</summary>
        [JsonIgnore] public bool MonthsDifferFromBooks => Math.Abs(MonthsTotal - Stats.ReserveCosts) >= 0.01;
    }

    /// <summary>Модел за PDF / печатење.</summary>
    public class AnnualReportDocument
    {
        public AnnualReport Report { get; set; } = new();
        public CompanyConfig Company { get; set; } = new();
        public bool AutoPrint { get; set; }
    }

    /// <summary>
    /// Годишен извештај за собрание на станари. Сите бројки ги пресметува C# (FinanceDataService),
    /// AI го пишува само објаснувачкиот текст.
    /// </summary>
    public class AnnualReportService
    {
        public static readonly string[] MonthNames =
        {
            "Јануари", "Февруари", "Март", "Април", "Мај", "Јуни",
            "Јули", "Август", "Септември", "Октомври", "Ноември", "Декември"
        };

        public static string MonthName(int month) => month >= 1 && month <= 12 ? MonthNames[month - 1] : "—";

        private readonly FinanceDataService _data;
        private readonly ClaudeClient _ai;
        private readonly CompanyConfig _company;

        public AnnualReportService(FinanceDataService data, ClaudeClient ai, IOptions<CompanyConfig> company)
        {
            _data = data;
            _ai = ai;
            _company = company.Value;
        }

        /// <summary>
        /// Ги групира поединечните трошоци по месеци. Описот е листа од различните ставки
        /// (најчестите прво), за да се гледа за што се потрошило без да се набројува секое книжење.
        /// </summary>
        public static List<MonthCost> GroupByMonth(IEnumerable<CostLine> costs)
        {
            return costs
                .Where(c => c.Date != null)
                .GroupBy(c => c.Date!.Value.Month)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var names = g
                        .Select(c => (c.Description ?? "").Trim())
                        .Where(d => d.Length > 0)
                        .GroupBy(d => d, StringComparer.CurrentCultureIgnoreCase)
                        .OrderByDescending(x => x.Count())
                        .Select(x => x.First())
                        .ToList();
                    var desc = names.Count == 0
                        ? "Трошоци од резервен фонд"
                        : string.Join(", ", names.Take(4)) + (names.Count > 4 ? $" и уште {names.Count - 4}" : "");
                    return new MonthCost
                    {
                        Month = g.Key,
                        Description = desc,
                        Amount = Math.Round(g.Sum(c => c.Amount), 2),
                        Items = g.Count()
                    };
                })
                .ToList();
        }

        public async Task<AnnualReport?> BuildAsync(int buildingId, int year, string? notes, CancellationToken ct = default)
        {
            var stats = await _data.GetBuildingYearStatsAsync(buildingId, year, ct);
            if (stats == null) return null;

            var months = GroupByMonth(stats.Costs);
            var report = new AnnualReport
            {
                Stats = stats,
                Months = months,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                Title = $"Годишен извештај за {year} година",
                Narrative = Fallback(stats, months)
            };

            if (_ai.IsConfigured)
            {
                var system =
                    "Ти пишуваш годишен извештај за собрание на станари во зграда во Северна Македонија, во име на управителот. " +
                    "Пиши на македонски (кирилица), јасно и разбирливо за обични станари, професионално и транспарентно.\n" +
                    "Структура (без markdown, само пасуси, наслови на посебен ред завршуваат со две точки):\n" +
                    "Вовед: — 1–2 реченици.\nПриходи и наплата: — колку е фактурирано и собрано, процент на наплата.\n" +
                    "Резервен фонд: — колку е собрано, потрошено и состојбата на крајот на годината.\n" +
                    "Изведени работи: — кратко за трошоците по месеци (групирај слични ставки, не набројувај секоја уплата).\n" +
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
                    costsByMonth = months.Select(m => new { month = MonthName(m.Month), items = m.Items, m.Description, amount = FinanceDataService.Money(m.Amount) }),
                    managerNotes = report.Notes
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

        private static string Fallback(BuildingYearStats s, List<MonthCost> months)
        {
            var m = FinanceDataService.Money;
            var works = s.Costs.Count == 0
                ? "Во текот на годината немаше трошоци од резервниот фонд."
                : $"Евидентирани се {s.Costs.Count} ставки на трошоци во {months.Count} месеци — прегледот по месеци е во табелата подолу.";
            return
                $"Вовед:\nВо продолжение е прегледот на финансиското работење на зградата „{s.BuildingName}“ за {s.Year} година.\n\n" +
                $"Приходи и наплата:\nВо текот на годината се издадени {s.InvoicesCount} фактури во вкупен износ од {m(s.Invoiced)}. " +
                $"Собрани се {m(s.CollectedRegular)}, што претставува наплата од {s.CollectionRate.ToString("0.#", FinanceDataService.Mk)}%.\n\n" +
                $"Резервен фонд:\nВо резервниот фонд се собрани {m(s.CollectedReserve)}, а потрошени се {m(s.ReserveCosts)}. " +
                $"Состојбата на резервниот фонд на крајот на годината изнесува {m(s.ReserveBalanceEndOfYear)}.\n\n" +
                $"Изведени работи:\n{works}\n\n" +
                $"Неплатени обврски:\nНеплатени фактури за годината: {m(s.UnpaidTotal)} кај {s.TenantsWithDebt} станари.\n\n" +
                "Заклучок:\nВи благодариме на сите станари што редовно ги плаќаат обврските. Редовната уплата овозможува навремено одржување на зградата.";
        }
    }
}
