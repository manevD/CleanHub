using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Services.Ai
{
    /// <summary>
    /// Мал клиент за Anthropic Messages API (без дополнителни NuGet пакети).
    /// </summary>
    public class ClaudeClient
    {
        private readonly HttpClient _http;
        private readonly AiConfig _config;
        private readonly ILogger<ClaudeClient> _logger;

        public ClaudeClient(HttpClient http, IOptions<AiConfig> config, ILogger<ClaudeClient> logger)
        {
            _http = http;
            _config = config.Value;
            _logger = logger;
            _http.Timeout = TimeSpan.FromSeconds(90);
        }

        public bool IsConfigured => _config.IsConfigured;
        public string Model => _config.Model;

        /// <summary>Едноставно барање: систем + корисничка порака → текст. Враќа null ако AI не е достапен.</summary>
        public async Task<string?> CompleteAsync(string system, string user, int maxTokens = 1200, CancellationToken ct = default)
        {
            if (!IsConfigured) return null;

            var body = new JsonObject
            {
                ["model"] = _config.Model,
                ["max_tokens"] = maxTokens,
                ["system"] = system,
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "user", ["content"] = user }
                }
            };

            try
            {
                var response = await SendAsync(body, ct);
                return ExtractText(response);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI барањето не успеа");
                return null;
            }
        }

        /// <summary>Сурово барање до Messages API (се користи за tool use).</summary>
        public async Task<JsonObject> SendAsync(JsonObject body, CancellationToken ct = default)
        {
            if (!IsConfigured) throw new InvalidOperationException("AI клучот не е поставен (AI:ApiKey).");

            using var request = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint);
            request.Headers.Add("x-api-key", _config.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI одговор {Status}: {Body}", (int)response.StatusCode, json.Length > 500 ? json[..500] : json);
                throw new HttpRequestException($"AI сервисот врати грешка {(int)response.StatusCode}.");
            }

            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }

        public static string ExtractText(JsonObject response)
        {
            var sb = new StringBuilder();
            if (response["content"] is JsonArray content)
            {
                foreach (var block in content)
                {
                    if (block?["type"]?.GetValue<string>() == "text")
                        sb.Append(block["text"]?.GetValue<string>());
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>Безбедна серијализација на податоци за промпт (кирилица без escape).</summary>
        public static string ToJson(object value) =>
            JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
    }
}
