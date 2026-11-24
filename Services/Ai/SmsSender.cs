using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Services.Ai
{
    /// <summary>Праќање SMS преку Telnyx Messaging API (без NuGet пакети).</summary>
    public class SmsSender
    {
        private readonly HttpClient _http;
        private readonly TelnyxConfig _config;
        private readonly ILogger<SmsSender> _logger;

        public SmsSender(HttpClient http, IOptions<TelnyxConfig> config, ILogger<SmsSender> logger)
        {
            _http = http;
            _config = config.Value;
            _logger = logger;
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        public bool IsConfigured => _config.IsConfigured;

        // ID на профилот пронајден автоматски (ако Telnyx:MessagingProfileId не е поставен)
        private static string? _discoveredProfileId;

        /// <summary>
        /// Го враќа Messaging Profile ID: од поставките, или автоматски од Telnyx
        /// (профилот чиј Alpha sender е еднаков на Telnyx:From, инаку единствениот профил).
        /// </summary>
        private async Task<string?> GetProfileIdAsync(CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(_config.MessagingProfileId)) return _config.MessagingProfileId.Trim();
            if (_discoveredProfileId != null) return _discoveredProfileId;

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.telnyx.com/v2/messaging_profiles?page[size]=50");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telnyx messaging_profiles {Status}: {Body}", (int)response.StatusCode, json.Length > 300 ? json[..300] : json);
                return null;
            }

            var profiles = (JsonNode.Parse(json)?["data"] as JsonArray)?
                .Where(p => p != null)
                .Select(p => new
                {
                    Id = p!["id"]?.GetValue<string>(),
                    Alpha = p["alpha_sender"]?.GetValue<string>(),
                    Enabled = p["enabled"]?.GetValue<bool>() ?? true
                })
                .Where(p => !string.IsNullOrWhiteSpace(p.Id) && p.Enabled)
                .ToList();

            if (profiles == null || profiles.Count == 0) return null;

            var match = profiles.FirstOrDefault(p => string.Equals(p.Alpha, _config.From, StringComparison.OrdinalIgnoreCase))
                        ?? (profiles.Count == 1 ? profiles[0] : null);

            _discoveredProfileId = match?.Id;
            if (_discoveredProfileId != null)
                _logger.LogInformation("Telnyx Messaging Profile пронајден автоматски: {Id}", _discoveredProfileId);
            return _discoveredProfileId;
        }

        /// <summary>
        /// Македонски број → E.164 (+389...). „070 123 456“ → „+38970123456“. Враќа null ако не е валиден.
        /// </summary>
        public static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var p = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
            if (p.StartsWith("00")) p = "+" + p[2..];
            else if (p.StartsWith("389")) p = "+" + p;
            else if (p.StartsWith("0")) p = "+389" + p[1..];
            else if (!p.StartsWith("+")) p = "+389" + p;

            var digits = p.Count(char.IsDigit);
            return digits is >= 10 and <= 15 ? p : null;
        }

        private static readonly Dictionary<char, string> MkLatin = new()
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['ѓ'] = "gj", ['е'] = "e", ['ж'] = "zh",
            ['з'] = "z", ['ѕ'] = "dz", ['и'] = "i", ['ј'] = "j", ['к'] = "k", ['л'] = "l", ['љ'] = "lj", ['м'] = "m",
            ['н'] = "n", ['њ'] = "nj", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['ќ'] = "kj",
            ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "c", ['ч'] = "ch", ['џ'] = "dj", ['ш'] = "sh"
        };

        /// <summary>
        /// Македонска кирилица → латиница. SMS секогаш се праќа на латиница: 160 знаци по SMS наместо 70
        /// (до 2× поевтино). Се отстрануваат и „паметни“ наводници/цртички што би го вратиле во Unicode.
        /// </summary>
        public static string ToLatin(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length + 16);
            foreach (var ch in text)
            {
                var low = char.ToLowerInvariant(ch);
                if (MkLatin.TryGetValue(low, out var lat))
                {
                    sb.Append(ch == low ? lat : char.ToUpperInvariant(lat[0]) + lat[1..]);
                    continue;
                }
                sb.Append(ch switch
                {
                    '„' or '“' or '”' or '«' or '»' => "\"",
                    '‘' or '’' => "'",
                    '–' or '—' => "-",
                    '…' => "...",
                    '№' => "br.",
                    '\u00A0' => " ",
                    _ => ch.ToString()
                });
            }
            return sb.ToString();
        }

        /// <summary>Враќа null ако е успешно, или текст на грешката.</summary>
        public async Task<string?> SendAsync(string phone, string text, CancellationToken ct = default)
        {
            if (!IsConfigured) return "SMS не е поставен (Telnyx:ApiKey).";

            var to = NormalizePhone(phone);
            if (to == null) return "Неважечки телефонски број.";
            if (string.IsNullOrWhiteSpace(text)) return "Празна порака.";

            string? profileId;
            try
            {
                profileId = await GetProfileIdAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                _logger.LogWarning(ex, "Telnyx профилот не може да се прочита");
                return "Нема врска со Telnyx.";
            }
            if (profileId == null)
                return "Не е пронајден Messaging Profile во Telnyx. Поставете Telnyx:MessagingProfileId.";

            var body = new JsonObject
            {
                ["from"] = _config.From,
                ["to"] = to,
                ["text"] = ToLatin(text.Trim()),
                ["messaging_profile_id"] = profileId
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            try
            {
                using var response = await _http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Telnyx {Status}: {Body}", (int)response.StatusCode, json.Length > 500 ? json[..500] : json);

                // Telnyx враќа {"errors":[{"title":"...","detail":"..."}]}
                try
                {
                    var err = (JsonNode.Parse(json)?["errors"] as JsonArray)?.FirstOrDefault();
                    var detail = err?["detail"]?.GetValue<string>() ?? err?["title"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(detail)) return "Telnyx: " + detail;
                }
                catch { /* не е JSON */ }

                return $"Telnyx грешка {(int)response.StatusCode}.";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "SMS не е пратен");
                return "Нема врска со Telnyx.";
            }
        }
    }
}
