using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Services.OnlinePayments
{
    /// <summary>
    /// Ги чита онлајн уплатите веднаш по стартување на апликацијата,
    /// а потоа секоја вечер во полноќ (OnlinePayments:DailyHour, време во Скопје).
    /// </summary>
    public class OnlinePaymentsHostedService : BackgroundService
    {
        private readonly CpayMailImporter _importer;
        private readonly OnlinePaymentsConfig _config;
        private readonly ILogger<OnlinePaymentsHostedService> _logger;

        public OnlinePaymentsHostedService(CpayMailImporter importer, IOptions<OnlinePaymentsConfig> config, ILogger<OnlinePaymentsHostedService> logger)
        {
            _importer = importer;
            _config = config.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.IsConfigured)
            {
                _logger.LogInformation("Онлајн уплати: исклучено (нема OnlinePayments:Username / Password).");
                return;
            }

            try
            {
                // Прво читање веднаш по стартување (кратка пауза за апликацијата да се подигне)
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                await _importer.ImportAsync(stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var delay = UntilNextRun();
                    _logger.LogInformation("Онлајн уплати: следно читање за {Delay}", delay);
                    await Task.Delay(delay, stoppingToken);
                    await _importer.ImportAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // апликацијата се гаси
            }
        }

        private TimeSpan UntilNextRun()
        {
            var tz = SkopjeTimeZone();
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            var hour = Math.Clamp(_config.DailyHour, 0, 23);
            var next = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, now.Offset);
            if (next <= now) next = next.AddDays(1);

            // точно поместување за тој ден (летно/зимско сметање на времето)
            var nextLocal = next.DateTime;
            var nextUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), tz);
            var delay = nextUtc - DateTime.UtcNow;
            return delay < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : delay;
        }

        private static TimeZoneInfo SkopjeTimeZone()
        {
            foreach (var id in new[] { "Europe/Skopje", "Central European Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.Local;
        }
    }
}
