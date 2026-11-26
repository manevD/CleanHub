using CleanHub.Config;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CleanHub.Services.OnlinePayments
{
    public class ImportResult
    {
        public bool Ok { get; set; }
        public int Read { get; set; }
        public int Added { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Ги чита е-пораките од cPay (IMAP, само читање — ништо не се брише/менува во сандачето)
    /// и ги зачувува уплатите од прикачениот orderInfo.xml.
    /// </summary>
    public class CpayMailImporter
    {
        private readonly OnlinePaymentsConfig _config;
        private readonly OnlinePaymentStore _store;
        private readonly ILogger<CpayMailImporter> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public CpayMailImporter(IOptions<OnlinePaymentsConfig> config, OnlinePaymentStore store, ILogger<CpayMailImporter> logger)
        {
            _config = config.Value;
            _store = store;
            _logger = logger;
        }

        public bool IsConfigured => _config.IsConfigured;

        public async Task<ImportResult> ImportAsync(CancellationToken ct = default)
        {
            if (!_config.IsConfigured)
                return new ImportResult { Error = "Не е поставена е-пошта за онлајн уплати (OnlinePayments:Username / Password)." };

            if (!await _gate.WaitAsync(0, ct))
                return new ImportResult { Error = "Читањето веќе е во тек." };

            try
            {
                var snapshot = _store.Snapshot();
                // Преклопување од 3 дена — дупликатите се прескокнуваат по cPayPaymentRef
                var since = (snapshot.LastRunUtc?.AddDays(-3) ?? DateTime.UtcNow.AddDays(-_config.InitialDays)).Date;

                var found = new List<OnlinePayment>();
                var read = 0;

                using (var client = new ImapClient())
                {
                    await client.ConnectAsync(_config.ImapHost, _config.ImapPort, SecureSocketOptions.SslOnConnect, ct);
                    await client.AuthenticateAsync(_config.Username, _config.Password, ct);

                    var folder = string.Equals(_config.Folder, "INBOX", StringComparison.OrdinalIgnoreCase)
                        ? client.Inbox
                        : await client.GetFolderAsync(_config.Folder, ct);
                    await folder.OpenAsync(FolderAccess.ReadOnly, ct);

                    var query = SearchQuery.FromContains(_config.FromAddress).And(SearchQuery.DeliveredAfter(since));
                    var uids = await folder.SearchAsync(query, ct);

                    foreach (var uid in uids)
                    {
                        var message = await folder.GetMessageAsync(uid, ct);
                        read++;

                        foreach (var part in message.BodyParts.OfType<MimePart>())
                        {
                            var name = part.FileName ?? part.ContentType?.Name ?? "";
                            var isXml = name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                                        || part.ContentType?.MimeType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true;
                            if (!isXml || part.Content == null) continue;

                            using var ms = new MemoryStream();
                            await part.Content.DecodeToAsync(ms, ct);
                            var xml = System.Text.Encoding.UTF8.GetString(ms.ToArray()).TrimStart('﻿');

                            var payment = CpayParser.Parse(xml, message.Date, message.MessageId);
                            if (payment != null) found.Add(payment);
                        }
                    }

                    await client.DisconnectAsync(true, ct);
                }

                var added = _store.AddRange(found);
                _store.SetRun(DateTime.UtcNow, added, null);
                _logger.LogInformation("Онлајн уплати: прочитани {Read} пораки, нови {Added}", read, added);
                return new ImportResult { Ok = true, Read = read, Added = added };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Онлајн уплати: грешка при читање на е-поштата");
                var msg = ex is AuthenticationException
                    ? "Најавата на е-поштата не успеа. Проверете ги корисничкото име и App Password."
                    : "Грешка при читање на е-поштата: " + ex.Message;
                _store.SetRun(DateTime.UtcNow, 0, msg);
                return new ImportResult { Error = msg };
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
