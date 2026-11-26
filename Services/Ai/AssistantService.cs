using System.Text.Json.Nodes;

namespace CleanHub.Services.Ai
{
    public class ChatTurn
    {
        public string Role { get; set; } = "user"; // user | assistant
        public string Content { get; set; } = "";
    }

    /// <summary>
    /// „Прашај ја апликацијата“: AI НЕ пишува SQL. Има само неколку алатки што читаат податоци
    /// преку FinanceDataService (AsNoTracking). Ништо не може да се измени или избрише.
    /// </summary>
    public class AssistantService
    {
        private const int MaxToolRounds = 6;
        private const int MaxHistoryTurns = 12;

        private readonly ClaudeClient _ai;
        private readonly FinanceDataService _data;
        private readonly AnomalyService _anomalies;
        private readonly ILogger<AssistantService> _logger;

        public AssistantService(ClaudeClient ai, FinanceDataService data, AnomalyService anomalies, ILogger<AssistantService> logger)
        {
            _ai = ai;
            _data = data;
            _anomalies = anomalies;
            _logger = logger;
        }

        public bool IsConfigured => _ai.IsConfigured;

        private static string SystemPrompt() =>
            "Ти си асистент во апликацијата „Марти Хигиена“ за управување со згради и станари во Северна Македонија. " +
            $"Денес е {DateTime.Now:dd.MM.yyyy}. Одговарај на македонски јазик (кирилица), кратко и јасно.\n" +
            "Правила:\n" +
            "- Бројки и податоци земај САМО од алатките. Никогаш не измислувај износи, имиња или датуми.\n" +
            "- Ако корисникот спомене објект или станар по име, прво најди го ID-то со list_buildings или search_customers.\n" +
            "- Износите пиши во формат 1.234,56 МКД.\n" +
            "- Кога има повеќе редови податоци, прикажи ги како markdown табела (| колона | колона |).\n" +
            "- Ако нешто не може да се одговори со достапните алатки, кажи го тоа искрено.\n" +
            "- „1200“ = редовен фонд (сметки на станари), „1201“ = резервен фонд на зградата.";

        private static JsonArray Tools() => new()
        {
            Tool("list_buildings", "Листа на сите објекти (згради) со ID и име.", new JsonObject()),
            Tool("search_customers", "Пребарај станар/партнер по дел од името или адресата. Враќа ID, име и објект.",
                new JsonObject { ["query"] = Str("Дел од името, на пр. „Петровски“ или „Стан 5“") }, "query"),
            Tool("customer_balance", "Состојба на станар: задолжено, платено, салдо, неплатени фактури и последни уплати (1200).",
                new JsonObject { ["customer_id"] = Int("ID на станарот") }, "customer_id"),
            Tool("building_year_summary", "Годишни бројки за објект: фактурирано, собрано 1200 и 1201, трошоци од резервен фонд, неплатено, состојба на резервниот фонд.",
                new JsonObject { ["building_id"] = Int("ID на објектот"), ["year"] = Int("Година, на пр. 2026") }, "building_id", "year"),
            Tool("list_debtors", "Станари со неплатени фактури, подредени по број на неплатени месеци. Опционално по објект и минимален број месеци.",
                new JsonObject { ["building_id"] = Int("ID на објект (опционално)"), ["min_unpaid_months"] = Int("Минимален број неплатени месеци (опционално, стандардно 1)") }),
            Tool("anomaly_report", "Листа на можни грешки: двојни уплати, згради без фактура минатиот месец, необично големи трошоци, негативно салдо.",
                new JsonObject())
        };

        public async Task<string> AskAsync(List<ChatTurn> history, CancellationToken ct = default)
        {
            if (!_ai.IsConfigured)
                return "AI асистентот не е вклучен. Администраторот треба да го постави клучот `AI:ApiKey` (види README).";

            var messages = new JsonArray();
            foreach (var turn in history.TakeLast(MaxHistoryTurns))
            {
                var role = turn.Role == "assistant" ? "assistant" : "user";
                var content = (turn.Content ?? "").Trim();
                if (content.Length == 0) continue;
                if (content.Length > 4000) content = content[..4000];
                messages.Add(new JsonObject { ["role"] = role, ["content"] = content });
            }
            if (messages.Count == 0 || messages[^1]?["role"]?.GetValue<string>() != "user")
                return "Напишете прашање.";

            for (var round = 0; round < MaxToolRounds; round++)
            {
                var body = new JsonObject
                {
                    ["model"] = _ai.Model,
                    ["max_tokens"] = 2000,
                    ["system"] = SystemPrompt(),
                    ["tools"] = Tools(),
                    ["messages"] = JsonNode.Parse(messages.ToJsonString())
                };

                JsonObject response;
                try
                {
                    response = await _ai.SendAsync(body, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AI асистент — грешка");
                    return "Во моментов AI сервисот не одговара. Обидете се повторно за малку.";
                }

                var stop = response["stop_reason"]?.GetValue<string>();
                var content = response["content"] as JsonArray ?? new JsonArray();

                if (stop != "tool_use")
                {
                    var text = ClaudeClient.ExtractText(response);
                    return string.IsNullOrWhiteSpace(text) ? "Немам одговор на ова прашање." : text;
                }

                // Одговорот на моделот (со tool_use блокови) се враќа во разговорот
                messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = JsonNode.Parse(content.ToJsonString()) });

                var results = new JsonArray();
                foreach (var block in content)
                {
                    if (block?["type"]?.GetValue<string>() != "tool_use") continue;

                    var id = block["id"]?.GetValue<string>() ?? "";
                    var name = block["name"]?.GetValue<string>() ?? "";
                    var input = block["input"] as JsonObject ?? new JsonObject();

                    string output;
                    var isError = false;
                    try
                    {
                        output = await RunToolAsync(name, input, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Алатката {Tool} не успеа", name);
                        output = "Грешка при читање на податоците.";
                        isError = true;
                    }

                    results.Add(new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = id,
                        ["content"] = output,
                        ["is_error"] = isError
                    });
                }

                messages.Add(new JsonObject { ["role"] = "user", ["content"] = results });
            }

