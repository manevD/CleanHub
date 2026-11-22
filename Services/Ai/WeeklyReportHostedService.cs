using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Services.Ai
{
    /// <summary>
    /// Секој понеделник (по WeeklyReportHour, време во Скопје) го праќа извештајот за грешки
    /// на AI:WeeklyReportRecipient. Ако примачот не е поставен, сервисот не прави ништо.
    /// </summary>
    public class WeeklyReportHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AiConfig _config;
        private readonly ILogger<WeeklyReportHostedService> _logger;
        private DateOnly? _lastSent;

        public WeeklyReportHostedService(IServiceScopeFactory scopeFactory, IOptions<AiConfig> config, ILogger<WeeklyReportHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_config.WeeklyReportRecipient))
            {
                _logger.LogInformation("Неделниот AI извештај е исклучен (AI:WeeklyReportRecipient е празно).");
                return;
            }

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
            do
            {
                try
                {
                    var now = NowInSkopje();
                    var today = DateOnly.FromDateTime(now);
                    if (now.DayOfWeek == DayOfWeek.Monday && now.Hour >= _config.WeeklyReportHour && _lastSent != today)
                    {
                        await SendAsync(stoppingToken);
                        _lastSent = today;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Неделниот извештај не е пратен");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task SendAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var anomalies = scope.ServiceProvider.GetRequiredService<AnomalyService>();
            var email = scope.ServiceProvider.GetRequiredService<EmailSender>();

            var report = await anomalies.BuildAsync(withAiSummary: true, ct);
            var html = AnomalyService.BuildEmailHtml(report, null);

            foreach (var to in _config.WeeklyReportRecipient!.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await email.SendAsync(to, $"Неделен извештај за грешки — {report.GeneratedAt:dd.MM.yyyy}", html, isHtml: true, ct);
            }

            _logger.LogInformation("Неделниот извештај е пратен ({Count} ставки)", report.Items.Count);
        }

        private static DateTime NowInSkopje()
        {
            foreach (var id in new[] { "Europe/Skopje", "Central European Standard Time" })
            {
                try { return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(id)); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return DateTime.Now;
        }
    }
}
