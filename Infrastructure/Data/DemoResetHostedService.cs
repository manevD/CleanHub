namespace CleanHub.Infrastructure.Data
{
    /// <summary>
    /// ДЕМО: секоја ноќ (Demo:ResetHour, стандардно 03:00 по скопско време) базата се враќа на почетните
    /// тест податоци, за секој што пробува демото да почне од исто.
    /// Се вклучува само со Demo:ResetNightly = true; DemoSeeder.ResetAsync дополнително проверува дека базата е демо.
    /// </summary>
    public class DemoResetHostedService(IServiceProvider services, IConfiguration config, ILogger<DemoResetHostedService> logger)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!config.GetValue<bool>("Demo:ResetNightly")) return;

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = UntilNextRun(Math.Clamp(config.GetValue("Demo:ResetHour", 3), 0, 23));
                try { await Task.Delay(delay, stoppingToken); }
                catch (TaskCanceledException) { return; }

                try
                {
                    using var scope = services.CreateScope();
                    await DemoSeeder.ResetAsync(scope.ServiceProvider, config, logger);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Demo reset не успеа.");
                }
            }
        }

        private static TimeSpan UntilNextRun(int hour)
        {
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Skopje"); }
            catch { try { tz = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"); } catch { tz = TimeZoneInfo.Utc; } }

            var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            var next = now.Date.AddHours(hour);
            if (next <= now) next = next.AddDays(1);
            var nextUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(next, DateTimeKind.Unspecified), tz);
            var wait = nextUtc - DateTime.UtcNow;
            return wait < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : wait;
        }
    }
}