            return "Прашањето бара премногу чекори. Обидете се да го поставите поконкретно.";
        }

        private async Task<string> RunToolAsync(string name, JsonObject input, CancellationToken ct)
        {
            switch (name)
            {
                case "list_buildings":
                {
                    var list = await _data.GetBuildingsAsync(ct);
                    return ClaudeClient.ToJson(list.Select(b => new { id = b.Id, name = b.Name }));
                }
                case "search_customers":
                {
                    var q = input["query"]?.GetValue<string>() ?? "";
                    return ClaudeClient.ToJson(await _data.SearchCustomersAsync(q, ct));
                }
                case "customer_balance":
                {
                    var id = GetInt(input, "customer_id");
                    if (id == null) return "Недостасува customer_id.";
                    var balance = await _data.GetCustomerBalanceAsync(id.Value, ct);
                    return balance == null ? "Станарот не е пронајден." : ClaudeClient.ToJson(new
                    {
                        balance.CustomerId,
                        balance.CustomerInfo,
                        balance.BuildingName,
                        owes = Math.Round(balance.Owes, MidpointRounding.AwayFromZero),
                        paid = Math.Round(balance.Demands, MidpointRounding.AwayFromZero),
                        saldo = Math.Round(balance.Saldo, MidpointRounding.AwayFromZero),
                        balance.UnpaidInvoices,
                        balance.LastPayments
                    });
                }
                case "building_year_summary":
                {
                    var id = GetInt(input, "building_id");
                    var year = GetInt(input, "year") ?? DateTime.Now.Year;
                    if (id == null) return "Недостасува building_id.";
                    var stats = await _data.GetBuildingYearStatsAsync(id.Value, year, ct);
                    return stats == null ? "Објектот не е пронајден." : ClaudeClient.ToJson(stats);
                }
                case "list_debtors":
                {
                    var buildingId = GetInt(input, "building_id");
                    var min = GetInt(input, "min_unpaid_months") ?? 1;
                    var debtors = await _data.GetDebtorsAsync(buildingId, Math.Max(1, min), ct);
                    return ClaudeClient.ToJson(new
                    {
                        count = debtors.Count,
                        total = Math.Round(debtors.Sum(d => d.UnpaidTotal), MidpointRounding.AwayFromZero),
                        debtors = debtors.Take(60).Select(d => new
                        {
                            id = d.CustomerId,
                            name = d.CustomerInfo,
                            building = d.BuildingName,
                            unpaidMonths = d.UnpaidCount,
                            amount = Math.Round(d.UnpaidTotal, MidpointRounding.AwayFromZero),
                            periods = d.Periods
                        }),
                        note = debtors.Count > 60 ? "Прикажани се првите 60." : null
                    });
                }
                case "anomaly_report":
                {
                    var report = await _anomalies.BuildAsync(withAiSummary: false, ct);
                    return ClaudeClient.ToJson(report.Items.Take(40).Select(i => new { i.Category, i.Title, i.Detail }));
                }
                default:
                    return "Непозната алатка.";
            }
        }

        private static int? GetInt(JsonObject input, string key)
        {
            var node = input[key];
            if (node == null) return null;
            try
            {
                if (node is JsonValue v)
                {
                    if (v.TryGetValue<int>(out var i)) return i;
                    if (v.TryGetValue<double>(out var d)) return (int)d;
                    if (v.TryGetValue<string>(out var s) && int.TryParse(s, out var p)) return p;
                }
            }
            catch (InvalidOperationException) { }
            return null;
        }

        private static JsonObject Tool(string name, string description, JsonObject properties, params string[] required) => new()
        {
            ["name"] = name,
            ["description"] = description,
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = new JsonArray(required.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray())
            }
        };

        private static JsonObject Str(string description) => new() { ["type"] = "string", ["description"] = description };
        private static JsonObject Int(string description) => new() { ["type"] = "integer", ["description"] = description };
    }
}
